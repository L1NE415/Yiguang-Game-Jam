using System;
using System.Collections.Generic;
using Framwork;
using UnityEngine;

/// <summary>
/// 生效中的 Buff 实例（Buff 资产 + 剩余时间），由 BuffSystem 内部维护。
/// </summary>
[Serializable]
public class ActiveBuff
{
    public PlantBuff Buff;
    public float RemainingTime;
}

/// <summary>
/// Buff 系统（场景单例，继承 Framwork.Singleton）。
/// 挂到场景中一个常驻 GameObject 上（可以和 WeatherSystem 挂同一个物体）。
///
/// 职责：
/// - 维护 Buff 列表（Inspector 中配置），自动订阅每个 Buff 的 TriggerEventName
/// - 事件触发（天气 / 元素 / 任意单参数事件）即全场生效该 Buff
/// - 按剩余时间自动过期，重复触发 = 刷新持续时间
/// - 把所有生效中 Buff 的倍率乘起来，暴露给 Plant 读取
/// - 生效 / 结束时通过 EventCenter 广播 BuffApplied / BuffRemoved
///
/// 注意：BuffSystem 不主动找植物，是 Plant 每帧来读合并倍率
/// （没挂 BuffSystem 时 Plant 视为倍率 1，正常生长）。
///
/// 用法示例：
/// <code>
/// // 代码手动上一个 Buff（比如道具效果）
/// BuffSystem.Instance.ApplyBuff(myBuff);
/// BuffSystem.Instance.ApplyBuff(myBuff, 30f); // 指定持续 30 秒
///
/// // 监听 Buff 生效 / 结束（比如 UI 显示 Buff 图标）
/// EventCenter.Subscribe&lt;PlantBuff, float&gt;(EventName.BuffApplied, (b, t) => { ... });
/// EventCenter.Subscribe&lt;PlantBuff&gt;(EventName.BuffRemoved, b => { ... });
/// </code>
/// </summary>
public class BuffSystem : Singleton<BuffSystem>
{
    [Tooltip("Buff 列表。TriggerEventName 非空的 Buff 会在对应事件触发时自动全场生效")]
    public List<PlantBuff> Buffs = new List<PlantBuff>();

    /// <summary>当前生效中的 Buff（含剩余时间），可用于 UI 遍历</summary>
    public IReadOnlyList<ActiveBuff> ActiveBuffs => activeBuffs;

    private readonly List<ActiveBuff> activeBuffs = new List<ActiveBuff>();

    // 订阅记录，OnDestroy 时成对取消，防止事件泄漏
    private readonly List<(string eventName, Action<Weather> weatherHandler, Action<Element> elementHandler)> subscriptions
        = new List<(string, Action<Weather>, Action<Element>)>();

    // ==================== 合并倍率（Plant 每帧读取） ====================

    /// <summary>全场生长速度倍率（所有生效 Buff 相乘，无 Buff 时为 1）</summary>
    public float GrowthMultiplier { get; private set; } = 1f;

    /// <summary>全场水分消耗倍率</summary>
    public float WaterDrainMultiplier { get; private set; } = 1f;

    /// <summary>全场养分消耗倍率</summary>
    public float NutrientDrainMultiplier { get; private set; } = 1f;

    /// <summary>某个 Buff 是否正在生效</summary>
    public bool HasBuff(PlantBuff buff) => FindActive(buff) != null;

    protected override void Awake()
    {
        base.Awake();

        // 订阅每个 Buff 的触发事件。
        // 天气/元素的自定义事件都是单参数事件，但参数类型不同（Weather / Element），
        // 所以两种签名都订阅，参数本身不用、只当触发信号。
        foreach (var buff in Buffs)
        {
            if (buff == null || string.IsNullOrEmpty(buff.TriggerEventName))
                continue;

            var b = buff; // 闭包捕获
            var weatherHandler = new Action<Weather>(_ => ApplyBuff(b));
            var elementHandler = new Action<Element>(_ => ApplyBuff(b));

            EventCenter.Subscribe<Weather>(b.TriggerEventName, weatherHandler);
            EventCenter.Subscribe<Element>(b.TriggerEventName, elementHandler);
            subscriptions.Add((b.TriggerEventName, weatherHandler, elementHandler));
        }
    }

    protected override void OnDestroy()
    {
        foreach (var s in subscriptions)
        {
            EventCenter.Unsubscribe<Weather>(s.eventName, s.weatherHandler);
            EventCenter.Unsubscribe<Element>(s.eventName, s.elementHandler);
        }
        subscriptions.Clear();
        activeBuffs.Clear();

        base.OnDestroy();
    }

    private void Update()
    {
        if (activeBuffs.Count == 0)
            return;

        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            var ab = activeBuffs[i];
            ab.RemainingTime -= Time.deltaTime;
            if (ab.RemainingTime <= 0f)
            {
                activeBuffs.RemoveAt(i);
                RecalculateMultipliers();
                EventCenter.Trigger(EventName.BuffRemoved, ab.Buff);
                Debug.Log($"[BuffSystem] Buff 结束：{ab.Buff}");
            }
        }
    }

    // ==================== 对外接口 ====================

    /// <summary>
    /// 上一个 Buff（全场生效）。
    /// 已在生效中则刷新持续时间。durationOverride 不传则用资产上的随机时长。
    /// </summary>
    public void ApplyBuff(PlantBuff buff, float? durationOverride = null)
    {
        if (buff == null)
        {
            Debug.LogWarning("[BuffSystem] ApplyBuff 的参数为空");
            return;
        }

        float duration = durationOverride ?? buff.RandomDuration;

        var existing = FindActive(buff);
        if (existing != null)
        {
            // 重复触发 = 刷新持续时间，倍率不变
            existing.RemainingTime = duration;
        }
        else
        {
            activeBuffs.Add(new ActiveBuff { Buff = buff, RemainingTime = duration });
            RecalculateMultipliers();
        }

        EventCenter.Trigger(EventName.BuffApplied, buff, duration);
        Debug.Log($"[BuffSystem] Buff 生效：{buff}，持续 {duration:F1}s");
    }

    /// <summary>手动移除一个生效中的 Buff（比如使用了解除类道具）</summary>
    public void RemoveBuff(PlantBuff buff)
    {
        var existing = FindActive(buff);
        if (existing == null)
            return;

        activeBuffs.Remove(existing);
        RecalculateMultipliers();
        EventCenter.Trigger(EventName.BuffRemoved, buff);
        Debug.Log($"[BuffSystem] Buff 被移除：{buff}");
    }

    // ==================== 内部实现 ====================

    private ActiveBuff FindActive(PlantBuff buff)
    {
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            if (activeBuffs[i].Buff == buff)
                return activeBuffs[i];
        }
        return null;
    }

    /// <summary>把所有生效中 Buff 的倍率乘起来（无 Buff 时回到 1）</summary>
    private void RecalculateMultipliers()
    {
        float growth = 1f, water = 1f, nutrient = 1f;
        foreach (var ab in activeBuffs)
        {
            growth *= ab.Buff.GrowthMultiplier;
            water *= ab.Buff.WaterDrainMultiplier;
            nutrient *= ab.Buff.NutrientDrainMultiplier;
        }
        GrowthMultiplier = growth;
        WaterDrainMultiplier = water;
        NutrientDrainMultiplier = nutrient;
    }
}
