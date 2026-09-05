using System.Collections.Generic;
using Framwork;
using UnityEngine;

/// <summary>
/// 元素合成系统（场景单例，继承 Framwork.Singleton）。
/// 挂载到场景中一个常驻 GameObject 上即可（比如游戏管理器物体）。
///
/// 职责：
/// - 维护合成配方列表（Inspector 中配置）
/// - TryCombine(a, b)：尝试合成，成功/失败都会通过 EventCenter 广播事件
/// - Use(element)：使用元素，同样通过 EventCenter 广播事件
///
/// 事件一览（均在 FramWork.EventName 中定义）：
/// - ElementCombined（Element a, Element b, Element result）合成成功
/// - ElementCombineFailed（Element a, Element b）合成失败（没有匹配配方）
/// - ElementUsed（Element element）元素被使用
/// 此外，配方 / 元素上配置的自定义事件名会在对应时机额外触发，
/// 实现"不同的元素、不同的合成触发不同的事件"。
///
/// 原料 / 产物：
/// - 本系统不负责原料扣除（请由调用方根据需求自行处理）
/// - 合成成功后，若场景中存在 BackpackSystem，会自动把产物 +1 入背包
///   （BackpackSystem 未挂载时安全跳过，不报错）
///
/// 用法示例：
/// <code>
/// // 任意脚本调用
/// ElementCraftSystem.Instance.TryCombine(waterElement, fireElement);
/// ElementCraftSystem.Instance.Use(steamElement);
///
/// // 监听合成成功
/// EventCenter.Subscribe&lt;Element, Element, Element&gt;(EventName.ElementCombined,
///     (a, b, result) =&gt; Debug.Log($"合成：{a} + {b} = {result}"));
/// </code>
/// </summary>
public class ElementCraftSystem : Singleton<ElementCraftSystem>
{
    [Tooltip("合成配方列表")]
    public List<ElementRecipe> Recipes = new List<ElementRecipe>();

    // ==================== 合成 ====================

    /// <summary>
    /// 尝试合成两个元素（顺序无关）。
    /// 成功：广播 ElementCombined + 配方自定义事件 + 产物 OnCrafted 事件，
    ///      若场景中存在 BackpackSystem 则把产物 +1 自动入背包，返回 true
    /// 失败：广播 ElementCombineFailed，返回 false
    /// 注意：本系统不管理原料扣除（背包逻辑），是否扣除原料由调用方决定。
    /// </summary>
    public bool TryCombine(Element a, Element b)
    {
        if (a == null || b == null)
        {
            Debug.LogWarning("[ElementCraftSystem] 合成原料为空");
            return false;
        }

        foreach (var recipe in Recipes)
        {
            if (recipe == null || !recipe.Matches(a, b))
                continue;

            var result = recipe.Output;
            if (result == null)
            {
                Debug.LogError($"[ElementCraftSystem] 配方 {a}+{b} 的产物为空，请检查配方");
                continue;
            }

            // 1. 全局合成成功事件
            EventCenter.Trigger(EventName.ElementCombined, a, b, result);

            // 2. 该配方自定义事件
            if (!string.IsNullOrEmpty(recipe.CombineEventName))
                EventCenter.Trigger(recipe.CombineEventName, result);

            // 3. 产物元素自己的"被合成"事件
            if (!string.IsNullOrEmpty(result.OnCraftedEventName))
                EventCenter.Trigger(result.OnCraftedEventName, result);

            // 4. 若场景中有背包系统，产物自动入库（未挂载时安全跳过）
            BackpackSystem.Instance?.Add(result, 1);

            Debug.Log($"[ElementCraftSystem] 合成成功：{a} + {b} = {result}");
            return true;
        }

        // 没有匹配配方
        EventCenter.Trigger(EventName.ElementCombineFailed, a, b);
        Debug.Log($"[ElementCraftSystem] 合成失败：{a} + {b} 没有对应配方");
        return false;
    }

    /// <summary>查询两个元素是否存在配方（不触发任何事件，可用于 UI 提示）</summary>
    public Element GetCombineResult(Element a, Element b)
    {
        foreach (var recipe in Recipes)
        {
            if (recipe != null && recipe.Matches(a, b))
                return recipe.Output;
        }
        return null;
    }

    // ==================== 使用 ====================

    /// <summary>
    /// 使用元素：广播全局 ElementUsed 事件。
    /// 若该元素配置了 UseEventName，还会额外触发该自定义事件（参数：本元素）。
    /// 具体使用效果（伤害、治疗、种植加成等）由各系统监听事件自行实现。
    /// </summary>
    public void Use(Element element)
    {
        if (element == null)
        {
            Debug.LogWarning("[ElementCraftSystem] 使用的元素为空");
            return;
        }

        // 1. 全局"元素被使用"事件
        EventCenter.Trigger(EventName.ElementUsed, element);

        // 2. 该元素自己的使用事件（不同元素不同事件的关键）
        if (!string.IsNullOrEmpty(element.UseEventName))
            EventCenter.Trigger(element.UseEventName, element);

        Debug.Log($"[ElementCraftSystem] 使用元素：{element}");
    }
}
