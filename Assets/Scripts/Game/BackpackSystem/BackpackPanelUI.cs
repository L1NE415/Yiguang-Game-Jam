using System.Collections.Generic;
using Framwork;
using UnityEngine;
using UnityEngine.UI;

namespace Game.BackpackSystem
{
    /// <summary>
    /// 背包面板（数据驱动刷新版）。
    /// 挂在背包面板根物体上，负责把 BackpackSystem 的数据渲染成格子里的物品图标。
    ///
    /// 拖拽仍由 DragHandle（物品自身）/ SlotHandle（每个格子）处理，
    /// 本脚本只管"数据 -> 图标"的显示，两者互不干扰：
    /// - 订阅 BackpackChanged，任何库存变化后全量刷新格子
    /// - 刷新时清空所有格子子物体，再按 AllElements 顺序重新实例化物品图标
    ///
    /// 注意：
    /// - 背包格子的"真实来源"是 BackpackSystem 数据层。手动摆进格子的物品
    ///   会在下一次刷新时被数据层内容覆盖（这是数据驱动的预期行为）。
    /// - 正在被拖拽的物品不会被清掉（避免拖到一半图标消失）。
    /// </summary>
    public class BackpackPanelUI : MonoBehaviour
    {
        [Tooltip("背包格子容器：其下每个挂 SlotHandle 的子物体视为一个格子（按层级顺序填充）")]
        [SerializeField] private RectTransform slotContainer;

        [Tooltip("物品图标预制体（结构：根 Image + CanvasGroup + DragHandle + ItemView，可有数量子文本）")]
        [SerializeField] private ItemView itemPrefab;

        [Tooltip("本面板显示的元素类型：Basic = 基础元素背包，Special = 合成物背包。两个面板各挂一份、各选一种即可")]
        [SerializeField] private ElementType displayType = ElementType.Basic;

        /// <summary>本面板显示的元素类型（供 CraftUI 等外部系统查询飞行终点用）</summary>
        public ElementType DisplayType => displayType;

        // 收集到的格子列表（Awake 时按层级顺序固定）
        private readonly List<SlotHandle> _slots = new List<SlotHandle>();

        private void Awake()
        {
            if (slotContainer == null)
            {
                Debug.LogError($"[{nameof(BackpackPanelUI)}] 未指定 Slot Container，无法刷新");
                return;
            }

            // 挂了 SlotHandle 的直接子物体 = 背包格子
            _slots.AddRange(slotContainer.GetComponentsInChildren<SlotHandle>(false));

            if (itemPrefab == null)
                Debug.LogError($"[{nameof(BackpackPanelUI)}] 未指定 Item Prefab，无法生成物品图标");
        }

        private void OnEnable()
        {
            EventCenter.Subscribe(EventName.BackpackChanged, Refresh);
            Refresh();
        }

        private void OnDisable()
        {
            EventCenter.Unsubscribe(EventName.BackpackChanged, Refresh);
        }

        /// <summary>
        /// 找一个"落点"格子（供材料槽点击取回的飞行动画用）：
        /// 优先返回正在显示同一元素的格子；没有则返回第一个空格子；全满返回 null。
        /// </summary>
        public SlotHandle FindLandingSlot(Element element)
        {
            SlotHandle firstEmpty = null;
            foreach (var slot in _slots)
            {
                var child = slot.item;
                if (child == null)
                {
                    // 记住第一个空格子作备选
                    if (firstEmpty == null) firstEmpty = slot;
                    continue;
                }
                var view = child.GetComponent<ItemView>();
                if (view != null && view.Element == element)
                    return slot; // 背包里已有该元素的图标：落在它上面
            }
            return firstEmpty;
        }

        /// <summary>
        /// 全量刷新：清空格子 → 按 BackpackSystem 数据重新填充。
        /// </summary>
        public void Refresh()
        {
            // 正在拖拽本面板的物品时跳过本次刷新（拖拽中销毁图标会导致引用悬空），
            // 下一次 BackpackChanged 会再刷新，数据不会丢。
            var dragging = DragHandle.itemBeginDragged;
            if (dragging != null && dragging.transform.IsChildOf(transform))
                return;

            // 1. 清空每个格子的子物体
            foreach (var slot in _slots)
            {
                for (int i = slot.transform.childCount - 1; i >= 0; i--)
                    Destroy(slot.transform.GetChild(i).gameObject);
            }

            // 2. 按数据层顺序重新填充（背包未挂载时显示为空）
            var backpack = global::BackpackSystem.Instance;
            if (backpack == null || itemPrefab == null) return;

            int index = 0;
            // 双背包：只显示与本面板 displayType 匹配的元素
            foreach (var element in backpack.GetElements(displayType))
            {
                if (index >= _slots.Count) break; // 格子用满了，超出部分不显示

                var view = Instantiate(itemPrefab, _slots[index].transform);
                view.transform.localPosition = Vector3.zero; // 吸附到格子中心
                view.transform.localScale = Vector3.one;
                view.Setup(element, backpack.GetCount(element));
                index++;
            }
        }
    }
}
