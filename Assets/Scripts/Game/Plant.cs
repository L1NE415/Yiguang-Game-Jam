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
    /// <summary>未确定：第二阶段水分没落进过任何区间，第三阶段用默认成熟贴图</summary>
    None = 0,
    /// <summary>绿萝：水分区间 50~66.67（P04）</summary>
    Pothos = 1,
    /// <summary>仙人掌：水分区间 0~16.67（P01）</summary>
    Cactus = 2,
    /// <summary>捕蝇草：水分区间 83.33~100（P06）</summary>
    Flytrap = 3,
    /// <summary>多肉植物：水分区间 16.67~33.33（P02）</summary>
    Succulent = 4,
    /// <summary>薰衣草：水分区间 33.33~50（P03）</summary>
    Lavender = 5,
    /// <summary>龟背竹：水分区间 66.67~83.33（P05）</summary>
    Monstera = 6,
}

/// <summary>
/// 最终形态判定条件（简化版）：
/// 前两个阶段（种子 / 发芽）期间，水分落在 [MinWater, MaxWater] 区间的每一帧都累计时长；
/// 进入第三阶段时，累计时长最长的一条对应的形态胜出（即把水分大部分时间压在哪个区间，就长成哪种）。
/// 阳光 / 养分暂时不参与判定（字段保留，方便以后恢复）。见 Plant.LockFinalForm。
/// </summary>
[Serializable]
public class FinalFormRule
{
    [Tooltip("满足本条件后第三阶段长成的形态")]
    public PlantFinalForm Form = PlantFinalForm.None;

    [Header("水分区间（判定只看这个）")]
    [Tooltip("水分区间下限：第二阶段水分达到过该值以上，第三阶段就长成 Form 指定的植物")]
    public float MinWater = 0f;

    [Tooltip("水分区间上限：第二阶段水分达到过该值以下，第三阶段就长成 Form 指定的植物")]
    public float MaxWater = 100f;

    [Header("阳光区间（暂不参与判定）")]
    [Tooltip("阳光区间下限（当前版本不参与判定，保留备用）")]
    public float MinSunlight = 0f;

    [Tooltip("阳光区间上限（当前版本不参与判定，保留备用）")]
    public float MaxSunlight = 100f;

    [Header("养分区间（暂不参与判定）")]
    [Tooltip("养分区间下限（当前版本不参与判定，保留备用）")]
    public float MinNutrient = 0f;

    [Tooltip("养分区间上限（当前版本不参与判定，保留备用）")]
    public float MaxNutrient = 100f;

    /// <summary>本条条件在前面阶段累计停留的时长（秒）；进第三阶段时取累计最长者。运行时数据，不序列化</summary>
    [NonSerialized] public float Accumulated;
}

/// <summary>
/// 植物类：挂载到植物 GameObject 上使用。
///
/// 三个资源：
/// - 水分 Water：随时间消耗，浇水（WaterPlant）补充
/// - 阳光 Sunlight：随时间自然衰减（SunlightConsumePerSec），由外部环境（天气/道具）补光维持，既是需求门槛也是生长加速项
/// - 养分 Nutrient：随时间消耗，施肥（Fertilize）补充
///
/// 三个阶段的资源需求（在 Inspector 的 StageRequirements 里按顺序配置）：
/// - 种子 Seed：需要水 + 少量阳光，不需要养分
/// - 发芽 Sprout：需要阳光 + 水 + 少量养分；同时进行最终形态判定（见 FinalFormRules）
/// - 成熟 Mature：默认不需要任何资源（不消耗、不看门槛、不会超时死亡）
///
/// 最终形态判定（简化版）：
/// - 前两个阶段（种子 / 发芽）期间，水分落在某条规则的 [MinWater, MaxWater] 区间的每一帧都累计时长
///   （阳光 / 养分暂不参与判定）
/// - 进入第三阶段时统一裁决：取累计停留时长最长的一条对应的形态胜出
///   （你大部分时间把水分压在哪个区间，就长成哪种）；一条都没累计到则兜底绿萝
/// - 默认规则（水分 0~100 均分 6 段，每段约 16.67；阳光/养分暂不参与）：
///   仙人掌 0~16.67 / 多肉植物 16.67~33.33 / 薰衣草 33.33~50 / 绿萝 50~66.67 / 龟背竹 66.67~83.33 / 捕蝇草 83.33~100
/// - 第三阶段进入时按锁定形态显示对应外观（PlantVisualChanger 负责换贴图）
///
/// 生长规则（简化版）：
/// - 植物随时间自己长大：阶段进度 = 已过时间 / 阶段限时，与属性达标与否无关
/// - 每个阶段结束时才做一次判定：看【这一刻】三项需求是否达标
///   达标 → 进入下一阶段；不达标 → 植物死亡，广播 PlantFailed
/// - 第三阶段不需要资源，长满即完全成熟（广播 PlantFullyGrown），不会死亡
/// - 中途属性不达标不会让进度条停住，只要在阶段结束的那一刻把数值补上来就行
///
/// Buff 接入：BuffSystem（天气/元素事件触发）提供倍率——
/// 生长倍率影响阶段推进速度，水 / 养分消耗倍率影响资源下降速度；
/// 场景里没挂 BuffSystem 时按倍率 1 正常生长。
///
/// 事件（均在 Framwork.EventName 中）：
/// - PlantStageChange(Plant, GrowthStage)      进入新的生长阶段
/// - PlantFullyGrown(Plant)                    三个阶段全部完成，完全成熟
/// - PlantFailed(Plant, GrowthStage)           某阶段超时未达成需求，植物死亡（游戏结束信号）
/// - PlantFinalFormDetermined(Plant, PlantFinalForm)  进入第三阶段时锁定了最终形态
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
    [Tooltip("第二阶段期间，水分达到过某条规则的水分区间，第三阶段就长成该形态（当前只看水分）")]
    public FinalFormRule[] FinalFormRules;

    [Tooltip("判定所需的连续保持秒数：当前简化判定只看水分是否达到过，本字段暂不使用（保留备用）")]
    public float FinalFormHoldSeconds = 10f;

    [Header("消耗")]
    [Tooltip("每秒消耗的水分")]
    public float WaterConsumePerSec = 1.5f;

    [Tooltip("每秒消耗的养分")]
    public float NutrientConsumePerSec = 1f;

    [Tooltip("每秒消耗的阳光（与水分/养分一样随时间自然衰减，需要外部环境补光维持）")]
    public float SunlightConsumePerSec = 1f;

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
    /// 第三阶段的外观由它决定；第二阶段水分达到过某条规则区间即锁定，锁定后不再变化。
    /// </summary>
    public PlantFinalForm FinalForm { get; private set; } = PlantFinalForm.None;

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

    /// <summary>当前阶段已消耗的时间（秒）：植物随时间自己长大，进度 = 已过时间 / 阶段限时</summary>
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

        // 第三阶段默认不需要资源：不消耗、不做死亡判定（勾选 MatureRequiresResources 可恢复）
        bool matureNeedsNothing = Stage == GrowthStage.Mature && !MatureRequiresResources;

        // 1. 前两个阶段（种子 / 发芽）：记录水分达到过哪些形态条件，见 TrackFinalForm
        if (Stage != GrowthStage.Mature)
            TrackFinalForm();

        // 2. 阶段结束判定用的达标快照：取本帧资源消耗【之前】的状态。
        //    避免阶段刚好在某一帧结束时，被这一帧的消耗把压线属性扣到门槛下造成误判死亡
        bool satisfiedThisFrame = RequirementsMet;

        // 3. 资源随时间消耗（水/养分消耗速率受 Buff 影响；阳光按固定速率自然衰减；第三阶段不消耗）
        if (!matureNeedsNothing)
        {
            Water = Mathf.Max(0f, Water - WaterConsumePerSec * waterMul * Time.deltaTime);
            Nutrient = Mathf.Max(0f, Nutrient - NutrientConsumePerSec * nutrientMul * Time.deltaTime);
            Sunlight = Mathf.Max(0f, Sunlight - SunlightConsumePerSec * Time.deltaTime);
        }

        // 4. 植物随时间自己长大：阶段进度 = 已过时间 / 阶段限时，与属性达标与否无关。
        //    Buff 生长倍率影响推进速度（保底 0.05 倍，避免减速 Buff 让它永远长不大）
        _stageElapsed += Time.deltaTime * Mathf.Max(0.05f, growthMul);
        StageProgress = Mathf.Clamp01(_stageElapsed / Mathf.Max(0.01f, req.TimeLimit));

        // 5. 阶段结束才结算：看这一刻的三项数值是否达标
        //    达标 → 进入下一阶段；不达标 → 植物死亡（第三阶段不判死，长满即完全成熟）
        if (StageProgress >= 1f)
        {
            if (matureNeedsNothing || satisfiedThisFrame || RequirementsMet)
                OnStageComplete();
            else
                FailCurrentStage();
        }
    }

    /// <summary>
    /// 最终形态条件累计（前两个阶段每帧调用）：
    /// 只看水分——水分落在某条规则的 [MinWater, MaxWater] 区间，就给该条累加本帧时长。
    /// 这里只累计不锁定，最终形态在【进入第三阶段时】取累计时长最长的一条（见 LockFinalForm）。
    /// 阳光 / 养分暂不参与判定（字段保留，方便以后恢复）。
    /// </summary>
    private void TrackFinalForm()
    {
        if (FinalForm != PlantFinalForm.None || FinalFormRules == null) return;

        foreach (var rule in FinalFormRules)
        {
            if (rule == null || rule.Form == PlantFinalForm.None) continue;

            if (Water >= rule.MinWater && Water <= rule.MaxWater)
                rule.Accumulated += Time.deltaTime;
        }
    }

    /// <summary>
    /// 进入第三阶段时按累计停留时长锁定最终形态：
    /// 在前面阶段累计时长最长的一条规则对应的形态胜出（即你把水分大部分时间压在哪个区间，就长成哪种）；
    /// 一条都没累计到（水分从没落进过任何区间）则兜底绿萝。
    /// 锁定后广播 PlantFinalFormDetermined，保证第三阶段一定有确定形态。
    /// </summary>
    private void LockFinalForm()
    {
        if (FinalForm != PlantFinalForm.None) return;

        PlantFinalForm result = PlantFinalForm.Pothos;
        float best = 0f;
        bool anyAccumulated = false;

        if (FinalFormRules != null)
        {
            foreach (var rule in FinalFormRules)
            {
                if (rule == null || rule.Accumulated <= best) continue;
                best = rule.Accumulated;
                result = rule.Form;
                anyAccumulated = true;
            }
        }

        FinalForm = result;
        Debug.Log(anyAccumulated
            ? $"[Plant] {name} 按水分累计停留时长最长锁定最终形态：{FinalForm}（{best:F1}s）"
            : $"[Plant] {name} 前面阶段水分从未落进任何形态区间，默认最终形态：{FinalForm}");
        EventCenter.Trigger(EventName.PlantFinalFormDetermined, this, FinalForm);
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

        // 进入第三阶段：按水分累计停留时长最长裁决最终形态（兜底绿萝），
        // 保证第三阶段一定有确定形态
        if (Stage == GrowthStage.Mature)
            LockFinalForm();

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
    /// 补齐最终形态判定默认规则（场景旧组件数组为空时填充）。
    /// 判定只看水分，6 条规则的水分区间无缝覆盖 0~100（每段约 16.67），
    /// 谁累计停留最久谁胜出：仙人掌 / 多肉植物 / 薰衣草 / 绿萝 / 龟背竹 / 捕蝇草。
    /// </summary>
    private void EnsureFinalFormRules()
    {
        if (FinalFormRules != null && FinalFormRules.Length > 0)
            return;

        FinalFormRules = new[]
        {
            // 水分区间按用户表 6 等分（每段约 16.67），阳光/养分不参与判定
            new FinalFormRule { Form = PlantFinalForm.Cactus,    MinWater = 0f,    MaxWater = 16.67f, MinSunlight = 0f, MaxSunlight = 100f, MinNutrient = 0f, MaxNutrient = 100f },
            new FinalFormRule { Form = PlantFinalForm.Succulent, MinWater = 16.67f, MaxWater = 33.33f, MinSunlight = 0f, MaxSunlight = 100f, MinNutrient = 0f, MaxNutrient = 100f },
            new FinalFormRule { Form = PlantFinalForm.Lavender,  MinWater = 33.33f, MaxWater = 50f,    MinSunlight = 0f, MaxSunlight = 100f, MinNutrient = 0f, MaxNutrient = 100f },
            new FinalFormRule { Form = PlantFinalForm.Pothos,    MinWater = 50f,    MaxWater = 66.67f, MinSunlight = 0f, MaxSunlight = 100f, MinNutrient = 0f, MaxNutrient = 100f },
            new FinalFormRule { Form = PlantFinalForm.Monstera,  MinWater = 66.67f, MaxWater = 83.33f, MinSunlight = 0f, MaxSunlight = 100f, MinNutrient = 0f, MaxNutrient = 100f },
            new FinalFormRule { Form = PlantFinalForm.Flytrap,   MinWater = 83.33f, MaxWater = 100f,   MinSunlight = 0f, MaxSunlight = 100f, MinNutrient = 0f, MaxNutrient = 100f },
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

        // 最终形态判定一并重置（下一轮重新累计、重新裁决）
        FinalForm = PlantFinalForm.None;
        if (FinalFormRules != null)
        {
            foreach (var rule in FinalFormRules)
            {
                if (rule != null) rule.Accumulated = 0f;
            }
        }

        EventCenter.Trigger(EventName.PlantReset, this);
    }
}
