using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;  // 项目用 InputSystem，不能用 UnityEngine.Input

namespace Game.BackpackSystem
{
    /// <summary>
    /// 物品自身的拖拽逻辑：挂在每个可拖拽的物品 GameObject 上。
    /// 要求：物品 Image 上挂有 CanvasGroup 组件（用于拖拽时临时关闭射线阻挡）。
    /// 行为：
    ///   - OnBeginDrag：记录起点位置 + 起点的父物体（卡槽/容器），关闭射线阻挡
    ///   - OnDrag：跟随鼠标（Screen Space - Overlay 下 transform.position == 鼠标屏幕坐标）
    ///   - OnEndDrag：恢复射线阻挡；如果父物体还是原来那个（没放进别的卡槽），回到起点
    /// </summary>
    public class DragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        // 当前被拖拽的物品，供 SlotHandle.OnDrop 引用（与文章一致用 static）
        public static GameObject itemBeginDragged;

        // 起始位置（用于松手时"回去"）
        Vector3 startPos;
        // 起始父物体（用于判断"是否被放进别的卡槽"）
        Transform startParent;

        public void OnBeginDrag(PointerEventData eventData)
        {
            itemBeginDragged = gameObject;
            startPos = transform.position;
            startParent = transform.parent;
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
            // 恢复射线阻挡
            GetComponent<CanvasGroup>().blocksRaycasts = true;
            // 如果没被卡槽接走（父物体没变），回到起点
            if (transform.parent == startParent)
            {
                transform.position = startPos;
            }
            else
            {
                // 被卡槽接走后：SetParent 默认保留世界位置（物品会"悬"在原屏幕点），GridLayoutGroup 要到下一帧才重排。
                // 主动把 localPosition 归零，让物品立刻吸附到新 slot 的中心（与 layout 重排结果一致）。
                transform.localPosition = Vector3.zero;
            }
        }
    }
}
