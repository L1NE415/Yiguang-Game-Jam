using System.Collections;
using Framwork;
using TMPro;
using UnityEngine;

/// <summary>
/// 植物数值变化指示器：使用瓶子（元素）后，在三个 TMP 文本上显示水分 / 阳光 / 养分的增减量。
///
/// 工作方式（全事件驱动，与项目范式一致）：
/// - 订阅全局事件 ElementUsed(Element, Plant)——拖动瓶子到植物上松手时由 DragHandle 触发
/// - 收到事件后用 element.UseEventName 到 ElementUseEffectLibrary 反查效果表，
///   拿到与本局实际生效完全相同的三项增减数值（UI 与逻辑永不脱节，无需重复配置）
/// - 三个文本分别显示 "+6" / "-2"，增加绿色、减少红色、无变化灰色
/// - 停留 DisplaySeconds 秒后按 FadeSeconds 淡出隐藏；连续使用瓶子会重置计时
///
/// 挂载：挂在指示文本的父物体（如 UI 2 场景 Canvas 下的 TexIndicator）上，
/// 把三个 TMP 文本拖入 Water Text / Sunlight Text / Nutrient Text 即可。
/// </summary>
public class ElementEffectIndicatorUI : MonoBehaviour
{
    [Header("指示文本（TMP，按资源拖入）")]
    [Tooltip("水分变化指示文本")]
    [SerializeField] private TMP_Text waterText;

    [Tooltip("阳光变化指示文本")]
    [SerializeField] private TMP_Text sunlightText;

    [Tooltip("养分变化指示文本")]
    [SerializeField] private TMP_Text nutrientText;

    [Header("显示设置")]
    [Tooltip("数值停留时长（秒），到时后开始淡出")]
    [SerializeField] private float displaySeconds = 2f;

    [Tooltip("淡出时长（秒），0 = 立即隐藏")]
    [SerializeField] private float fadeSeconds = 0.4f;

    [Tooltip("数值增加时的颜色")]
    [SerializeField] private Color gainColor = new Color(0.30f, 0.85f, 0.35f, 1f);

    [Tooltip("数值减少时的颜色")]
    [SerializeField] private Color loseColor = new Color(0.92f, 0.32f, 0.26f, 1f);

    [Tooltip("数值无变化（0）时的颜色")]
    [SerializeField] private Color zeroColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    /// <summary>淡出协程句柄（连续使用时重置计时用）</summary>
    private Coroutine hideRoutine;

    private void OnEnable()
    {
        EventCenter.Subscribe<Element, Plant>(EventName.ElementUsed, OnElementUsed);
        HideImmediate();
    }

    private void OnDisable()
    {
        EventCenter.Unsubscribe<Element, Plant>(EventName.ElementUsed, OnElementUsed);
    }

    /// <summary>
    /// ElementUsed 事件回调：反查效果表并刷新三个指示文本。
    /// effect 查不到（元素没配 UseEventName）或没有目标植物时跳过——与 ElementUseEffectLibrary 的跳过条件保持一致。
    /// </summary>
    private void OnElementUsed(Element element, Plant plant)
    {
        if (plant == null) return;

        var effect = ElementUseEffectLibrary.GetEffect(element != null ? element.UseEventName : null);
        if (effect == null) return;

        Show(waterText, effect.Water);
        Show(sunlightText, effect.Sunlight);
        Show(nutrientText, effect.Nutrient);

        // 重置淡出计时：连续使用瓶子时以最后一次为准
        if (hideRoutine != null)
            StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(HideRoutine());
    }

    /// <summary>按增减量刷新单个文本：格式 +6 / -2 / +0，颜色按增/减/零区分</summary>
    private void Show(TMP_Text text, float delta)
    {
        if (text == null) return;

        text.text = FormatDelta(delta);
        text.color = delta > 0f ? gainColor : delta < 0f ? loseColor : zeroColor;
        SetAlpha(text, 1f);
        if (!text.gameObject.activeSelf)
            text.gameObject.SetActive(true);
    }

    /// <summary>增减量统一格式：正数带 +，负数自带 -，零显示 +0</summary>
    private static string FormatDelta(float delta)
    {
        float rounded = Mathf.Round(delta);
        return rounded > 0f ? $"+{rounded:0}" : rounded < 0f ? rounded.ToString("0") : "+0";
    }

    /// <summary>停留 → 淡出 → 隐藏</summary>
    private IEnumerator HideRoutine()
    {
        yield return new WaitForSeconds(displaySeconds);

        var texts = new[] { waterText, sunlightText, nutrientText };

        if (fadeSeconds <= 0f)
        {
            SetAllActive(texts, false);
            hideRoutine = null;
            yield break;
        }

        // 整组一起淡出，视觉上是一个整体消失
        for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            float a = 1f - t / fadeSeconds;
            foreach (var text in texts)
                SetAlpha(text, a);
            yield return null;
        }

        SetAllActive(texts, false);
        hideRoutine = null;
    }

    /// <summary>立即隐藏全部指示文本（初始状态 / 淡出兜底）</summary>
    private void HideImmediate()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }
        SetAllActive(new[] { waterText, sunlightText, nutrientText }, false);
    }

    private static void SetAllActive(TMP_Text[] texts, bool active)
    {
        foreach (var text in texts)
        {
            if (text != null && text.gameObject.activeSelf != active)
                text.gameObject.SetActive(active);
        }
    }

    private static void SetAlpha(TMP_Text text, float alpha)
    {
        if (text == null) return;
        var c = text.color;
        c.a = alpha;
        text.color = c;
    }
}
