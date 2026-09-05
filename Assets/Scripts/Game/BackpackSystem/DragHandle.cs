using System.Collections;
using System.Collections.Generic;
using Framwork;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;  // 项目用 InputSystem，不能用 UnityEngine.Input

namespace Game.BackpackSystem
{
    /// <summary>
    /// 物品自身的拖拽逻辑：挂在每个可拖拽的物品 GameObject 上。
    /// 要求：物品 Image 上挂有 CanvasGroup 组件（用于拖拽时临时关闭射线阻挡）。
    /// 行为：
    ///   - OnBeginDrag：记录起点位置 + 起点的父物体（卡槽/容器），把图标挂到根 Canvas
    ///     最顶层（离开格子后显示在所有 UI 之上），关闭射线阻挡
    ///   - OnDrag：跟随鼠标（Screen Space - Overlay 下 transform.position == 鼠标屏幕坐标）
    ///   - OnEndDrag：恢复射线阻挡；被卡槽接走则吸附到槽中心，没被接走则送回原格子
    /// </summary>
    public class DragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        // 当前被拖拽的物品，供 SlotHandle.OnDrop 引用（与文章一致用 static）
        public static GameObject itemBeginDragged;

        /// <summary>
        /// 最近一次拖拽结束的时间（Time.unscaledTime）。
        /// 点击取回（SlotHandle.OnPointerClick）用它防误触：
        /// 拖拽放下的瞬间可能跟随触发一次点击，短时间内的点击直接忽略。
        /// </summary>
        public static float LastDragEndTime { get; private set; } = -10f;

        // 起始位置（用于松手时"回去"）
        Vector3 startPos;
        // 起始父物体（用于判断"是否被放进别的卡槽"）
        Transform startParent;
        // 拖拽期间临时挂载的根 Canvas（图标置顶层）
        Transform dragLayer;

        /// <summary>
        /// 物品的"原位"父物体（最近一次拖拽起点的父物体）。
        /// 合成动画结束后把材料送回原背包格子用。
        /// </summary>
        public Transform StartParent => startParent;

        private void Awake()
        {
            // 实例化到某个 slot 时就记住它——没经过拖拽的物品也知道自己的原位
            startParent = transform.parent;

            // 拖拽层固定取根 Canvas（挂到它下面 + 最后一个 sibling = 显示在最上层）
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) dragLayer = canvas.transform;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            itemBeginDragged = gameObject;
            startPos = transform.position;
            startParent = transform.parent;

            // 图标离开格子后挂到根 Canvas 最顶层，保证显示在所有 UI 之上
            // （worldPositionStays = true：保持当前世界位置，图标不会跳一下）
            if (dragLayer != null && transform.parent != dragLayer)
            {
                transform.SetParent(dragLayer, true);
                transform.SetAsLastSibling();
            }

            // 拖拽时不阻挡射线，否则目标卡槽的 OnDrop 收不到事件
            GetComponent<CanvasGroup>().blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // 项目使用 InputSystem（原 UnityEngine.Input 已被禁用，会抛 InvalidOperationException）
            // Mouse.current.position 返回屏幕坐标，对 Screen Space - Overlay 而言 transform.position = 屏幕坐标。
            transform.position = Mouse.current.position.ReadValue();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            itemBeginDragged = null;
            LastDragEndTime = Time.unscaledTime;
            // 恢复射线阻挡
            GetComponent<CanvasGroup>().blocksRaycasts = true;

            if (transform.parent == startParent)
            {
                // 没离开过原格子（如克隆放置把原件送回了原处）：回到起点
                transform.position = startPos;
            }
            else if (dragLayer != null && transform.parent == dragLayer)
            {
                // 还挂在拖拽层上 = 没被任何卡槽接走：
                // 先看是否松手在植物的检测范围内（对植物使用该元素）
                if (TryUseOnPlant())
                    return;

                // 否则送回原格子
                ReturnHome();
            }
            else
            {
                // 被卡槽接走：SetParent 默认保留世界位置（物品会"悬"在原屏幕点），
                // 主动把 localPosition 归零，让物品立刻吸附到新 slot 的中心。
                transform.localPosition = Vector3.zero;
            }
        }

        /// <summary>
        /// 把物品送回原格子并吸附到格子中心。
        /// 若原格子已被"取出时补齐"的图标占用（拖拽途中背包被刷新过），
        /// 说明数据层图标已在原位，本件多余，直接销毁避免重叠。
        /// </summary>
        private void ReturnHome()
        {
            if (startParent == null)
            {
                Destroy(gameObject);
                return;
            }

            var slot = startParent.GetComponent<SlotHandle>();
            if (slot != null && slot.item != null && slot.item != gameObject)
            {
                Destroy(gameObject); // 原位已有补齐的图标
                return;
            }

            transform.SetParent(startParent, false);
            transform.localPosition = Vector3.zero;
        }

        /// <summary>
        /// 松手位置是否命中植物的检测范围（命中则对该植物使用本元素并返回 true）。
        /// 使用流程：
        /// 1. 触发全局 ElementUsed(Element, Plant) + 元素资产上配置的 UseEventName(Element, Plant)，
        ///    效果由 ElementUseEffectLibrary（数值表）订阅并应用到植物的水分/阳光/养分。
        /// 2. 特殊元素使用后从背包消耗 1 个（Remove 内部广播 BackpackChanged 刷新 UI）；
        ///    基础元素不消耗（BackpackSystem 配置了始终保留），广播一次刷新把图标补回背包。
        /// 3. 销毁拖拽中的图标（背包图标由刷新按数据层重建）。
        /// </summary>
        private bool TryUseOnPlant()
        {
            var view = GetComponent<ItemView>();
            var element = view != null ? view.Element : null;
            if (element == null) return false;

            // 鼠标位置（InputSystem 屏幕坐标）找检测范围内的植物
            var plant = Plant.GetPlantUnderPointer(Mouse.current.position.ReadValue());
            if (plant == null) return false;

            // 1. 广播使用事件：全局 + 元素自己的事件名（参数：元素 + 目标植物）
            EventCenter.Trigger(EventName.ElementUsed, element, plant);
            if (!string.IsNullOrEmpty(element.UseEventName))
                EventCenter.Trigger(element.UseEventName, element, plant);

            Debug.Log($"[DragHandle] 对植物 {plant.name} 使用元素 {element}");

            // 2. 特殊元素消耗 1 个；基础元素广播刷新让背包图标补回原位
            var backpack = global::BackpackSystem.Instance;
            if (element.Type == ElementType.Special)
            {
                if (backpack != null)
                    backpack.Remove(element, 1); // Remove 内部会广播 BackpackChanged
            }
            else
            {
                EventCenter.Trigger(EventName.BackpackChanged);
            }

            // 3. 销毁拖拽中的图标
            Destroy(gameObject);
            return true;
        }
    }
}
