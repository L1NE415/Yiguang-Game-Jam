using System.Collections;
using Game.BackpackSystem;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 合成面板 UI：两个材料槽 + 一个合成按钮。
/// 挂在合成面板根物体上（不需要命名空间——要引用全局的
/// ElementCraftSystem / BackpackSystem，放全局命名空间最省事）。
///
/// 完整流程（点击合成按钮后）：
///   1. 从两个材料槽取出 ItemView 的 Element，预检配方（无配方则物品弹回槽中心，流程结束）
///   2. 靠拢动画：两个材料图标向两槽中点移动
///   3. 合并：隐藏两个材料图标（不销毁）
///   4. 飞行动画：生成产物图标（临时 Image），从合并点飞向特殊元素背包面板，途中轻微放大
///   5. 材料归位：两个材料送回原背包格子并解除隐藏
///   6. 数据层合成 TryCombine：产物 Add 入背包 → BackpackChanged → 双背包按数据刷新图标
///
/// 原料不消耗：TryCombine 不扣原料，材料销毁后背包刷新时会从数据层
/// 重新生成材料图标——"合成不消耗背包物品"由数据驱动自然实现。
///
/// 接线：
///   - 两个材料槽各挂 SlotHandle（复用背包拖放的接放逻辑）
///   - 合成按钮拖入 craftButton 字段（Button 组件）
///   - 背包面板拖入 backpackPanel 字段（飞行终点；留空则自动场景中找）
///   - flyLayer 建议拖 Canvas 根（保证飞行图标盖在最上层；留空则用 Canvas）
/// </summary>
public class CraftUI : MonoBehaviour
{
    [Header("材料槽（各挂 SlotHandle）")]
    [Tooltip("材料槽 A")]
    [SerializeField] private SlotHandle slotA;

    [Tooltip("材料槽 B")]
    [SerializeField] private SlotHandle slotB;

    [Header("合成按钮")]
    [Tooltip("合成按钮（点击触发合成流程）")]
    [SerializeField] private Button craftButton;

    [Header("飞行目标")]
    [Tooltip("产物背包面板（运行时优先按产物类型自动解析对应面板，此字段仅在找不到匹配面板时兜底；建议拖合成物背包）")]
    [SerializeField] private RectTransform backpackPanel;

    [Tooltip("飞行图标所在层（建议 Canvas 根，保证盖在最上层）")]
    [SerializeField] private RectTransform flyLayer;

    [Header("动画参数")]
    [Tooltip("两个材料向中间靠拢的时长（秒）")]
    [SerializeField] private float convergeDuration = 0.25f;

    [Tooltip("产物图标飞向背包的时长（秒）")]
    [SerializeField] private float flyDuration = 0.45f;

    [Tooltip("产物图标飞行途中放大的幅度（0 = 不放大）")]
    [SerializeField] private float flyScalePunch = 0.35f;

    [Header("失败提示面板")]
    [Tooltip("合成失败提示面板（保持失活，合成失败时自动激活）")]
    [SerializeField] private GameObject failPanel;

    [Tooltip("失败面板上的关闭按钮（点击后面板失活）；留空则自动取面板下的第一个 Button")]
    [SerializeField] private Button failPanelCloseButton;

    // 合成流程进行中（防止连点）
    private bool _crafting;

    private void Start()
    {
        if (craftButton != null)
            craftButton.onClick.AddListener(OnCraftButtonClicked);
        else
            Debug.LogError("[CraftUI] 未指定合成按钮 Craft Button");

        // 终点 / 飞行层的兜底查找
        if (backpackPanel == null)
        {
            // 产物是特殊元素：优先找"合成物背包"面板（DisplayType = Special）
            foreach (var panel in FindObjectsByType<BackpackPanelUI>(FindObjectsSortMode.None))
            {
                if (panel.DisplayType == ElementType.Special)
                {
                    backpackPanel = (RectTransform)panel.transform;
                    break;
                }
            }

            // 没有特殊背包面板时，退回任意背包面板
            if (backpackPanel == null)
            {
                var panel = FindFirstObjectByType<BackpackPanelUI>();
                if (panel != null) backpackPanel = (RectTransform)panel.transform;
            }
        }
        if (flyLayer == null)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null) flyLayer = (RectTransform)canvas.transform;
        }

        // 失败面板关闭按钮接线：留空则自动取面板下的第一个 Button（含失活子物体）
        if (failPanel != null && failPanelCloseButton == null)
            failPanelCloseButton = failPanel.GetComponentInChildren<Button>(true);
        if (failPanelCloseButton != null)
            failPanelCloseButton.onClick.AddListener(CloseFailPanel);
        else if (failPanel != null)
            Debug.LogWarning("[CraftUI] 失败面板下没有找到 Button，无法通过点击关闭（可在 Fail Panel Close Button 手动指定）");
    }

    private void OnDestroy()
    {
        if (craftButton != null)
            craftButton.onClick.RemoveListener(OnCraftButtonClicked);
        if (failPanelCloseButton != null)
            failPanelCloseButton.onClick.RemoveListener(CloseFailPanel);
    }

    /// <summary>合成按钮点击入口</summary>
    private void OnCraftButtonClicked()
    {
        if (_crafting) return; // 动画播放中，忽略连点
        if (DragHandle.itemBeginDragged != null) return; // 有物品正在拖拽中，忽略点击

        var viewA = GetItemView(slotA);
        var viewB = GetItemView(slotB);

        // 槽没放满：提示并返回
        if (viewA == null || viewB == null)
        {
            Debug.Log("[CraftUI] 材料槽未放满，无法合成");
            ShowFailPanel();
            return;
        }

        // 预检配方：没有匹配配方时物品弹回各自槽中心，不进入动画
        var result = ElementCraftSystem.Instance.GetCombineResult(viewA.Element, viewB.Element);
        if (result == null)
        {
            Debug.Log($"[CraftUI] 没有配方：{viewA.Element} + {viewB.Element}，材料弹回槽位");
            viewA.transform.localPosition = Vector3.zero;
            viewB.transform.localPosition = Vector3.zero;
            ShowFailPanel();
            return;
        }

        // 预检背包容量：产物已达堆叠上限时同样弹回材料并提示失败（不进入动画、不消耗材料）
        var backpack = BackpackSystem.Instance;
        if (backpack != null && !backpack.CanAdd(result, 1))
        {
            Debug.Log($"[CraftUI] 产物 {result} 已达背包数量上限，材料弹回槽位");
            viewA.transform.localPosition = Vector3.zero;
            viewB.transform.localPosition = Vector3.zero;
            ShowFailPanel();
            return;
        }

        StartCoroutine(CraftSequence(viewA, viewB, result));
    }

    /// <summary>显示合成失败提示面板（面板未配置时静默跳过，不影响原有流程）</summary>
    private void ShowFailPanel()
    {
        if (failPanel != null) failPanel.SetActive(true);
    }

    /// <summary>关闭合成失败提示面板（由面板上的关闭按钮触发）</summary>
    private void CloseFailPanel()
    {
        if (failPanel != null) failPanel.SetActive(false);
    }

    /// <summary>完整合成动画流程</summary>
    private IEnumerator CraftSequence(ItemView viewA, ItemView viewB, Element result)
    {
        _crafting = true;

        // 先缓存两个材料的 Element（后面材料物体会被隐藏并送回原位）
        Element elementA = viewA.Element;
        Element elementB = viewB.Element;

        // ---- 1. 靠拢动画：两材料向中点移动 ----
        Vector3 mid = (slotA.transform.position + slotB.transform.position) * 0.5f;
        yield return MoveBothTo(viewA.transform, viewB.transform, mid, convergeDuration);

        // ---- 2. 合并：隐藏两个材料图标（不销毁，等产物飞完再送回原背包位）----
        viewA.gameObject.SetActive(false);
        viewB.gameObject.SetActive(false);

        // ---- 3. 产物图标从合并点飞向特殊元素背包 ----
        // 飞行终点：优先按产物类型解析对应背包面板（特殊元素 → 合成物背包），
        // 不信任 Inspector 拖引用（本场景就发生过误拖到基础背包的情况）；
        // 找不到匹配面板时才退回 backpackPanel 字段
        RectTransform flyTarget = FindPanelFor(result.Type) ?? backpackPanel;
        if (flyTarget != null && flyLayer != null && result.Icon != null)
        {
            GameObject flyer = new GameObject("CraftFlyIcon", typeof(Image));
            flyer.transform.SetParent(flyLayer, false);

            var img = flyer.GetComponent<Image>();
            img.sprite = result.Icon;
            img.raycastTarget = false; // 不挡任何 UI 事件
            img.color = Color.white;

            // 起点取两槽中点（合并处），终点取背包面板中心
            Vector3 start = mid;
            Vector3 end = flyTarget.position;
            Vector3 baseScale = Vector3.one;

            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / flyDuration);
                float e = Mathf.SmoothStep(0f, 1f, t);

                flyer.transform.position = Vector3.Lerp(start, end, e);
                // 缩放曲线：途中弹大再回落（正弦半波）
                flyer.transform.localScale = baseScale * (1f + flyScalePunch * Mathf.Sin(Mathf.PI * t));
                yield return null;
            }

            // 到达特殊背包：飞行图标消失（产物由数据层刷新生成）
            Destroy(flyer);
        }

        // ---- 4. 材料归位：送回各自原来的背包格子，再解除隐藏 ----
        // （随后数据层刷新会按背包数据在原位重建材料图标，位置一致、视觉无缝）
        ReturnToOriginalSlot(viewA);
        ReturnToOriginalSlot(viewB);

        // ---- 5. 数据层合成：内部自动 Add 产物 → BackpackChanged → 双背包刷新 ----
        // 基础背包按数据重建材料（原位）、特殊背包出现产物图标；不 Remove 原料 = 合成不消耗背包物品
        ElementCraftSystem.Instance.TryCombine(elementA, elementB);

        _crafting = false;
    }

    /// <summary>
    /// 把材料图标送回它原来的背包格子（DragHandle 记录的原位父物体）并解除隐藏：
    /// 隐藏 → 位置设为原背包格子中心 → 解除隐藏。
    /// 没有原位可回（手动摆放、从未拖拽过的物品）时直接销毁兜底。
    /// </summary>
    private static void ReturnToOriginalSlot(ItemView view)
    {
        if (view == null) return;

        var drag = view.GetComponent<DragHandle>();
        Transform home = drag != null ? drag.StartParent : null;

        if (home != null)
        {
            view.transform.SetParent(home, false);
            view.transform.localPosition = Vector3.zero; // 吸附到原格子中心
            view.gameObject.SetActive(true);             // 解除隐藏
        }
        else
        {
            Destroy(view.gameObject); // 无原位可回：销毁兜底
        }
    }

    /// <summary>同时把两个物体移动到同一点（世界坐标插值）</summary>
    private IEnumerator MoveBothTo(Transform a, Transform b, Vector3 target, float duration)
    {
        Vector3 posA = a.position;
        Vector3 posB = b.position;
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / duration);
            float e = Mathf.SmoothStep(0f, 1f, t);
            a.position = Vector3.Lerp(posA, target, e);
            b.position = Vector3.Lerp(posB, target, e);
            yield return null;
        }
    }

    /// <summary>
    /// 按元素类型查找对应的背包面板 RectTransform
    /// （特殊元素 → 合成物背包，基础元素 → 基础背包）。
    /// 找不到返回 null（调用方退回 Inspector 配置的 backpackPanel 兜底）。
    /// </summary>
    private static RectTransform FindPanelFor(ElementType type)
    {
        foreach (var panel in FindObjectsByType<BackpackPanelUI>(FindObjectsSortMode.None))
        {
            if (panel.DisplayType == type)
                return (RectTransform)panel.transform;
        }
        return null;
    }

    /// <summary>读取槽内物品的 ItemView（空槽 / 物品缺组件都返回 null）</summary>
    private static ItemView GetItemView(SlotHandle slot)
    {
        if (slot == null || slot.item == null) return null;
        return slot.item.GetComponent<ItemView>();
    }
}
