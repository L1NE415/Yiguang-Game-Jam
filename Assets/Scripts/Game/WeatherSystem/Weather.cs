using UnityEngine;

/// <summary>
/// 天气数据（ScriptableObject）。
/// 在 Project 窗口右键 Create -&gt; Gamejam -&gt; Weather 创建天气资产，
/// 配置好后拖进 WeatherSystem 的 WeatherList 即可参与轮转。
///
/// 一块天气数据只管两件事：
/// 1. 持续时间：在 Min/Max 之间随机
/// 2. 自定义事件：StartEventName / EndEventName，天气开始与结束时通过 EventCenter 广播
///
/// "不同的天气触发不同的事件"就是靠第 2 点实现的：
/// 新增一种天气只需要新建一个资产、填上事件名，不用改任何代码。
///
/// 用法示例（在天气资产上填 StartEventName = "WeatherStormStart"）：
/// <code>
/// EventCenter.Subscribe&lt;Weather&gt;("WeatherStormStart", w =&gt; { /* 雷暴开始：劈一道闪电 */ });
/// </code>
/// </summary>
[CreateAssetMenu(menuName = "Gamejam/Weather", fileName = "NewWeather")]
public class Weather : ScriptableObject
{
    [Header("基础信息")]
    [Tooltip("显示名称")]
    public string DisplayName;

    [Header("持续时间（秒）")]
    [Tooltip("本次天气的最短持续时间")]
    public float MinDuration = 20f;

    [Tooltip("本次天气的最长持续时间")]
    public float MaxDuration = 40f;

    [Header("自定义事件（可选，留空则不额外触发）")]
    [Tooltip("该天气开始时触发的事件名（参数：Weather 本次天气）")]
    public string StartEventName;

    [Tooltip("该天气结束时触发的事件名（参数：Weather 结束的天气）")]
    public string EndEventName;

    /// <summary>在最短/最长持续时间之间随机一个值（秒）</summary>
    public float RandomDuration =>
        Random.Range(Mathf.Min(MinDuration, MaxDuration), Mathf.Max(MinDuration, MaxDuration));

    public override string ToString() => string.IsNullOrEmpty(DisplayName) ? name : DisplayName;
}
