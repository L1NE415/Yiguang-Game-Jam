using System.Collections;
using Framwork;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 突发事件提示框 UI。
///
/// 职责：
/// - 订阅 EventName.RandomEventTriggered（参数：string 标题，string 文案）
/// - 收到事件后显示提示框，持续 displayDuration 秒（默认 2.5s）后自动失活
/// - 文案内容由 EmotionEventLibrary（代码）提供，本组件只负责展示
///
/// 两种使用方式（二选一）：
/// 1. 零配置：什么都不做。RandomEventSystem 自举时会调用 EnsureToast()，
///    自动在场景 Canvas 下创建一个内置样式的提示框（半透明黑底 + 标题/正文两行白字）
/// 2. 自定义：自己做一个提示框面板（失活状态），挂上本组件并把面板与文本拖进字段。
///    挂了本组件的物体存在时，EnsureToast 会跳过自动创建，直接用你的面板
///
/// 注意：本组件所在物体必须保持激活（面板失活没关系，组件本身要能收到事件）。
/// </summary>
public class RandomEventToastUI : MonoBehaviour
{
    [Header("提示框引用（留空则自动构建）")]
    [Tooltip("提示框面板（失活状态的物体；显示/隐藏的就是它）")]
    [SerializeField] private GameObject toastPanel;

    [Tooltip("标题文本（TMP 优先，其次 Legacy Text；都留空则自动构建面板时生成）")]
    [SerializeField] private TMPro.TMP_Text titleTextTMP;
    [SerializeField] private Text titleText;

    [Tooltip("正文文本（TMP 优先，其次 Legacy Text）")]
    [SerializeField] private TMPro.TMP_Text messageTextTMP;
    [SerializeField] private Text messageText;

    [Header("显示时长")]
    [Tooltip("提示框持续多少秒后自动消失")]
    [SerializeField] private float displayDuration = 2.5f;

    /// <summary>当前隐藏协程（重复触发时重置计时）</summary>
    private Coroutine hideRoutine;

    private void Awake()
    {
        // 场景里手动挂的组件：没拖面板时自动构建一个
        if (toastPanel == null)
            BuildToast();
    }

    private void OnEnable()
    {
        EventCenter.Subscribe<string, string>(EventName.RandomEventTriggered, OnRandomEventTriggered);
    }

    private void OnDisable()
    {
        EventCenter.Unsubscribe<string, string>(EventName.RandomEventTriggered, OnRandomEventTriggered);
    }

    /// <summary>收到特殊事件：填文案 -> 显示 -> displayDuration 秒后自动隐藏</summary>
    private void OnRandomEventTriggered(string title, string message)
    {
        if (toastPanel == null) return;

        SetText(titleTextTMP, titleText, title);
        SetText(messageTextTMP, messageText, message);

        // 置顶显示，避免被其他 UI 盖住
        toastPanel.transform.SetAsLastSibling();
        toastPanel.SetActive(true);

        // 重复触发时重置计时
        if (hideRoutine != null) StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        if (toastPanel != null) toastPanel.SetActive(false);
        hideRoutine = null;
    }

    /// <summary>填文本：TMP 优先，其次 Legacy Text</summary>
    private static void SetText(TMPro.TMP_Text tmp, Text legacy, string value)
    {
        if (tmp != null) tmp.text = value;
        else if (legacy != null) legacy.text = value;
    }

    // ==================== 自动构建内置面板 ====================

    /// <summary>
    /// 确保场景中存在提示框（幂等）：
    /// 已有挂本组件的物体（含失活）直接跳过；否则在第一个 Canvas 下创建内置样式面板。
    /// 由 RandomEventSystem 的自举逻辑调用，场景里什么都不配也能弹提示。
    /// </summary>
    public static void EnsureToast()
    {
        // 已存在（含失活物体上的）就不重复创建
        if (FindObjectsByType<RandomEventToastUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0)
            return;

        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[RandomEventToastUI] 场景中没有 Canvas，提示框无法创建（事件仍会正常发放奖励）");
            return;
        }

        var go = new GameObject("RandomEventToastUI", typeof(RandomEventToastUI));
        go.transform.SetParent(canvas.transform, false);
        go.GetComponent<RandomEventToastUI>().BuildToast();
        Debug.Log("[RandomEventToastUI] 已自动创建内置提示框");
    }

    /// <summary>
    /// 动态构建内置样式提示框：半透明黑底，顶部居中，标题 + 正文两行白字。
    /// 构建完 toastPanel 处于失活状态，等事件触发才显示。
    /// </summary>
    private void BuildToast()
    {
        // 内置字体（Unity 6 的 Legacy 内置字体）
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ---- 面板：屏幕顶部居中，半透明黑底 ----
        var panel = new GameObject("ToastPanel", typeof(Image));
        panel.transform.SetParent(transform, false);
        panel.SetActive(false);

        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -30f);
        panelRect.sizeDelta = new Vector2(640f, 150f);

        var panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.85f);
        panelImage.raycastTarget = false; // 不挡任何 UI 事件

        // ---- 标题（上半）----
        var title = CreateText(panel.transform, font, "TitleText");
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(20f, 6f);
        titleRect.offsetMax = new Vector2(-20f, -10f);
        title.fontSize = 30;
        title.fontStyle = FontStyle.Bold;
        title.color = Color.white;
        title.text = "事件标题";

        // ---- 正文（下半）----
        var message = CreateText(panel.transform, font, "MessageText");
        var messageRect = message.GetComponent<RectTransform>();
        messageRect.anchorMin = new Vector2(0f, 0f);
        messageRect.anchorMax = new Vector2(1f, 0.5f);
        messageRect.offsetMin = new Vector2(20f, 10f);
        messageRect.offsetMax = new Vector2(-20f, -6f);
        message.fontSize = 22;
        message.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        message.text = "事件文案";

        // 登记引用（本组件与面板同物体，Awake 可能已过，这里直接赋值即可）
        toastPanel = panel;
        titleText = title;
        messageText = message;
    }

    /// <summary>创建一个 Legacy Text 子物体（自动换行、居中对齐、不挡射线）</summary>
    private static Text CreateText(Transform parent, Font font, string name)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);

        var text = go.GetComponent<Text>();
        text.font = font;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }
}
