using System;
using System.Collections.Generic;
using Framwork;
using UnityEngine;

/// <summary>
/// 瓶子使用效果图标显示组件。
/// 挂在场景中自带 SpriteRenderer 的 Weather / Emotion 对象上，
/// 订阅全局事件 ElementUsed(Element, Plant)：
/// - 使用的元素属于本组件负责的类别（天气瓶 / 情绪瓶）时，
///   把 SpriteRenderer 的贴图换成该元素对应的效果图标；
/// - 图标保持 displayDuration 秒后，在 fadeOutDuration 秒内渐渐淡出消失；
/// - 淡出期间再次使用同类别的其他瓶子，会立即替换图标并重新计时。
///
/// 图标来源：只认 iconEntries 列表里给该元素单独配置的 Icon——
/// 这就是"每个元素对应效果图标"的集中配置处，在 Inspector 里填；
/// 没配置的元素不显示任何图标。
/// </summary>
public class BottleIconDisplay : MonoBehaviour
{
    /// <summary>瓶子类别：决定本组件响应哪些元素的使用事件</summary>
    public enum BottleCategory
    {
        /// <summary>天气瓶（特殊元素 P01~P21，ElementId 以 Element_P 开头）</summary>
        WeatherBottle = 0,

        /// <summary>情绪瓶（Emotion_ 开头，如 Emotion_Aunt；兼容旧版 Element_C 开头的 C01~C36）</summary>
        EmotionBottle = 1,

        /// <summary>所有元素都响应（包括基础元素，ElementId 不带 P/C 前缀的也算）</summary>
        All = 2,
    }

    /// <summary>单个元素的效果图标配置（iconEntries 列表的条目）</summary>
    [Serializable]
    public class IconEntry
    {
        [Tooltip("元素资产（从 Assets/Data/Element 拖入）")]
        public Element Element;

        [Tooltip("该元素被使用时显示的效果图标")]
        public Sprite Icon;

        [Tooltip("该元素专属的显示时长（秒，<=0 时使用组件全局 displayDuration）")]
        public float Duration;
    }

    [Header("类别（本对象显示哪类瓶子的图标）")]
    [Tooltip("天气瓶 = Element_P 开头的元素；情绪瓶 = Emotion_ 开头的元素（兼容旧版 Element_C）")]
    [SerializeField] private BottleCategory category = BottleCategory.WeatherBottle;

    [Header("元素效果图标表（每个元素对应的效果图标在这里配）")]
    [Tooltip("元素 → 效果图标 映射表；没配置的元素不显示图标")]
    [SerializeField] private List<IconEntry> iconEntries = new List<IconEntry>();

    [Header("显示时长（秒）")]
    [Tooltip("图标完全显示的保持时长（从使用那一刻起算）")]
    [SerializeField] private float displayDuration = 5f;

    [Tooltip("出现时的淡入时长（秒，0 = 立即出现）")]
    [SerializeField] private float fadeInDuration = 0.2f;

    [Tooltip("到时后的渐隐消失时长（秒，0 = 立即消失）")]
    [SerializeField] private float fadeOutDuration = 1f;

    [Header("其他")]
    [Tooltip("开始时是否隐藏图标（等第一次使用瓶子才显示）；关闭则保留场景里预设的贴图")]
    [SerializeField] private bool hideAtStart = true;

    [Tooltip("统一图标的显示尺寸（世界单位，取精灵长边缩放到该值；0 = 不缩放，按对象自身 Scale）")]
    [SerializeField] private float uniformWorldSize = 0f;

    /// <summary>当前正在显示的元素（没有显示中图标时为 null）</summary>
    public Element CurrentElement { get; private set; }

    private SpriteRenderer spriteRenderer;
    private Color baseColor = Color.white;
    private float elapsed;            // 当前图标已显示的秒数
    private float currentDuration;    // 当前图标总显示时长（含渐隐）
    private bool hasIcon;             // 是否处于"显示中（含淡出）"状态
    private Vector3 originalScale;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError($"[BottleIconDisplay] {name} 上没有 SpriteRenderer，图标无法显示");
            enabled = false;
            return;
        }

        baseColor = spriteRenderer.color;
        originalScale = transform.localScale;

        if (hideAtStart)
            spriteRenderer.sprite = null;
    }

    private void OnEnable()
    {
        EventCenter.Subscribe<Element, Plant>(EventName.ElementUsed, OnElementUsed);
    }

    private void OnDisable()
    {
        EventCenter.Unsubscribe<Element, Plant>(EventName.ElementUsed, OnElementUsed);
    }

    private void Update()
    {
        if (!hasIcon || spriteRenderer == null)
            return;

        elapsed += Time.deltaTime;

        // 有效淡入/淡出时长做保护，保证 "淡入 -> 保持 -> 渐隐" 三段时序合法
        float total = currentDuration <= 0f ? 0.01f : currentDuration;
        float fadeIn = Mathf.Clamp(fadeInDuration, 0f, total * 0.3f);
        float fadeOut = Mathf.Clamp(fadeOutDuration, 0f, total * 0.5f);

        if (elapsed >= total)
        {
            // 到点：图标彻底消失
            hasIcon = false;
            CurrentElement = null;
            spriteRenderer.sprite = null;
            transform.localScale = originalScale;
            return;
        }

        float alpha;
        if (elapsed < fadeIn)
            alpha = fadeIn > 0f ? elapsed / fadeIn : 1f;                       // 淡入
        else if (elapsed < total - fadeOut)
            alpha = 1f;                                                        // 保持
        else
            alpha = fadeOut > 0f ? 1f - (elapsed - (total - fadeOut)) / fadeOut : 1f; // 渐隐

        spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * Mathf.Clamp01(alpha));
    }

    /// <summary>
    /// ElementUsed 事件回调：元素属于本组件类别时显示对应图标。
    /// （不管是不是对植物使用——图标只关心"用了哪种瓶子"）
    /// </summary>
    private void OnElementUsed(Element element, Plant plant)
    {
        if (element == null || !BelongsToCategory(element))
            return;

        ShowIcon(element);
    }

    /// <summary>显示某元素的效果图标（替换当前图标并重新计时）；该元素没配置图标则隐藏</summary>
    public void ShowIcon(Element element)
    {
        if (spriteRenderer == null || element == null)
            return;

        Sprite icon = ResolveIcon(element, out float durationOverride);
        if (icon == null)
        {
            // 没在 iconEntries 里配置的元素：不显示任何图标
            hasIcon = false;
            CurrentElement = null;
            spriteRenderer.sprite = null;
            return;
        }

        CurrentElement = element;
        currentDuration = durationOverride > 0f ? durationOverride : displayDuration;
        elapsed = 0f;
        hasIcon = true;
        spriteRenderer.sprite = icon;

        // 可选：统一显示尺寸（不同精灵的像素尺寸往往不一致）
        if (uniformWorldSize > 0f)
        {
            Vector2 s = icon.bounds.size;
            float maxSide = Mathf.Max(s.x, s.y);
            if (maxSide > 0.0001f)
                transform.localScale = originalScale * (uniformWorldSize / maxSide);
        }
        else
        {
            transform.localScale = originalScale;
        }

        // 淡入从 0 开始（Update 会接着算）
        spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
    }

    /// <summary>判断元素是否属于本组件负责的类别（按 ElementId 前缀区分）</summary>
    private bool BelongsToCategory(Element element)
    {
        switch (category)
        {
            case BottleCategory.WeatherBottle: return IsWeatherBottle(element);
            case BottleCategory.EmotionBottle: return IsEmotionBottle(element);
            default: return true; // All
        }
    }

    /// <summary>解析元素对应的效果图标：只在 iconEntries 里找（没配置返回 null）；顺带取出专属时长</summary>
    private Sprite ResolveIcon(Element element, out float durationOverride)
    {
        durationOverride = 0f;

        foreach (var entry in iconEntries)
        {
            if (entry == null || entry.Element == null || entry.Icon == null)
                continue;

            if (entry.Element == element)
            {
                durationOverride = entry.Duration;
                return entry.Icon;
            }
        }

        return null; // 没配置：不显示
    }

    /// <summary>天气瓶：ElementId 以 Element_P 开头（P01~P21 特殊元素）</summary>
    private static bool IsWeatherBottle(Element element)
        => GetId(element).StartsWith("Element_P", StringComparison.OrdinalIgnoreCase);

    /// <summary>情绪瓶：Emotion_ 开头（如 Emotion_Aunt）或旧的 Element_C 开头（C01~C36）</summary>
    private static bool IsEmotionBottle(Element element)
    {
        string id = GetId(element);
        return id.StartsWith("Emotion_", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("Element_C", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>元素的判别 ID：优先 ElementId，为空时用资产名</summary>
    private static string GetId(Element element)
    {
        if (!string.IsNullOrEmpty(element.ElementId))
            return element.ElementId;
        return element.name;
    }
}
