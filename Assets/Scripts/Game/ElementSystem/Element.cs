using UnityEngine;

/// <summary>元素类型：基础元素 / 特殊元素</summary>
public enum ElementType
{
    /// <summary>基础元素（水、火、土、风等，作为合成原料）</summary>
    Basic = 0,
    /// <summary>特殊元素（由基础元素合成而来，效果更强）</summary>
    Special = 1,
}

/// <summary>
/// 元素数据（ScriptableObject）。
/// 在 Project 窗口右键 Create -> Gamejam -> Element 创建元素资产，
/// 配置好后拖入 ElementCraftSystem 的配方列表或背包中使用。
///
/// 每个元素可配置两个自定义事件名：
/// - UseEventName：该元素被使用时，除了全局 ElementUsed 事件外额外触发
/// - OnCraftedEventName：该元素被合成出来时，除了全局 ElementCombined 事件外额外触发
/// 这样"不同的元素触发不同的事件"只需在资产上填事件名，不用写死在代码里。
/// </summary>
[CreateAssetMenu(menuName = "Gamejam/Element", fileName = "NewElement")]
public class Element : ScriptableObject
{
    [Header("基础信息")]
    [Tooltip("元素唯一标识（建议与文件名一致，用于日志与调试）")]
    public string ElementId;

    [Tooltip("显示名称")]
    public string DisplayName;

    [Tooltip("元素类型：基础 / 特殊")]
    public ElementType Type = ElementType.Basic;

    [TextArea, Tooltip("描述文案")]
    public string Description;

    [Header("自定义事件（可选，留空则不额外触发）")]
    [Tooltip("该元素被使用时额外触发的事件名（参数：Element 本元素）")]
    public string UseEventName;

    [Tooltip("该元素作为产物被合成时额外触发的事件名（参数：Element 本元素）")]
    public string OnCraftedEventName;

    public override string ToString() => $"{DisplayName}({ElementId}, {Type})";
}
