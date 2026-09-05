using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.BackpackSystem
{
    /// <summary>
    /// 卡槽的接放逻辑：挂在每个 slot GameObject 上。
    /// 要求：slot 自身有 Image（raycastTarget=true，能接收拖放事件）。
    /// 行为：拖动物品到本 slot 上方并松手时，OnDrop 触发；如果 slot 为空，就把当前被拖的物品 SetParent 到自己下面。
    /// </summary>
    public class SlotHandle : MonoBehaviour, IDropHandler
    {
        public GameObject item
        {
            get
            {
                // 槽位里的物品：取第一个子物体
                if (transform.childCount > 0)
                {
                    return transform.GetChild(0).gameObject;
                }
                return null;
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            // 仅当槽位为空时才允许放置（每个卡槽只能放一个，与文章一致）
            if (!item)
            {
                DragHandle.itemBeginDragged.transform.SetParent(transform);
            }
        }
    }
}
