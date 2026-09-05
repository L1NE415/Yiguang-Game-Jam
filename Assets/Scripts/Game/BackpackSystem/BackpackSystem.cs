using System.Collections.Generic;
using Framwork;
using UnityEngine;

/// <summary>
/// 背包系统（场景单例，继承 Framwork.Singleton）。
/// 挂到场景中一个常驻 GameObject 上即可（比如和 ElementCraftSystem 同一个物体）。
///
/// 职责：
/// - 运行时存储元素库存（内存数据，不持久化、不存档）
/// - 元素的存取、数量查询、容量限制
///
/// 事件一览（在 Framwork.EventName 中定义）：
/// - BackpackItemAdded(Element element, int added, int newCount)   元素被加入
/// - BackpackItemRemoved(Element element, int removed, int newCount)  元素被移除（含归零）
/// - BackpackChanged()  任何库存变化的总开关（无参数）
///
/// 用法示例：
/// <code>
/// // 获得元素（任意系统产出元素后调用）
/// BackpackSystem.Instance.Add(waterElement, 3);
///
/// // 查询与判断
/// int n = BackpackSystem.Instance.GetCount(waterElement);
/// if (BackpackSystem.Instance.Has(fireElement, 1)) { ... }
///
/// // 消耗并使用：先 Remove，成功后再触发 Use 效果
/// if (BackpackSystem.Instance.Remove(steamElement, 1))
///     ElementCraftSystem.Instance.Use(steamElement);
/// </code>
///
/// 与 ElementCraftSystem 的关系：
/// 合成系统不管理原料扣除（见 ElementCraftSystem.TryCombine 注释），
/// 调用方应在订阅 ElementCombined 事件后自行 Add 产物、Remove 原料，
/// 背包系统本身不与合成系统耦合。
/// </summary>
public class BackpackSystem : Singleton<BackpackSystem>
{
    [Header("容量（0 表示不限制）")]
    [Tooltip("单种元素的最大堆叠数")]
    public int MaxStackPerElement = 99;

    [Tooltip("背包总容量（所有元素累加，0 = 不限制）")]
    public int MaxTotal = 0;

    // 内部存储：ScriptableObject 引用 -> 数量
    private readonly Dictionary<Element, int> _items = new Dictionary<Element, int>();

    /// <summary>当前总元素数（所有种类累加）</summary>
    public int TotalCount
    {
        get
        {
            int t = 0;
            foreach (var v in _items.Values) t += v;
            return t;
        }
    }

    /// <summary>当前拥有的所有元素（用于 UI 遍历，顺序按 Add 先后）</summary>
    public IEnumerable<Element> AllElements => _items.Keys;

    /// <summary>查询某元素当前数量（没有则返回 0）</summary>
    public int GetCount(Element element)
    {
        if (element == null) return 0;
        return _items.TryGetValue(element, out int n) ? n : 0;
    }

    /// <summary>是否拥有指定元素至少 count 个</summary>
    public bool Has(Element element, int count = 1)
        => element != null && count > 0 && GetCount(element) >= count;

    /// <summary>
    /// 添加元素到背包。返回实际加入的数量（受容量限制时可能少于 count）。
    /// element 为 null、count &lt;= 0、容量已满等情况会返回 0 且不触发任何事件。
    /// </summary>
    public int Add(Element element, int count = 1)
    {
        if (element == null || count <= 0) return 0;

        // 1. 总容量限制
        if (MaxTotal > 0)
        {
            int room = MaxTotal - TotalCount;
            if (room <= 0) return 0;
            count = Mathf.Min(count, room);
        }

        // 2. 单种堆叠上限
        if (MaxStackPerElement > 0)
        {
            int current = GetCount(element);
            int room = MaxStackPerElement - current;
            if (room <= 0) return 0;
            count = Mathf.Min(count, room);
        }

        int newCount = GetCount(element) + count;
        _items[element] = newCount;

        EventCenter.Trigger(EventName.BackpackItemAdded, element, count, newCount);
        EventCenter.Trigger(EventName.BackpackChanged);
        return count;
    }

    /// <summary>
    /// 从背包移除元素。返回是否成功（数量不足时不移除、不触发事件）。
    /// 移除后归零的元素会自动从字典清理（避免 0 残留）。
    /// </summary>
    public bool Remove(Element element, int count = 1)
    {
        if (element == null || count <= 0) return false;
        int current = GetCount(element);
        if (current < count) return false;

        int newCount = current - count;
        if (newCount == 0)
            _items.Remove(element);
        else
            _items[element] = newCount;

        EventCenter.Trigger(EventName.BackpackItemRemoved, element, count, newCount);
        EventCenter.Trigger(EventName.BackpackChanged);
        return true;
    }

    /// <summary>清空背包（无变化时不触发事件）</summary>
    public void Clear()
    {
        if (_items.Count == 0) return;
        _items.Clear();
        EventCenter.Trigger(EventName.BackpackChanged);
    }
}
