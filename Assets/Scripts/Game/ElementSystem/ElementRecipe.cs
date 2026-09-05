using UnityEngine;

/// <summary>
/// 元素合成配方：InputA + InputB = Output（A/B 顺序无关）。
/// 在 ElementCraftSystem 组件的 Inspector 列表中配置。
/// </summary>
[System.Serializable]
public class ElementRecipe
{
    [Tooltip("原料 A")]
    public Element InputA;

    [Tooltip("原料 B")]
    public Element InputB;

    [Tooltip("合成产物")]
    public Element Output;

    [Header("自定义事件（可选）")]
    [Tooltip("此配方合成成功时额外触发的事件名（参数：Element 产物）")]
    public string CombineEventName;

    /// <summary>判断一组原料是否匹配本配方（顺序无关）</summary>
    public bool Matches(Element a, Element b)
    {
        return (a == InputA && b == InputB) || (a == InputB && b == InputA);
    }
}
