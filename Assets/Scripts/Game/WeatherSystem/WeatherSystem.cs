using System.Collections.Generic;
using Framwork;
using UnityEngine;

/// <summary>
/// 天气系统（场景单例，继承 Framwork.Singleton）。
/// 挂到场景中一个常驻 GameObject 上即可。
///
/// 职责只有一个：维护可出现的天气列表，按持续时间自动轮转，
/// 切换时通过 EventCenter 广播事件——具体表现由各业务系统自己监听实现。
///
/// 事件一览（均定义在 Framwork.EventName 中）：
/// - WeatherChanged（Weather 旧天气, Weather 新天气）每一次切换都会触发（首个天气时旧天气为 null）
/// - WeatherStarted（Weather 本次天气, float 持续秒数）天气开始
/// - WeatherEnded（Weather 结束的天气）天气结束
/// 此外，每种天气资产上配置的 StartEventName / EndEventName 会在对应时机额外触发，
/// 这就是"不同的天气触发不同的事件"的实现方式。
///
/// 用法示例：
/// <code>
/// // 手动切换（比如玩家用道具求雨）
/// WeatherSystem.Instance.SetWeather(rainWeather);
/// WeatherSystem.Instance.GoToNextWeather();
///
/// // 监听任意天气切换
/// EventCenter.Subscribe&lt;Weather, Weather&gt;(EventName.WeatherChanged,
///     (oldWeather, newWeather) =&gt; Debug.Log($"{oldWeather} -&gt; {newWeather}"));
///
/// // 监听某一种天气特有的事件（事件名填在天气资产的 StartEventName 上）
/// EventCenter.Subscribe&lt;Weather&gt;("WeatherStormStart", w =&gt; { /* 雷暴开始，劈一道闪电 */ });
/// </code>
/// </summary>
public class WeatherSystem : Singleton<WeatherSystem>
{
    [Header("天气配置")]
    [Tooltip("可出现的天气列表，运行时从中随机轮转")]
    public List<Weather> WeatherList = new List<Weather>();

    [Tooltip("开局的第一个天气；留空则从列表中随机取一个")]
    public Weather StartWeather;

    [Header("轮转设置")]
    [Tooltip("是否自动轮转；关闭后只能通过 SetWeather / GoToNextWeather 手动切换")]
    public bool AutoCycle = true;

    [Tooltip("是否允许连续两轮抽到同一种天气")]
    public bool AllowRepeat = false;

    [Tooltip("天气推进速度倍率：1 为实时，调试时可调大加快切换")]
    public float TimeScale = 1f;

    /// <summary>当前天气（未启动时为 null）</summary>
    public Weather CurrentWeather { get; private set; }

    /// <summary>上一个天气</summary>
    public Weather PreviousWeather { get; private set; }

    /// <summary>本次天气的总时长（秒）</summary>
    public float Duration { get; private set; }

    /// <summary>本次天气剩余时间（秒）</summary>
    public float RemainingTime { get; private set; }

    /// <summary>本次天气的进度 0~1（1 表示即将切换），可用于天气倒计时 UI</summary>
    public float Progress => Duration <= 0f ? 1f : Mathf.Clamp01((Duration - RemainingTime) / Duration);

    private void Start()
    {
        if (WeatherList == null)
            WeatherList = new List<Weather>();

        // 清掉列表里的空引用，避免轮转时抽到 null
        WeatherList.RemoveAll(w => w == null);

        if (WeatherList.Count == 0)
        {
            Debug.LogWarning("[WeatherSystem] 天气列表为空，请在 Inspector 中配置 WeatherList，天气系统已停用");
            enabled = false;
            return;
        }

        var first = StartWeather != null ? StartWeather : WeatherList[Random.Range(0, WeatherList.Count)];
        SetWeather(first);
    }

    private void Update()
    {
        if (CurrentWeather == null) return;

        var dt = Time.deltaTime * TimeScale;
        RemainingTime -= dt;

        if (AutoCycle && RemainingTime <= 0f)
            GoToNextWeather();
    }

    // ==================== 天气切换 ====================

    /// <summary>
    /// 切换到指定天气并广播全部事件。
    /// 触发顺序：旧天气 WeatherEnded + 旧天气 EndEventName
    ///          -&gt; WeatherChanged
    ///          -&gt; 新天气 WeatherStarted + 新天气 StartEventName
    /// </summary>
    public void SetWeather(Weather next)
    {
        if (next == null)
        {
            Debug.LogWarning("[WeatherSystem] 要切换的天气为空");
            return;
        }

        var old = CurrentWeather;

        // 1. 结束旧天气
        if (old != null)
        {
            EventCenter.Trigger(EventName.WeatherEnded, old);

            if (!string.IsNullOrEmpty(old.EndEventName))
                EventCenter.Trigger(old.EndEventName, old);
        }

        // 2. 记账
        PreviousWeather = old;
        CurrentWeather = next;
        Duration = next.RandomDuration;
        RemainingTime = Duration;

        // 3. 开始新天气
        EventCenter.Trigger(EventName.WeatherChanged, old, next);
        EventCenter.Trigger(EventName.WeatherStarted, next, Duration);

        if (!string.IsNullOrEmpty(next.StartEventName))
            EventCenter.Trigger(next.StartEventName, next);

        Debug.Log($"[WeatherSystem] 天气切换：{old} -> {next}，持续 {Duration:F1}s");
    }

    /// <summary>随机切换到下一种天气（受 AllowRepeat 影响）</summary>
    public void GoToNextWeather()
    {
        if (WeatherList == null || WeatherList.Count == 0)
            return;

        var count = WeatherList.Count;
        var index = Random.Range(0, count);

        // 不允许重复时，跳过当前天气再抽一个下标，保证一定换一种
        if (!AllowRepeat && count > 1 && WeatherList[index] == CurrentWeather)
            index = (index + 1 + Random.Range(0, count - 1)) % count;

        SetWeather(WeatherList[index]);
    }

    /// <summary>把当前天气重新随机一个时长（比如玩家用道具延长/缩短当前天气）</summary>
    public void RerollDuration()
    {
        if (CurrentWeather == null) return;
        Duration = CurrentWeather.RandomDuration;
        RemainingTime = Duration;
    }

    /// <summary>在 Inspector 右键菜单里手动切下一个天气，方便调试</summary>
    [ContextMenu("切换到下一个天气")]
    private void DebugGoToNextWeather()
    {
        GoToNextWeather();
    }
}
