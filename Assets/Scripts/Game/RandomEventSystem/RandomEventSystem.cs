using System.Collections;
using System.Collections.Generic;
using Framwork;
using UnityEngine;

/// <summary>
/// 突发事件系统（场景单例，继承 Framwork.Singleton）。
///
/// 职责：
/// - 开始游戏后每隔随机时间（minInterval ~ maxInterval 秒）随机触发一个突发事件
/// - 触发后把事件奖励的情绪元素加进基础背包（情绪元素为运行时动态创建的 Element 实例，Type = Basic）
/// - 特殊事件额外触发 EventName.RandomEventTriggered（参数：string 标题，string 文案），
///   由 RandomEventToastUI 订阅并弹提示框；普通事件静默发放
///
/// 零配置：场景里不挂本组件也会在运行时自动创建（[RuntimeInitializeOnLoadMethod]），
/// 使用默认间隔；想自定义间隔就在场景里挂上并调 Inspector 参数。
///
/// 事件与文案全部定义在 EmotionEventLibrary（纯代码），改文案不用进 Unity。
/// </summary>
public class RandomEventSystem : Singleton<RandomEventSystem>
{
    [Header("触发间隔（秒）")]
    [Tooltip("两次事件之间的最小间隔")]
    [SerializeField] private float minInterval = 30f;

    [Tooltip("两次事件之间的最大间隔")]
    [SerializeField] private float maxInterval = 60f;

    [Header("事件抽取")]
    [Tooltip("特殊事件的触发概率（0~1，剩余概率触发普通事件）")]
    [SerializeField, Range(0f, 1f)] private float specialEventChance = 0.35f;

    [Header("情绪元素资产")]
    [Tooltip("全部情绪元素资产（事件奖励按 ElementId 匹配；留空时回退为运行时动态创建，无图标）")]
    [SerializeField] private List<Element> emotionElements = new List<Element>();

    [Header("调试（只读）")]
    [Tooltip("下一次事件还有多少秒（仅 Inspector 查看）")]
    [SerializeField] private float debugNextEventCountdown = -1f;

    [Tooltip("最近一次触发的事件描述（仅 Inspector 查看）")]
    [SerializeField] private string debugLastEvent = "（尚未触发）";

    /// <summary>已创建的情绪元素实例缓存（同一情绪元素共用一个实例，背包才能正确堆叠计数）</summary>
    private readonly Dictionary<string, Element> _elementCache = new Dictionary<string, Element>();

    /// <summary>本实例是否为自举自动创建的空白实例（未配置情绪元素资产）</summary>
    private bool isAutoCreated;

    protected override void Awake()
    {
        // 场景中配置好的实例（带情绪元素资产）优先级更高：
        // 如果当前单例是自举创建的空白实例（例如游戏从 StartScene 启动时自举抢先生成），
        // 就销毁空白实例、由本实例接管单例——保证事件奖励用得上配置的图标与文案
        if (!isAutoCreated && instance is RandomEventSystem existing && existing != this && existing.isAutoCreated)
        {
            Destroy(existing.gameObject);
            instance = null;
        }

        base.Awake();
    }

    private void Start()
    {
        StartCoroutine(EventLoop());
    }

    /// <summary>主循环：等待随机时长 -> 触发一个事件 -> 循环。使用 scaled time（timeScale=0 时随游戏暂停一起停）</summary>
    private IEnumerator EventLoop()
    {
        while (true)
        {
            float wait = Random.Range(Mathf.Max(0.1f, minInterval), Mathf.Max(minInterval + 0.1f, maxInterval));
            debugNextEventCountdown = wait;

            // 逐帧递减调试倒计时（WaitForSeconds 不方便拿剩余值）
            float elapsed = 0f;
            while (elapsed < wait)
            {
                yield return null;
                elapsed += Time.deltaTime;
                debugNextEventCountdown = wait - elapsed;
            }

            TriggerRandomEvent();
        }
    }

    /// <summary>按配置概率随机抽取并触发一个事件</summary>
    [ContextMenu("立即触发一个随机事件（调试）")]
    public void TriggerRandomEvent()
    {
        bool wantSpecial = Random.value < specialEventChance;

        // 先按"想要的类型"抽，抽不到（理论上不会，表里两类都有）就退回全表随机
        var pool = new List<EmotionEventLibrary.EmotionEvent>();
        foreach (var e in EmotionEventLibrary.Events)
            if (e.IsSpecial == wantSpecial) pool.Add(e);
        if (pool.Count == 0) pool = EmotionEventLibrary.Events;

        var evt = pool[Random.Range(0, pool.Count)];
        TriggerEvent(evt);
    }

    /// <summary>
    /// 触发指定事件：发放情绪元素；特殊事件额外广播提示框事件。
    /// 外部系统（比如剧情脚本）也可以直接调用它强制触发某个事件。
    /// </summary>
    public void TriggerEvent(EmotionEventLibrary.EmotionEvent evt)
    {
        if (evt == null) return;

        Element element = GetOrCreateRewardElement(evt.RewardId);
        if (element == null)
        {
            Debug.LogError($"[RandomEventSystem] 事件 {evt.Id} 的奖励元素 {evt.RewardId} 不存在，事件未生效");
            return;
        }

        // 情绪元素发放进背包（BackpackSystem 不在场景时无法发放，警告提示）
        if (BackpackSystem.Instance == null)
        {
            Debug.LogWarning($"[RandomEventSystem] 场景中没有 BackpackSystem，事件 {evt.Id} 的奖励无法入包");
        }
        else
        {
            BackpackSystem.Instance.Add(element, 1);
        }

        debugLastEvent = $"{evt.Id} {(evt.IsSpecial ? "[特殊]" : "[普通]")} -> {element.DisplayName}";
        Debug.Log($"[RandomEventSystem] 突发事件 {debugLastEvent}");

        // 特殊事件：广播给提示框 UI（标题 + 文案由 Toast 显示，持续时长由 Toast 控制）
        if (evt.IsSpecial)
        {
            EventCenter.Trigger(EventName.RandomEventTriggered, evt.Title, evt.Message);
        }
    }

    /// <summary>
    /// 按情绪元素 Id 获取 Element 实例：
    /// 优先从 Inspector 配置的资产列表（emotionElements）里按 ElementId 匹配——
    /// 这样资产上配的图标 / 描述 / 事件名全部生效；
    /// 列表没配或没匹配到时，回退为运行时动态创建（Type = Basic、无图标，不落盘）。
    /// </summary>
    private Element GetOrCreateRewardElement(string rewardId)
    {
        if (string.IsNullOrEmpty(rewardId)) return null;

        // 1. 优先：Inspector 配置的情绪元素资产
        foreach (var element in emotionElements)
        {
            if (element != null && element.ElementId == rewardId)
                return element;
        }

        // 2. 回退：动态创建实例的缓存（同一情绪元素共用一个实例，背包才能正确堆叠计数）
        if (_elementCache.TryGetValue(rewardId, out Element cached) && cached != null)
            return cached;

        var def = EmotionEventLibrary.GetElementDef(rewardId);
        if (def == null) return null;

        var element2 = ScriptableObject.CreateInstance<Element>();
        element2.ElementId = def.Id;
        element2.DisplayName = def.DisplayName;
        element2.Type = ElementType.Basic;   // 存放在基础背包
        element2.Description = def.Description;
        // Icon 留空：ItemView 遇到空图标会隐藏 Image；UseEventName 留空：暂无对植物的使用效果

        _elementCache[rewardId] = element2;
        Debug.LogWarning($"[RandomEventSystem] 情绪元素 {rewardId} 未在 Inspector 资产列表中配置，已回退为动态创建（无图标）");
        return element2;
    }

    // ==================== 零配置自举 ====================

    /// <summary>
    /// 场景加载后自动确保系统存在（场景里没挂也能跑），同时确保提示框 UI 已就绪。
    /// 场景里手动挂了本组件的场合（想调间隔参数），这里会跳过创建只补 Toast。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null &&
            FindObjectsByType<RandomEventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length == 0)
        {
            var go = new GameObject("RandomEventSystem");
            var comp = go.AddComponent<RandomEventSystem>();
            comp.isAutoCreated = true;   // 标记为空白实例：之后场景里配置好的实例加载时可将其接管替换
            Debug.Log("[RandomEventSystem] 场景未配置，已自动创建（默认间隔 30~60 秒）");
        }

        // 提示框 UI 一起自举（已存在则跳过）
        RandomEventToastUI.EnsureToast();
    }
}
