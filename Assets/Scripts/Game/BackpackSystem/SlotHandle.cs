using System.Collections;
using System.Collections.Generic;
using Framwork; // EventCenter / EventName
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.BackpackSystem
{
    /// <summary>
    /// 卡槽的接放逻辑：挂在每个 slot GameObject 上。
    /// 要求：slot 自身有 Image（raycastTarget=true，能接收拖放事件）。
    /// 行为：拖动物品到本 slot 上方并松手时，OnDrop 触发；如果 slot 为空，就把当前被拖的物品 SetParent 到自己下面。
    /// </summary>
    public class SlotHandle : MonoBehaviour, IDropHandler, IPointerClickHandler
    {
        [Tooltip("克隆放置模式（合成材料槽用）：放下时克隆一份进槽、原件送回原位。用于「同种元素进两个材料槽」（如 光+光）——背包里每种元素只有一个图标，不克隆的话同种元素无法同时进两个槽")]
        [SerializeField] private bool cloneOnDrop = false;

        /// <summary>本槽是否为克隆放置模式（供外部查询）</summary>
        public bool CloneOnDrop => cloneOnDrop;

        // 飞回背包动画时长（秒）
        private const float ReturnFlyDuration = 0.3f;

        public GameObject item
        {
            get
            {
                // 槽位里的物品：取第一个"可见"的子物体
                // （合成动画期间材料会被临时隐藏，隐藏中的不算占用，不挡后续放置）
                for (int i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i).gameObject;
                    if (child.activeSelf) return child;
                }
                return null;
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            var dragged = DragHandle.itemBeginDragged;
            if (dragged == null) return;

            // 仅当槽位为空时才允许放置（每个卡槽只能放一个）
            if (item) return;

            if (cloneOnDrop)
            {
                // 材料槽：克隆一份放进槽里，原件留在原处（背包）——同种元素可以同时进两个材料槽
                var clone = Instantiate(dragged, transform);
                clone.transform.localPosition = Vector3.zero;
                clone.transform.localScale = Vector3.one;
                // 原件此刻 blocksRaycasts=false（拖拽中），克隆必须恢复射线，否则之后拖不动它
                var cg = clone.GetComponent<CanvasGroup>();
                if (cg != null) cg.blocksRaycasts = true;

                // 原件送回原位（父物体未变，OnEndDrag 会把它摆回 startPos）
                var drag = dragged.GetComponent<DragHandle>();
                Transform home = drag != null ? drag.StartParent : null;
                if (home != null && dragged.transform.parent != home)
                    dragged.transform.SetParent(home, false);
            }
            else
            {
                // 判断物品是否来自某个背包面板：拖拽开始时物品已挂到根 Canvas（置顶层），
                // 不能用 GetComponentInParent 顺着当前父级查，要用 DragHandle 记录的原位父物体查
                var drag = dragged.GetComponent<DragHandle>();
                Transform home = drag != null ? drag.StartParent : dragged.transform.parent;
                var fromPanel = home != null
                    ? home.GetComponentInParent<BackpackPanelUI>()
                    : null;

                dragged.transform.SetParent(transform);

                // 从背包"取出"物品（如放进合成材料槽）时：数据层并没有移除该元素
                // （元素永远在背包数据里），但背包格子的图标已被拖走。
                // 广播 BackpackChanged 让背包面板按数据层立即补回一个图标——
                // 效果等同"取出时自动复制一份回背包"。
                if (fromPanel != null)
                    EventCenter.Trigger(EventName.BackpackChanged);
            }
        }

        /// <summary>
        /// 点击取回（材料槽自动生效，无需任何 Inspector 配置）：
        /// 点击槽内物品 → 图标飞回背包中该元素所在的格子 → 到达后移除自己。
        ///
        /// 实现原理：取出物品时数据层从未移除元素，背包里该元素的图标一直在原位，
        /// 飞行动画只是视觉表达，到达后销毁飞行图标即可；
        /// 再广播一次 BackpackChanged 让双背包按数据重刷，保证显示与数据完全一致。
        ///
        /// 背包面板自己的格子点击无效（物品已经在背包里，无事可做）。
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            // 防误触 1：拖拽放下的瞬间可能跟随触发一次点击，短时间内的点击忽略
            if (Time.unscaledTime - DragHandle.LastDragEndTime < 0.2f) return;
            // 防误触 2：有物品正在拖拽中（多点触控等极端情况），忽略
            if (DragHandle.itemBeginDragged != null) return;

            var go = item;
            if (go == null) return; // 空槽，无事可做

            // 背包面板自己的格子：物品已在"家"，点击不做任何事
            if (GetComponentInParent<BackpackPanelUI>() != null) return;

            var view = go.GetComponent<ItemView>();
            Element element = view != null ? view.Element : null;

            // 找飞回终点：元素所属类型的背包面板中，正在显示同一元素的格子（其次空格子）
            Vector3 endPos;
            if (!TryFindLandingPosition(element, out endPos))
            {
                // 找不到落点（元素不在背包数据 / 面板不存在）：直接移除并按数据刷新
                Destroy(go);
                EventCenter.Trigger(EventName.BackpackChanged);
                return;
            }

            StartCoroutine(FlyBackAndRemove(go, endPos));
        }

        /// <summary>
        /// 查找飞回终点：优先取"与本槽元素同类型"的背包面板，
        /// 在其中找正在显示同一元素的格子（背包取出时已补齐，通常都能找到），
        /// 找不到则退回第一个空格子。都没有返回 false。
        /// </summary>
        private bool TryFindLandingPosition(Element element, out Vector3 endPos)
        {
            endPos = transform.position;

            if (element == null) return false;

            // 找与元素类型匹配的背包面板（基础元素 → 基础背包，特殊元素 → 合成物背包）
            BackpackPanelUI targetPanel = null;
            foreach (var panel in FindObjectsByType<BackpackPanelUI>(FindObjectsSortMode.None))
            {
                if (panel.DisplayType == element.Type)
                {
                    targetPanel = panel;
                    break;
                }
            }

            // 兜底：没有同类型面板时退回任意面板（正常配置下不会发生）
            if (targetPanel == null)
                targetPanel = FindFirstObjectByType<BackpackPanelUI>();
            if (targetPanel == null) return false;

            var slot = targetPanel.FindLandingSlot(element);
            if (slot == null) return false; // 面板全满且没有同元素图标

            endPos = slot.transform.position;
            return true;
        }

        /// <summary>
        /// 飞行动画：图标挂到根 Canvas 最顶层（盖在其他 UI 之上）飞往背包落点，
        /// 到达后移除自己，并广播 BackpackChanged 让背包显示与数据层对齐。
        /// </summary>
        private IEnumerator FlyBackAndRemove(GameObject go, Vector3 endPos)
        {
            // 挂到根 Canvas 最顶层飞行，保证盖在其他 UI 之上
            var canvas = GetComponentInParent<Canvas>();
            Transform layer = canvas != null ? canvas.transform : null;
            var t = go.transform;
            if (layer != null && t.parent != layer)
            {
                t.SetParent(layer, true);
                t.SetAsLastSibling();
            }

            // 飞行期间不响应射线：防止飞行中被再次点击或拖拽
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = false;

            Vector3 start = t.position;
            float p = 0f;
            while (p < 1f)
            {
                p = Mathf.Min(1f, p + Time.deltaTime / ReturnFlyDuration);
                t.position = Vector3.Lerp(start, endPos, Mathf.SmoothStep(0f, 1f, p));
                yield return null;
            }

            // 到达背包位置：移除自己，广播刷新让显示与数据层完全一致
            Destroy(go);
            EventCenter.Trigger(EventName.BackpackChanged);
        }
    }
}
