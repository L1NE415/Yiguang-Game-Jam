using System;
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
/// 单个生长阶段的需求配置。
/// MinXxx 为 0 表示该阶段不需要这种资源。
/// </summary>
[Serializable]
public class StageRequirement
{
    [Tooltip("本阶段限时（秒）：倒计时长满进度则进入下一阶段，超时未长满则死亡")]
    public float TimeLimit = 30f;

    [Tooltip("本阶段最低水分需求（0 = 不需要水）")]
    public float MinWater = 0f;

    [Tooltip("本阶段最低阳光需求（0 = 不需要阳光）")]
    public float MinSunlight = 0f;

    [Tooltip("本阶段最低养分需求（0 = 不需要养分）")]
    public float MinNutrient = 0f;
}

/// <summary>
/// 植物类：挂载到植物 GameObject 上使用。
///
/// 三个资源：
/// - 水分 Water：随时间消耗，浇水（WaterPlant）补充
/// - 阳光 Sunlight：由外部环境设置（天气/道具），既是需求门槛也是生长加速项
/// - 养分 Nutrient：随时间消耗，施肥（Fertilize）补充
///
/// 三个阶段的资源需求（在 Inspector 的 StageRequirements 里按顺序配置）：
/// - 种子 Seed：需要水 + 少量阳光，不需要养分
/// - 发芽 Sprout：需要阳光 + 水 + 少量养分
/// - 成熟 Mature：水 / 阳光 / 养分都要，且养分需求更高
///
/// 生长规则：
/// - 每个阶段有固定限时（TimeLimit），进入阶段后倒计时开始走
/// - 只有当前阶段的三项需求全部满足时，进度条才增长；缺任意一项就停在原地
/// - 进度长满 → 进入下一阶段（成熟阶段长满 = 完全成熟，游戏胜利）
/// - 倒计时归零而进度没长满 → 植物死亡，广播 PlantFailed，由游戏管理脚本判定游戏结束
/// - 阳光高于需求时提供额外加速（最多 1.5 倍），但不会因为阳光低而拖慢到超时
///
/// Buff 接入：BuffSystem（天气/元素事件触发）提供全场生长与消耗倍率，
/// 本类每帧读取并乘上；场景里没挂 BuffSystem 时按倍率 1 正常生长。
///
/// 事件（均在 Framwork.EventName 中）：
/// - PlantStageChange(Plant, GrowthStage)  进入新的生长阶段
/// - PlantFullyGrown(Plant)                三个阶段全部完成，完全成熟
/// - PlantFailed(Plant, GrowthStage)       某阶段超时未达成需求，植物死亡（游戏结束信号）
///
/// 用法示例：
/// <code>
/// // 浇水 / 施肥 / 调整光照（任意脚本调用）
/// plant.WaterPlant(30f);
/// plant.Fertilize(20f);
/// plant.Sunlight = 80f;
///
/// // UI 提示：缺什么、还剩多久
/// if (!plant.RequirementsMet) Debug.Log(plant.MissingResourceText);
/// Debug.Log(plant.RemainingTime);
///
/// // 监听阶段变化 / 成熟 / 失败
/// EventCenter.Subscribe&lt;Plant, GrowthStage&gt;(EventName.PlantStageChange, (p, s) =&gt; { ... });
/// EventCenter.Subscribe&lt;Plant&gt;(EventName.PlantFullyGrown, p =&gt; { ... });
/// EventCenter.Subscribe&lt;Plant, GrowthStage&gt;(EventName.PlantFailed, (p, s) =&gt; { /* 游戏结束 */ });
/// </code>
/// </summary>
public class Plant : MonoBehaviour
{
    [Header("资源属性 (0~100)")]
    [Tooltip("当前水分，随时间消耗")]
    [Range(0f, 100f)] public float Water = 50f;

    [Tooltip("当前阳光，由外部环境设置（天气/道具）")]
    [Range(0f, 100f)] public float Sunlight = 50f;

    [Tooltip("当前养分，随时间消耗")]
    [Range(0f, 100f)] public float Nutrient = 50f;

    [Header("阶段需求（按 Seed / Sprout / Mature 顺序配置）")]
    [Tooltip("三个阶段的限时与资源门槛，Min 为 0 表示该阶段不需要这种资源")]
    public StageRequirement[] StageRequirements;

    [Header("消耗")]
    [Tooltip("每秒消耗的水分")]
    public float WaterConsumePerSec = 1.5f;

    [Tooltip("每秒消耗的养分")]
    public float NutrientConsumePerSec = 1f;

    [Header("状态")]
    [Tooltip("当前生长阶段")]
    public GrowthStage Stage = GrowthStage.Seed;

    /// <summary>当前阶段的生长进度（0~1），可用来驱动缩放动画</summary>
    public float StageProgress { get; private set; }

    /// <summary>是否已完全成熟（三个阶段全部长满）</summary>
    public bool IsFullyGrown { get; private set; }

    /// <summary>是否已死亡（某阶段超时失败）</summary>
    public bool IsDead { get; private set; }

    /// <summary>配置缺失时的兜底需求（不设门槛，限时 30 秒）</summary>
    private static readonly StageRequirement FallbackRequirement = new StageRequirement();

    /// <summary>当前阶段的需求配置</summary>
    public StageRequirement CurrentRequirement
    {
        get
        {
            if (StageRequirements == null || StageRequirements.Length <= (int)Stage)
                return FallbackRequirement;
            var req = StageRequirements[(int)Stage];
            return req != null ? req : FallbackRequirement;
        }
    }

    /// <summary>当前阶段剩余时间（秒）</summary>
    public float RemainingTime =>
        Mathf.Max(0f, CurrentRequirement.TimeLimit - _stageElapsed);

    /// <summary>水分是否满足当前阶段需求</summary>
    public bool WaterSatisfied => Water >= CurrentRequirement.MinWater;

    /// <summary>阳光是否满足当前阶段需求</summary>
    public bool SunlightSatisfied => Sunlight >= CurrentRequirement.MinSunlight;

    /// <summary>养分是否满足当前阶段需求</summary>
    public bool NutrientSatisfied => Nutrient >= CurrentRequirement.MinNutrient;

    /// <summary>当前阶段的三项需求是否全部满足（满足才会长进度）</summary>
    public bool RequirementsMet =>
        WaterSatisfied && SunlightSatisfied && NutrientSatisfied;

    /// <summary>缺什么资源（UI 提示用，都满足时返回空字符串）</summary>
    public string MissingResourceText
    {
        get
        {
            if (RequirementsMet) return string.Empty;
            if (!WaterSatisfied) return "缺水";
            if (!SunlightSatisfied) return "缺阳光";
            return "缺养分";
        }
    }

    /// <summary>阳光加速系数：0.5 ~ 1.5 倍，成长加速用</summary>
    private float SunlightFactor => Mathf.Lerp(0.5f, 1.5f, Sunlight / 100f);

    /// <summary>当前阶段已消耗的时间（秒）</summary>
    private float _stageElapsed;

    private void Awake()
    {
        EnsureRequirements();
    }

    /// <summary>Inspector 里 Reset 组件时恢复默认三阶段配置</summary>
    private void Reset()
    {
        EnsureRequirements();
    }

    private void Update()
    {
        // 完全成熟或已死亡：一切结算停止
        if (IsFullyGrown || IsDead) return;

        var req = CurrentRequirement;

        // 0. Buff 倍率（没挂 BuffSystem 时视为 1，不影响原逻辑）
        var buffs = BuffSystem.Instance;
        float growthMul = 1f, waterMul = 1f, nutrientMul = 1f;
        if (buffs != null)
        {
            growthMul = buffs.GrowthMultiplier;
            waterMul = buffs.WaterDrainMultiplier;
            nutrientMul = buffs.NutrientDrainMultiplier;
        }

        // 1. 阶段倒计时（固定时长，与生长进度无关）
        _stageElapsed += Time.deltaTime;

        // 2. 资源随时间消耗（消耗速率受 Buff 影响）
        Water = Mathf.Max(0f, Water - WaterConsumePerSec * waterMul * Time.deltaTime);
        Nutrient = Mathf.Max(0f, Nutrient - NutrientConsumePerSec * nutrientMul * Time.deltaTime);

        // 3. 只有满足当前阶段需求时才长进度
        //    阳光提供加速（最低 1 倍，保证达标后不会被阳光拖到超时），Buff 倍率再乘上去
        if (RequirementsMet)
        {
            float speed = Mathf.Max(1f, SunlightFactor) * growthMul;
            StageProgress += Time.deltaTime * speed / Mathf.Max(0.01f, req.TimeLimit);
        }

        // 4. 结算：先判成功，再判超时
        if (StageProgress >= 1f)
        {
            StageProgress = 1f;
            OnStageComplete();
        }
        else if (_stageElapsed >= req.TimeLimit)
        {
            FailCurrentStage();
        }
    }

    /// <summary>本阶段进度长满：进入下一阶段，或在成熟阶段完成生长</summary>
    private void OnStageComplete()
    {
        if (Stage == GrowthStage.Mature)
        {
            IsFullyGrown = true;
            Debug.Log($"[Plant] {name} 完全成熟！");
            EventCenter.Trigger(EventName.PlantFullyGrown, this);
            return;
        }

        Stage = (GrowthStage)((int)Stage + 1);
        StageProgress = 0f;
        _stageElapsed = 0f;

        Debug.Log($"[Plant] {name} 进入阶段：{Stage}");
        EventCenter.Trigger(EventName.PlantStageChange, this, Stage);
    }

    /// <summary>本阶段超时未达成需求：植物死亡（游戏结束信号）</summary>
    private void FailCurrentStage()
    {
        IsDead = true;
        string reason = MissingResourceText;
        Debug.LogWarning($"[Plant] {name} 在 {Stage} 阶段超时未能满足需求（{reason}），植物死亡");
        EventCenter.Trigger(EventName.PlantFailed, this, Stage);
    }

    /// <summary>
    /// 补齐三阶段默认配置：
    /// 种子（水 + 少量阳光）→ 发芽（水阳光 + 少量养分）→ 成熟（全要，养分需求上升）
    /// </summary>
    private void EnsureRequirements()
    {
        if (StageRequirements != null && StageRequirements.Length == 3)
            return;

        var defaults = new[]
        {
            new StageRequirement { TimeLimit = 30f, MinWater = 30f, MinSunlight = 10f, MinNutrient = 0f },   // 种子
            new StageRequirement { TimeLimit = 35f, MinWater = 40f, MinSunlight = 40f, MinNutrient = 15f },  // 发芽
            new StageRequirement { TimeLimit = 40f, MinWater = 50f, MinSunlight = 60f, MinNutrient = 40f },  // 成熟
        };

        // 已有配置但数量不对时，尽量保留玩家填过的部分
        if (StageRequirements != null)
        {
            for (int i = 0; i < StageRequirements.Length && i < 3; i++)
            {
                if (StageRequirements[i] != null)
                    defaults[i] = StageRequirements[i];
            }
        }

        StageRequirements = defaults;
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

    /// <summary>重置回种子阶段（收获后再种一轮 / 重开一局）</summary>
    public void ResetPlant()
    {
        Stage = GrowthStage.Seed;
        StageProgress = 0f;
        _stageElapsed = 0f;
        IsFullyGrown = false;
        IsDead = false;
        Water = 50f;
        Nutrient = 50f;
    }
}
