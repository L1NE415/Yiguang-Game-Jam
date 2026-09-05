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

/// <summary>植物最终形态（第三阶段长成什么样子，由第二阶段的属性区间决定）</summary>
public enum PlantFinalForm
{
    /// <summary>未确定：第二阶段没有任何条件保持超过判定时长，第三阶段用默认成熟贴图</summary>
    None = 0,
    /// <summary>绿萝：水 12~25、阳光 12~25、养分 12~25</summary>
    Pothos = 1,
    /// <summary>仙人掌：水 1~6、阳光 20~40、养分 12~25</summary>
    Cactus = 2,
    /// <summary>捕蝇草：水 12~25、阳光 12~25、养分 26~45</summary>
    Flytrap = 3,
}

/// <summary>
/// 最终形态判定条件：
/// 第二阶段（发芽）期间，水分 / 阳光 / 养分三项同时维持在 [Min, Max] 区间内
/// 连续超过 Plant.FinalFormHoldSeconds 秒，第三阶段就会长成 Form 指定的植物。
/// 一旦某项属性离开区间，该条件的连续保持时长立即清零重计。
/// </summary>
[Serializable]
public class FinalFormRule
{
    [Tooltip("满足本条件后第三阶段长成的形态")]
    public PlantFinalForm Form = PlantFinalForm.None;

    [Header("水分区间")]
    [Tooltip("水分区间下限")]
    public float MinWater = 0f;

    [Tooltip("水分区间上限")]
    public float MaxWater = 100f;

    [Header("阳光区间")]
    [Tooltip("阳光区间下限")]
    public float MinSunlight = 0f;

    [Tooltip("阳光区间上限")]
    public float MaxSunlight = 100f;

    [Header("养分区间")]
    [Tooltip("养分区间下限")]
    public float MinNutrient = 0f;

    [Tooltip("养分区间上限")]
    public float MaxNutrient = 100f;

    /// <summary>本条件已连续满足的时长（运行时数据，不序列化）</summary>
    [NonSerialized] public float HoldElapsed;
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
/// - 发芽 Sprout：需要阳光 + 水 + 少量养分；同时进行最终形态判定（见 FinalFormRules）
/// - 成熟 Mature：默认不需要任何资源（不消耗、不看门槛、不会超时死亡）
///
/// 最终形态判定（第二阶段进行）：
/// - 第二阶段期间，水 / 阳光 / 养分三项同时维持在 FinalFormRules 某条规则的区间内
///   连续超过 FinalFormHoldSeconds（默认 10 秒），最终形态即锁定为该规则的 Form
/// - 离开区间就清零重计，先满足先锁定，锁定后不再判定
/// - 默认规则：绿萝（全 12~25）/ 仙人掌（水 1~6、阳光 20~40、养分 12~25）
///   / 捕蝇草（水 12~25、阳光 12~25、养分 26~45）
/// - 兜底：第二阶段结束时三个条件都没锁定成功 → 默认长成绿萝
/// - 第三阶段进入时按锁定形态显示对应外观（PlantVisualChanger 负责换贴图）
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
/// - PlantStageChange(Plant, GrowthStage)      进入新的生长阶段
/// - PlantFullyGrown(Plant)                    三个阶段全部完成，完全成熟
/// - PlantFailed(Plant, GrowthStage)           某阶段超时未达成需求，植物死亡（游戏结束信号）
/// - PlantFinalFormDetermined(Plant, PlantFinalForm)  第二阶段锁定了最终形态
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

    [Header("第三阶段（成熟）")]
    [Tooltip("进入第三阶段后是否仍需要资源：默认不勾选 = 第三阶段不需要任何资源（不消耗、不看门槛、不会超时死亡）；勾选 = 恢复旧行为")]
    public bool MatureRequiresResources = false;

    [Header("最终形态判定（第二阶段）")]
    [Tooltip("第二阶段期间，水/阳光/养分三项同时维持在区间内连续超过 FinalFormHoldSeconds 秒，第三阶段就长成该形态")]
    public FinalFormRule[] FinalFormRules;

    [Tooltip("判定所需的连续保持秒数（<= 0 时按 10 秒处理）")]
    public float FinalFormHoldSeconds = 10f;

    [Header("消耗")]
    [Tooltip("每秒消耗的水分")]
    public float WaterConsumePerSec = 1.5f;

    [Tooltip("每秒消耗的养分")]
    public float NutrientConsumePerSec = 1f;

    [Header("状态")]
    [Tooltip("当前生长阶段")]
    public GrowthStage Stage = GrowthStage.Seed;

    [Header("元素使用检测")]
    [Tooltip("元素使用的检测半径（世界单位）：拖动元素图标松手时，鼠标世界位置进入该半径即视为对本植物使用该元素。<= 0 时按默认值 2.5 处理")]
    [SerializeField] private float useDetectRadius = 2.5f;

    /// <summary>
    /// 有效检测半径。
    /// 注意：场景里已存在的 Plant 组件反序列化新增字段会得到 0（不会用 C# 初始值），
    /// 因此这里做 0 值回退，保证旧组件无需手动 Reset 也能用默认半径。
    /// </summary>
    public float UseDetectRadius => useDetectRadius > 0f ? useDetectRadius : 2.5f;

    /// <summary>当前阶段的生长进度（0~1），可用来驱动缩放动画</summary>
    public float StageProgress { get; private set; }

    /// <summary>是否已完全成熟（三个阶段全部长满）</summary>
    public bool IsFullyGrown { get; private set; }

    /// <summary>是否已死亡（某阶段超时失败）</summary>
    public bool IsDead { get; private set; }

    /// <summary>
    /// 第二阶段判定的最终形态（未确定为 None）。
    /// 第三阶段的外观由它决定；在第二阶段某条件连续保持超时即锁定，锁定后不再变化。
    /// </summary>
    public PlantFinalForm FinalForm { get; private set; } = PlantFinalForm.None;

    /// <summary>有效判定时长（场景里旧组件反序列化新增字段会得到 0，这里回退 10 秒）</summary>
    public float EffectiveFinalFormHoldSeconds =>
        FinalFormHoldSeconds > 0f ? FinalFormHoldSeconds : 10f;

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
        EnsureFinalFormRules();
    }

    /// <summary>Inspector 里 Reset 组件时恢复默认三阶段配置</summary>
    private void Reset()
    {
        EnsureRequirements();
        EnsureFinalFormRules();
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

        // 1. 第二阶段：最终形态判定（三项属性同时维持在区间内持续计时）
        if (Stage == GrowthStage.Sprout)
            TrackFinalForm(Time.deltaTime);

        // 第三阶段默认不需要资源：不倒计时、不消耗、不看门槛、不会超时死亡
        // （勾选 MatureRequiresResources 可恢复旧行为）
        bool matureNeedsNothing = Stage == GrowthStage.Mature && !MatureRequiresResources;

        // 2. 阶段倒计时（固定时长，与生长进度无关；第三阶段不倒计时）
        if (!matureNeedsNothing)
            _stageElapsed += Time.deltaTime;

        // 3. 资源随时间消耗（消耗速率受 Buff 影响；第三阶段不消耗）
        if (!matureNeedsNothing)
        {
            Water = Mathf.Max(0f, Water - WaterConsumePerSec * waterMul * Time.deltaTime);
            Nutrient = Mathf.Max(0f, Nutrient - NutrientConsumePerSec * nutrientMul * Time.deltaTime);
        }

        // 4. 只有满足当前阶段需求时才长进度
        //    阳光提供加速（最低 1 倍，保证达标后不会被阳光拖到超时），Buff 倍率再乘上去
        //    第三阶段不需要资源：无条件生长，仅受 Buff 生长倍率影响
        if (matureNeedsNothing || RequirementsMet)
        {
            float speed = matureNeedsNothing ? growthMul : Mathf.Max(1f, SunlightFactor) * growthMul;
            StageProgress += Time.deltaTime * speed / Mathf.Max(0.01f, req.TimeLimit);
        }

        // 5. 结算：先判成功，再判超时（第三阶段永不超时死亡）
        if (StageProgress >= 1f)
        {
            StageProgress = 1f;
            OnStageComplete();
        }
        else if (!matureNeedsNothing && _stageElapsed >= req.TimeLimit)
        {
            FailCurrentStage();
        }
    }

    /// <summary>
    /// 第二阶段（发芽）最终形态判定：
    /// 三项属性同时落在某条规则的 [Min, Max] 区间内就为该规则累计连续保持时长，
    /// 一旦任一属性离开区间，该规则的累计时长立即清零；
    /// 累计超过 EffectiveFinalFormHoldSeconds 即锁定最终形态并广播事件。
    /// 先满足哪条锁哪条，锁定后不再判定。
    /// </summary>
    private void TrackFinalForm(float deltaTime)
    {
        if (FinalForm != PlantFinalForm.None || FinalFormRules == null) return;

        foreach (var rule in FinalFormRules)
        {
            if (rule == null || rule.Form == PlantFinalForm.None) continue;

            bool inRange =
                Water >= rule.MinWater && Water <= rule.MaxWater &&
                Sunlight >= rule.MinSunlight && Sunlight <= rule.MaxSunlight &&
                Nutrient >= rule.MinNutrient && Nutrient <= rule.MaxNutrient;

            rule.HoldElapsed = inRange ? rule.HoldElapsed + deltaTime : 0f;

            if (rule.HoldElapsed >= EffectiveFinalFormHoldSeconds)
            {
                FinalForm = rule.Form;
                Debug.Log($"[Plant] {name} 第二阶段属性维持 {EffectiveFinalFormHoldSeconds:F1} 秒，最终形态锁定为：{FinalForm}");
                EventCenter.Trigger(EventName.PlantFinalFormDetermined, this, FinalForm);
                return;
            }
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

        // 第二阶段结束时最终形态仍未锁定（三个区间都没保持够时长）：
        // 默认兜底为绿萝，保证第三阶段一定有确定的最终形态
        if (Stage == GrowthStage.Mature && FinalForm == PlantFinalForm.None)
        {
            FinalForm = PlantFinalForm.Pothos;
            Debug.Log($"[Plant] {name} 第二阶段未达成任何形态条件，默认最终形态：{FinalForm}");
            EventCenter.Trigger(EventName.PlantFinalFormDetermined, this, FinalForm);
        }

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
    /// 种子（水 + 少量阳光）→ 发芽（水阳光 + 少量养分）→ 成熟（默认不需要资源）
    /// </summary>
    private void EnsureRequirements()
    {
        if (StageRequirements != null && StageRequirements.Length == 3)
            return;

        var defaults = new[]
        {
            new StageRequirement { TimeLimit = 30f, MinWater = 30f, MinSunlight = 10f, MinNutrient = 0f },   // 种子
            new StageRequirement { TimeLimit = 35f, MinWater = 40f, MinSunlight = 40f, MinNutrient = 15f },  // 发芽
            new StageRequirement { TimeLimit = 40f, MinWater = 0f, MinSunlight = 0f, MinNutrient = 0f },     // 成熟（不需要资源）
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

    /// <summary>
    /// 补齐最终形态判定默认规则（场景旧组件数组为空时填充）：
    /// - 绿萝：水 12~25、阳光 12~25、养分 12~25
    /// - 仙人掌：水 1~6、阳光 20~40、养分 12~25
    /// - 捕蝇草：水 12~25、阳光 12~25、养分 26~45
    /// </summary>
    private void EnsureFinalFormRules()
    {
        if (FinalFormRules != null && FinalFormRules.Length > 0)
            return;

        FinalFormRules = new[]
        {
            new FinalFormRule { Form = PlantFinalForm.Pothos,  MinWater = 12f, MaxWater = 25f, MinSunlight = 12f, MaxSunlight = 25f, MinNutrient = 12f, MaxNutrient = 25f },
            new FinalFormRule { Form = PlantFinalForm.Cactus,  MinWater = 1f,  MaxWater = 6f,  MinSunlight = 20f, MaxSunlight = 40f, MinNutrient = 12f, MaxNutrient = 25f },
            new FinalFormRule { Form = PlantFinalForm.Flytrap, MinWater = 12f, MaxWater = 25f, MinSunlight = 12f, MaxSunlight = 25f, MinNutrient = 26f, MaxNutrient = 45f },
        };
    }

    // ==================== 元素使用检测 ====================

    /// <summary>
    /// 判断屏幕坐标是否落在本植物的检测范围内（检测半径 UseDetectRadius）。
    /// 用于"拖动元素图标到植物上松手"的命中判断。
    /// </summary>
    public bool IsScreenPointInRange(Vector2 screenPos)
    {
        var cam = Camera.main;
        if (cam == null) return false;

        // 屏幕坐标 → 世界坐标（正交相机；z 传相机到 z=0 平面的距离，保证落在游戏平面上）
        var worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
        return Vector2.Distance(transform.position, worldPos) <= UseDetectRadius;
    }

    /// <summary>
    /// 找到屏幕坐标命中的植物（检测范围内距离最近的一个），没有则返回 null。
    /// 场景里有多株植物时取最近；只有一株时即"鼠标在它附近松手"。
    /// </summary>
    public static Plant GetPlantUnderPointer(Vector2 screenPos)
    {
        var cam = Camera.main;
        if (cam == null) return null;

        var worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));

        Plant best = null;
        float bestDist = float.MaxValue;
        foreach (var plant in FindObjectsByType<Plant>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            // 死亡/已成熟的植物不再接受元素使用
            if (plant.IsDead || plant.IsFullyGrown) continue;

            float d = Vector2.Distance(plant.transform.position, worldPos);
            if (d <= plant.UseDetectRadius && d < bestDist)
            {
                best = plant;
                bestDist = d;
            }
        }
        return best;
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

    /// <summary>
    /// 应用元素/天气效果：水分、阳光、养分三项同时增减（各自 clamp 到 0~100）。
    /// 与 WaterPlant/Fertilize/AddSunlight 不同，本方法支持负数（削弱），
    /// 是元素被使用事件（ElementUseEffectLibrary）作用到植物的统一入口。
    /// </summary>
    public void ApplyWeatherEffect(float waterDelta, float sunlightDelta, float nutrientDelta)
    {
        Water = Mathf.Clamp(Water + waterDelta, 0f, 100f);
        Sunlight = Mathf.Clamp(Sunlight + sunlightDelta, 0f, 100f);
        Nutrient = Mathf.Clamp(Nutrient + nutrientDelta, 0f, 100f);
    }

#if UNITY_EDITOR
    /// <summary>选中植物时在 Scene 视图画出元素使用检测范围（青色圆圈）</summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, UseDetectRadius);
    }
#endif

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

        // 最终形态判定一并重置（下一轮重新判定）
        FinalForm = PlantFinalForm.None;
        if (FinalFormRules != null)
        {
            foreach (var rule in FinalFormRules)
            {
                if (rule != null) rule.HoldElapsed = 0f;
            }
        }

        EventCenter.Trigger(EventName.PlantReset, this);
    }
}
