using System.Collections;
using Framwork;
using UnityEngine;

/// <summary>
/// 植物可视化组件：根据植物生长阶段自动切换 SpriteRenderer 的贴图。
///
/// 挂在与 Plant 同一个 GameObject（或其子物体）上，功能如下：
/// - 初始时按植物当前阶段显示对应贴图
/// - 阶段切换（PlantStageChange）→ 换到新阶段贴图 + 播放一次"生长弹跳"动画
/// - 完全成熟（PlantFullyGrown）→ 保持成熟贴图 + 播放一次庆祝脉冲
/// - 阶段失败（PlantFailed）→ 换成枯萎贴图（死掉的植物）
/// - 重新播种（PlantReset）→ 回到种子贴图
///
/// 贴图数据在 Inspector 上配置（数组下标即 GrowthStage 枚举值）：
///   stageSprites[0] = 种子 Seed
///   stageSprites[1] = 发芽 Sprout
///   stageSprites[2] = 成熟 Mature
///   deadSprite      = 枯萎（阶段失败时显示）
///
/// 完全依赖 Plant 触发的事件，无需手动调用任何方法；
/// 也支持调用 <see cref="Refresh"/> 主动刷新一次当前阶段贴图。
/// </summary>
[DisallowMultipleComponent]
public class PlantVisualChanger : MonoBehaviour
{
    [Header("引用（留空自动查找）")]
    [Tooltip("植物逻辑组件：留空时自动在自身/子物体上查找")]
    [SerializeField] private Plant plant;

    [Tooltip("要改贴图的 SpriteRenderer：留空时自动在自身/子物体上查找")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("阶段贴图（下标 = GrowthStage 枚举值）")]
    [Tooltip("种子阶段贴图")]
    [SerializeField] private Sprite seedSprite;

    [Tooltip("发芽阶段贴图")]
    [SerializeField] private Sprite sproutSprite;

    [Tooltip("成熟阶段贴图")]
    [SerializeField] private Sprite matureSprite;

    [Header("死亡 / 重置")]
    [Tooltip("枯萎贴图（阶段失败时显示）；不填则死亡时保持当前贴图")]
    [SerializeField] private Sprite deadSprite;

    [Header("动画")]
    [Tooltip("进入新阶段时是否播放一次生长弹跳动画")]
    [SerializeField] private bool popOnStageChange = true;

    [Tooltip("弹跳动画的缩放倍率（1.2 = 长到 1.2 倍再回弹）")]
    [SerializeField] private float popScale = 1.2f;

    [Tooltip("弹跳动画时长（秒）")]
    [SerializeField] private float popDuration = 0.18f;

    /// <summary>当前正在播放的弹跳动画协程（避免叠放）</summary>
    private Coroutine _popRoutine;

    private void Reset()
    {
        AutoFindReferences();
    }

    private void Awake()
    {
        AutoFindReferences();
    }

    private void OnEnable()
    {
        EventCenter.Subscribe<Plant, GrowthStage>(EventName.PlantStageChange, OnStageChange);
        EventCenter.Subscribe<Plant>(EventName.PlantFullyGrown, OnFullyGrown);
        EventCenter.Subscribe<Plant, GrowthStage>(EventName.PlantFailed, OnFailed);
        EventCenter.Subscribe<Plant>(EventName.PlantReset, OnReset);
    }

    private void OnDisable()
    {
        EventCenter.Unsubscribe<Plant, GrowthStage>(EventName.PlantStageChange, OnStageChange);
        EventCenter.Unsubscribe<Plant>(EventName.PlantFullyGrown, OnFullyGrown);
        EventCenter.Unsubscribe<Plant, GrowthStage>(EventName.PlantFailed, OnFailed);
        EventCenter.Unsubscribe<Plant>(EventName.PlantReset, OnReset);
    }

    /// <summary>组件注册完成后的兜底：立即显示植物当前阶段贴图</summary>
    private void Start()
    {
        Refresh();
    }

    // ==================== 对外接口 ====================

    /// <summary>
    /// 主动刷新一次，按植物当前阶段设置贴图。
    /// 通常在植物被脚本直接改 Stage（而非走事件流）后调用。
    /// </summary>
    public void Refresh()
    {
        if (!EnsureReferences()) return;

        // 已死亡 → 枯萎贴图优先
        if (plant.IsDead && deadSprite != null)
        {
            spriteRenderer.sprite = deadSprite;
            return;
        }

        var sp = GetStageSprite(plant.Stage);
        if (sp != null)
            spriteRenderer.sprite = sp;
    }

    // ==================== 事件回调 ====================

    private void OnStageChange(Plant p, GrowthStage newStage)
    {
        if (!IsMyPlant(p)) return;

        var sp = GetStageSprite(newStage);
        if (sp != null)
        {
            spriteRenderer.sprite = sp;
            if (popOnStageChange) PlayPopAnimation();
        }
    }

    private void OnFullyGrown(Plant p)
    {
        if (!IsMyPlant(p)) return;

        // 已处于成熟阶段，保持成熟贴图即可，额外播放一次"成熟庆祝"脉冲
        var sp = GetStageSprite(GrowthStage.Mature);
        if (sp != null) spriteRenderer.sprite = sp;
        PlayPopAnimation(1.08f, popDuration + 0.12f);
    }

    private void OnFailed(Plant p, GrowthStage failedStage)
    {
        if (!IsMyPlant(p)) return;

        if (deadSprite != null)
        {
            spriteRenderer.sprite = deadSprite;
            // 枯萎时不播弹跳，改为轻微下压表现"凋零"
            PlayWitherAnimation();
        }
    }

    private void OnReset(Plant p)
    {
        if (!IsMyPlant(p)) return;

        var sp = GetStageSprite(GrowthStage.Seed);
        if (sp != null)
        {
            spriteRenderer.sprite = sp;
            if (popOnStageChange) PlayPopAnimation();
        }
    }

    // ==================== 内部实现 ====================

    /// <summary>事件是否针对本植物（避免多株植物串台）</summary>
    private bool IsMyPlant(Plant p)
    {
        EnsureReferences();
        return p != null && p == plant;
    }

    /// <summary>按阶段取贴图，越界/未配置时返回 null</summary>
    private Sprite GetStageSprite(GrowthStage stage)
    {
        switch (stage)
        {
            case GrowthStage.Seed: return seedSprite;
            case GrowthStage.Sprout: return sproutSprite;
            case GrowthStage.Mature: return matureSprite;
            default: return null;
        }
    }

    /// <summary>查找未指定的 Plant / SpriteRenderer 引用</summary>
    private void AutoFindReferences()
    {
        if (plant == null) plant = GetComponent<Plant>();
        if (plant == null) plant = GetComponentInChildren<Plant>(true);

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    /// <summary>确保 Plant 与 SpriteRenderer 都可用，缺一不可</summary>
    private bool EnsureReferences()
    {
        AutoFindReferences();
        if (plant == null || spriteRenderer == null)
        {
            Debug.LogWarning($"[PlantVisualChanger] {name} 缺少 Plant 或 SpriteRenderer 引用，无法切换贴图", this);
            return false;
        }
        return true;
    }

    /// <summary>播放一次"生长弹跳"：放大到目标倍率再回弹到原始缩放</summary>
    private void PlayPopAnimation(float scale = -1f, float duration = -1f)
    {
        if (_popRoutine != null)
        {
            StopCoroutine(_popRoutine);
            _popRoutine = null;
        }

        _baseScale = transform.localScale;
        _popRoutine = StartCoroutine(PopRoutine(scale > 0f ? scale : popScale, duration > 0f ? duration : popDuration));
    }

    private Vector3 _baseScale;

    /// <summary>枯萎表现：轻微向下压扁并恢复（比弹跳更"垂头丧气"）</summary>
    private void PlayWitherAnimation()
    {
        if (_popRoutine != null)
        {
            StopCoroutine(_popRoutine);
            _popRoutine = null;
        }

        _baseScale = transform.localScale;
        _popRoutine = StartCoroutine(WitherRoutine());
    }

    private IEnumerator PopRoutine(float targetScale, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            // 归一化：0 → 1 → 0 的钟形曲线，峰值在中间
            float amount = Mathf.Sin(k * Mathf.PI);
            float s = Mathf.Lerp(1f, targetScale, amount);
            transform.localScale = _baseScale * s;
            yield return null;
        }

        transform.localScale = _baseScale;
        _popRoutine = null;
    }

    private IEnumerator WitherRoutine()
    {
        float duration = 0.35f;
        float t = 0f;
        // 先快速下压一点点，再缓慢回弹
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float squash = 1f - 0.12f * Mathf.Sin(k * Mathf.PI);
            transform.localScale = new Vector3(_baseScale.x * (1f + 0.06f * Mathf.Sin(k * Mathf.PI)),
                                               _baseScale.y * squash,
                                               _baseScale.z);
            yield return null;
        }

        transform.localScale = _baseScale;
        _popRoutine = null;
    }
}
