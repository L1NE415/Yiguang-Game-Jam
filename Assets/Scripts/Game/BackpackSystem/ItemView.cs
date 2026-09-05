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
        /// 填充视图。count <= 1 时隐藏数量文本（单个不显示数字）。
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
                countText.text = count > 1 ? count.ToString() : string.Empty;
                // 文本不参与射线，避免挡住图标 / 卡槽
                countText.raycastTarget = false;
            }
        }
    }
}
