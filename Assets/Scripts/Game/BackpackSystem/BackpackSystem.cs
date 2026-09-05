using System.Collections.Generic;
using Framwork;
using UnityEngine;

/// <summary>
/// 背包系统（场景单例，继承 Framwork.Singleton）。
/// 挂到场景中一个常驻 GameObject 上即可（比如和 ElementCraftSystem 同一个物体）。
///
/// 职责：
/// - 运行时存储元素库存（内存数据，不持久化、不存档）
/// - 元素的存取、数量查询（基础元素不限量；特殊元素每种有堆叠上限，见 specialElementMaxCount）
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
    [Header("双背包配置")]
    [Tooltip("开局自动补进背包的基础元素（每种至少 1 个；基础元素背包始终有这些元素）")]
    [SerializeField] private List<Element> initialBasicElements = new List<Element>();

    [Tooltip("基础元素是否不可被移除（勾选后基础元素始终保留在背包里）")]
    [SerializeField] private bool keepBasicElementsAlways = true;

    [Tooltip("每种特殊元素的最大堆叠数量（0 = 不限制）。基础元素不受影响")]
    [SerializeField] private int specialElementMaxCount = 3;

    [Header("调试（只读）")]
    [Tooltip("当前背包内容快照（每次库存变化自动刷新，仅用于 Inspector 查看）")]
    [SerializeField] private List<string> debugItems = new List<string>();

    // 内部存储：ScriptableObject 引用 -> 数量
    private readonly Dictionary<Element, int> _items = new Dictionary<Element, int>();

    protected override void Awake()
    {
        base.Awake();
        // 只有真正的单例实例才做初始化（重复实例在 base.Awake 里已被销毁）
        if (Instance == this)
            EnsureInitialBasicElements();
    }

    /// <summary>
    /// 把 Inspector 里配置的基础元素补进背包（每种已有则跳过，不叠加数量）。
    /// Awake 时自动调用；运行时改了配置也可以手动再调。
    /// </summary>
    public void EnsureInitialBasicElements()
    {
        foreach (var element in initialBasicElements)
        {
            if (element != null && GetCount(element) == 0)
                Add(element, 1);
        }
    }

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

    /// <summary>查询指定类型的所有元素（Basic = 基础元素背包，Special = 合成物背包）</summary>
    public IEnumerable<Element> GetElements(ElementType type)
    {
        foreach (var e in _items.Keys)
        {
            if (e != null && e.Type == type)
                yield return e;
        }
    }

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
    /// 是否还能加入 count 个该元素（特殊元素受堆叠上限约束，基础元素始终为 true）。
    /// 合成前可用它预检容量，避免"合成成功但产物装不下"。
    /// </summary>
    public bool CanAdd(Element element, int count = 1)
    {
        if (element == null || count <= 0) return false;
        // 上限为 0 表示不限制；非特殊元素也不受限制
        if (element.Type != ElementType.Special || specialElementMaxCount <= 0)
            return true;
        return GetCount(element) + count <= specialElementMaxCount;
    }

    /// <summary>
    /// 添加元素到背包。返回实际加入的数量（特殊元素达到堆叠上限时可能少于 count 甚至为 0）。
    /// element 为 null、count &lt;= 0 时返回 0 且不触发任何事件。
    /// </summary>
    public int Add(Element element, int count = 1)
    {
        if (element == null || count <= 0) return 0;

        int current = GetCount(element);

        // 特殊元素堆叠上限：已满直接拒绝，未满则裁剪到剩余空间
        if (element.Type == ElementType.Special && specialElementMaxCount > 0)
        {
            int free = specialElementMaxCount - current;
            if (free <= 0)
            {
                Debug.LogWarning($"[BackpackSystem] 特殊元素 {element} 已达堆叠上限 {specialElementMaxCount}，无法再加入");
                return 0;
            }
            if (count > free)
                count = free; // 只装得下一部分：按剩余空间裁剪
        }

        int newCount = current + count;
        _items[element] = newCount;

        EventCenter.Trigger(EventName.BackpackItemAdded, element, count, newCount);
        EventCenter.Trigger(EventName.BackpackChanged);
        UpdateDebugView();
        return count;
    }

    /// <summary>
    /// 从背包移除元素。返回是否成功（数量不足时不移除、不触发事件）。
    /// 移除后归零的元素会自动从字典清理（避免 0 残留）。
    /// </summary>
    public bool Remove(Element element, int count = 1)
    {
        if (element == null || count <= 0) return false;

        // 基础元素始终保留（可配置）：保证基础元素背包里始终有初始元素
        if (keepBasicElementsAlways && element.Type == ElementType.Basic)
        {
            Debug.LogWarning($"[BackpackSystem] 基础元素 {element} 不可移除（始终保留）");
            return false;
        }

        int current = GetCount(element);
        if (current < count) return false;

        int newCount = current - count;
        if (newCount == 0)
            _items.Remove(element);
        else
            _items[element] = newCount;

        EventCenter.Trigger(EventName.BackpackItemRemoved, element, count, newCount);
        EventCenter.Trigger(EventName.BackpackChanged);
        UpdateDebugView();
        return true;
    }

    /// <summary>清空背包（无变化时不触发事件；保留基础元素时只清空特殊元素）</summary>
    public void Clear()
    {
        if (_items.Count == 0) return;
        _items.Clear();
        // 基础元素需要"始终有"：清空后立即补回初始基础元素
        if (keepBasicElementsAlways)
            EnsureInitialBasicElements();
        EventCenter.Trigger(EventName.BackpackChanged);
        UpdateDebugView();
    }

    /// <summary>刷新 Inspector 调试列表（"名称(类型) x数量"每行一条；编辑器外为空操作）</summary>
    private void UpdateDebugView()
    {
#if UNITY_EDITOR
        debugItems.Clear();
        foreach (var kv in _items)
            debugItems.Add($"{kv.Key.DisplayName}({kv.Key.Type}) x{kv.Value}");
#endif
    }

    /// <summary>把当前背包内容打印到 Console（右键组件菜单也可调用）</summary>
    [ContextMenu("打印背包内容")]
    private void LogItems()
    {
        if (_items.Count == 0) { Debug.Log("[BackpackSystem] 背包为空"); return; }
        foreach (var kv in _items)
            Debug.Log($"[BackpackSystem] {kv.Key.DisplayName}({kv.Key.Type}) x{kv.Value}");
    }
}
