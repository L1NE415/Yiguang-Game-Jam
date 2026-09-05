using UnityEngine;

/// <summary>
/// 元素合成配方（ScriptableObject）：InputA + InputB = Output（A/B 顺序无关）。
/// Create -> Gamejam -> ElementRecipe 创建配方资产。
/// 配方资产统一放在 Assets/Data/ElementRecipes/ 下，
/// 手动拖入 ElementCraftSystem 的 Recipes 列表使用。
/// </summary>
[CreateAssetMenu(menuName = "Gamejam/ElementRecipe", fileName = "NewElementRecipe")]
public class ElementRecipe : ScriptableObject
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
