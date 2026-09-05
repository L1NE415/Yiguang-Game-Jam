using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.BackpackSystem
{
    /// <summary>
    /// 背包物品的视图组件：挂在物品图标预制体上（配合 DragHandle / CanvasGroup 使用）。
    /// 职责：
    /// - 持有该图标代表的 Element 数据引用（合成槽读取它来判断配方）
    /// - Setup() 一次性填充图标 + 数量文本
    /// </summary>
    public class ItemView : MonoBehaviour
    {
        [Tooltip("物品图标（留空则自动取本物体上的 Image）")]
        [SerializeField] private Image icon;

        [Tooltip("数量文本（可留空，表示不显示数量）")]
        [SerializeField] private Text countText;

        /// <summary>该图标代表的元素数据（Setup 后有效）</summary>
        public Element Element { get; private set; }

        private void Awake()
        {
            // icon 没在 Inspector 里拖引用时，自动取自身 Image 兜底
            if (icon == null)
                icon = GetComponent<Image>();
        }

        /// <summary>
        /// 填充视图。数量文本只对需要计数的元素显示（见 ShouldShowCount）：
        /// 天气瓶显示剩余数量（含 1），基础元素与情绪元素不显示。
        /// </summary>
        public void Setup(Element element, int count = 1)
        {
            Element = element;

            if (icon != null)
            {
                // 图标要接收射线（DragHandle 靠它触发拖拽），保持 true
                icon.raycastTarget = true;
                if (element != null && element.Icon != null)
                {
                    icon.sprite = element.Icon;
                    icon.color = Color.white;
                    icon.enabled = true;
                }
                else
                {
                    // 没有图标数据（比如动态创建的情绪元素）时隐藏 Image，避免显示成白块
                    icon.sprite = null;
                    icon.enabled = false;
                }
            }

            if (countText != null)
            {
                countText.text = ShouldShowCount(element) ? count.ToString() : string.Empty;
                // 文本不参与射线，避免挡住图标 / 卡槽
                countText.raycastTarget = false;
            }
        }

        /// <summary>
        /// 是否显示剩余数量文本：
        /// - 天气瓶（ElementId 以 Element_P 开头）→ 显示剩余数量（含 1 个时也显示）
        /// - 情绪元素（Emotion_ 前缀的新情绪瓶 / Element_C 前缀的旧情绪产物）→ 不显示
        /// - 基础元素（Element_Water 等，无 P/C/Emotion 前缀）→ 不显示
        /// </summary>
        private static bool ShouldShowCount(Element element)
        {
            if (element == null) return false;

            string id = element.ElementId;
            if (string.IsNullOrEmpty(id)) return false;

            return id.StartsWith("Element_P", StringComparison.OrdinalIgnoreCase);
        }
    }
}
