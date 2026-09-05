using Framwork;
using UnityEngine;

/// <summary>植物生长阶段</summary>
public enum GrowthStage
{
    /// <summary>种子</summary>
    Seed = 0,
    /// <summary>发芽</summary>
    Sprout = 1,
    /// <summary>成熟</summary>
    Mature = 2,
}

/// <summary>
/// 植物类：挂载到植物 GameObject 上使用。
///
/// 三个属性：
/// - 水分 Water：随时间消耗，浇水补充
/// - 阳光 Sunlight：作为生长速度倍率（光照越足长得越快），由外部环境设置
/// - 养分 Nutrient：随时间消耗，施肥补充
///
/// 三个阶段：种子 Seed -> 发芽 Sprout -> 成熟 Mature
/// 水分/养分高于阈值时持续生长，攒满每阶段所需时间就进入下一阶段；
/// 每次进入新阶段会通过 EventCenter 广播 PlantStageChange 事件。
///
/// Buff 接入：BuffSystem（天气/元素事件触发）提供全场生长与消耗倍率，
/// 本类每帧读取并乘上；场景里没挂 BuffSystem 时按倍率 1 正常生长。
///
/// 用法示例：
/// <code>
/// // 浇水 / 施肥 / 调整光照（任意脚本调用）
/// plant.WaterPlant(30f);
/// plant.Fertilize(20f);
/// plant.Sunlight = 80f;
///
/// // 监听阶段变化（如刷新外观、播动画）
/// EventCenter.Subscribe&lt;Plant, GrowthStage&gt;(EventName.PlantStageChange, (p, stage) => { ... });
/// </code>
/// </summary>
public class Plant : MonoBehaviour
{
    [Header("资源属性 (0~100)")]
    [Tooltip("当前水分，随时间消耗")]
    [Range(0f, 100f)] public float Water = 50f;

    [Tooltip("当前阳光，决定生长速度倍率，由外部环境设置")]
    [Range(0f, 100f)] public float Sunlight = 50f;

    [Tooltip("当前养分，随时间消耗")]
    [Range(0f, 100f)] public float Nutrient = 50f;

    [Header("生长设置")]
    [Tooltip("当前生长阶段")]
    public GrowthStage Stage = GrowthStage.Seed;

    [Tooltip("每个阶段需要的生长时间（秒）")]
    public float StageDuration = 10f;

    [Tooltip("每秒消耗的水分")]
    public float WaterConsumePerSec = 1.5f;

    [Tooltip("每秒消耗的养分")]
    public float NutrientConsumePerSec = 1f;

    [Tooltip("生长所需的最低水分")]
    public float MinWaterToGrow = 20f;

    [Tooltip("生长所需的最低养分")]
    public float MinNutrientToGrow = 15f;

    /// <summary>当前阶段的生长进度（0~1），可用来驱动缩放动画</summary>
    public float StageProgress { get; private set; }

    /// <summary>是否已成熟</summary>
    public bool IsMature => Stage == GrowthStage.Mature;

    /// <summary>生长速度倍率：阳光 0 -> 0.3 倍速，阳光 100 -> 1.5 倍速</summary>
    private float GrowthSpeedFactor => Mathf.Lerp(0.3f, 1.5f, Sunlight / 100f);

    private void Update()
    {
        // 成熟后不再生长，但仍然消耗资源（可按需改成不消耗）
        if (IsMature) return;

        // 0. Buff 倍率（没挂 BuffSystem 时视为 1，不影响原逻辑）
        var buffs = BuffSystem.Instance;
        float growthMul = 1f, waterMul = 1f, nutrientMul = 1f;
        if (buffs != null)
        {
            growthMul = buffs.GrowthMultiplier;
            waterMul = buffs.WaterDrainMultiplier;
            nutrientMul = buffs.NutrientDrainMultiplier;
        }

        // 1. 资源随时间消耗（消耗速率受 Buff 影响）
        Water = Mathf.Max(0f, Water - WaterConsumePerSec * waterMul * Time.deltaTime);
        Nutrient = Mathf.Max(0f, Nutrient - NutrientConsumePerSec * nutrientMul * Time.deltaTime);

        // 2. 资源不足则暂停生长（缺水或缺肥）
        if (Water < MinWaterToGrow || Nutrient < MinNutrientToGrow)
            return;

        // 3. 正常生长：阳光决定速度，Buff 再乘一个倍率
        StageProgress += Time.deltaTime * GrowthSpeedFactor * growthMul / StageDuration;
        if (StageProgress >= 1f)
        {
            StageProgress = 0f;
            GrowToNextStage();
        }
    }

    /// <summary>进入下一生长阶段，并广播事件</summary>
    private void GrowToNextStage()
    {
        var next = (GrowthStage)Mathf.Min((int)Stage + 1, (int)GrowthStage.Mature);
        if (next == Stage) return;

        Stage = next;
        Debug.Log($"[Plant] {name} 进入阶段：{Stage}");
        EventCenter.Trigger(EventName.PlantStageChange, this, Stage);
    }

    // ==================== 外部操作接口 ====================

    /// <summary>浇水（增加水分）</summary>
    public void WaterPlant(float amount)
    {
        Water = Mathf.Min(100f, Water + amount);
    }

    /// <summary>施肥（增加养分）</summary>
    public void Fertilize(float amount)
    {
        Nutrient = Mathf.Min(100f, Nutrient + amount);
    }

    /// <summary>补充阳光（例如放到太阳下/使用光照道具）</summary>
    public void AddSunlight(float amount)
    {
        Sunlight = Mathf.Min(100f, Sunlight + amount);
    }

    /// <summary>重置回种子阶段（收获后再种一轮）</summary>
    public void ResetPlant()
    {
        Stage = GrowthStage.Seed;
        StageProgress = 0f;
        Water = 50f;
        Nutrient = 50f;
    }
}
