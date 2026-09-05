using UnityEngine;

/// <summary>
/// 植物 Buff 数据（ScriptableObject）。
/// 在 Project 窗口右键 Create -&gt; Gamejam -&gt; PlantBuff 创建，
/// 配置好后拖进 BuffSystem 的 BuffList 即可生效。
///
/// 一块 Buff 数据只管三件事：
/// 1. 效果倍率：生长 / 水分消耗 / 养分消耗，全部乘法叠加，1 表示无影响
/// 2. 持续时间：在 Min/Max 之间随机
/// 3. 触发事件名：TriggerEventName 填天气资产的 StartEventName（如 WeatherRainStart）
///    或元素资产的 UseEventName（如 ElementSteamUsed）等单参数事件名，
///    BuffSystem 会自动订阅，事件触发 = 全场植物挂上该 Buff。
///
/// 新增一种 Buff 只需要新建一个资产、填好字段，不用改任何代码。
///
/// 用法示例（在 Buff 资产上填 TriggerEventName = "WeatherStormStart"）：
/// 雷暴天气开始 -> 自动触发该 Buff，无需任何订阅代码。
/// </summary>
[CreateAssetMenu(menuName = "Gamejam/PlantBuff", fileName = "NewPlantBuff")]
public class PlantBuff : ScriptableObject
{
    [Header("基础信息")]
    [Tooltip("显示名称")]
    public string DisplayName;

    [Header("持续时间（秒）")]
    [Tooltip("最短持续秒数")]
    public float MinDuration = 10f;

    [Tooltip("最长持续秒数")]
    public float MaxDuration = 15f;

    [Header("效果倍率（乘法叠加，1 = 无影响）")]
    [Tooltip("生长速度倍率：2 = 生长翻倍，0.5 = 减半，0 = 停止生长")]
    public float GrowthMultiplier = 1f;

    [Tooltip("水分消耗倍率：0.5 = 消耗减半（如湿润），2 = 消耗翻倍（如干旱）")]
    public float WaterDrainMultiplier = 1f;

    [Tooltip("养分消耗倍率")]
    public float NutrientDrainMultiplier = 1f;

    [Header("触发（可选）")]
    [Tooltip("触发本 Buff 的事件名，填天气/元素资产上的事件名（单参数事件）。留空则只能代码调用 BuffSystem.ApplyBuff 手动触发")]
    public string TriggerEventName;

    /// <summary>在最短/最长持续时间之间随机一个值（秒）</summary>
    public float RandomDuration =>
        Random.Range(Mathf.Min(MinDuration, MaxDuration), Mathf.Max(MinDuration, MaxDuration));

    public override string ToString() => string.IsNullOrEmpty(DisplayName) ? name : DisplayName;
}
