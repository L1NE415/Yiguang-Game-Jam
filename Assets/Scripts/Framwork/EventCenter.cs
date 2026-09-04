using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framwork
{
    /// <summary>
    /// 全局事件中心（观察者模式 / 发布-订阅中心）
    ///
    /// 用法示例：
    /// <code>
    /// // 订阅（比如在 MonoBehaviour 的 OnEnable 中）
    /// EventCenter.Subscribe("PlayerDead", OnPlayerDead);
    /// EventCenter.Subscribe<int>("ScoreChange", OnScoreChange);
    ///
    /// // 触发（任意地方）
    /// EventCenter.Trigger("PlayerDead");
    /// EventCenter.Trigger("ScoreChange", 100);
    ///
    /// // 取消订阅（记得在 OnDisable / OnDestroy 中调用，防止空引用）
    /// EventCenter.Unsubscribe("PlayerDead", OnPlayerDead);
    /// EventCenter.Unsubscribe("ScoreChange", OnScoreChange);
    /// </code>
    ///
    /// 事件名建议集中定义在 EventName.cs 中，避免手写字符串出错。
    /// </summary>
    public static class EventCenter
    {
        /// <summary>事件名 -> 该事件下所有回调（同一事件名可混合无参/带参签名）</summary>
        private static readonly Dictionary<string, List<Delegate>> mEventDict = new Dictionary<string, List<Delegate>>();

        // ==================== 订阅 ====================

        public static void Subscribe(string eventName, Action handler)
        {
            AddListener(eventName, handler);
        }

        public static void Subscribe<T>(string eventName, Action<T> handler)
        {
            AddListener(eventName, handler);
        }

        public static void Subscribe<T1, T2>(string eventName, Action<T1, T2> handler)
        {
            AddListener(eventName, handler);
        }

        public static void Subscribe<T1, T2, T3>(string eventName, Action<T1, T2, T3> handler)
        {
            AddListener(eventName, handler);
        }

        // ==================== 取消订阅 ====================

        public static void Unsubscribe(string eventName, Action handler)
        {
            RemoveListener(eventName, handler);
        }

        public static void Unsubscribe<T>(string eventName, Action<T> handler)
        {
            RemoveListener(eventName, handler);
        }

        public static void Unsubscribe<T1, T2>(string eventName, Action<T1, T2> handler)
        {
            RemoveListener(eventName, handler);
        }

        public static void Unsubscribe<T1, T2, T3>(string eventName, Action<T1, T2, T3> handler)
        {
            RemoveListener(eventName, handler);
        }

        // ==================== 触发 ====================

        /// <summary>触发无参事件（只会调用订阅时签名为 Action 的回调）</summary>
        public static void Trigger(string eventName)
        {
            if (!mEventDict.TryGetValue(eventName, out var handlers) || handlers.Count == 0)
                return;

            // 拷贝一份，避免回调中增删订阅导致遍历异常
            var snapshot = handlers.ToArray();
            foreach (var d in snapshot)
            {
                if (d is Action action)
                    SafeInvoke(eventName, action);
            }
        }

        /// <summary>触发单参事件</summary>
        public static void Trigger<T>(string eventName, T arg)
        {
            if (!mEventDict.TryGetValue(eventName, out var handlers) || handlers.Count == 0)
                return;

            var snapshot = handlers.ToArray();
            foreach (var d in snapshot)
            {
                if (d is Action<T> action)
                    SafeInvoke(eventName, action, arg);
            }
        }

        /// <summary>触发双参事件</summary>
        public static void Trigger<T1, T2>(string eventName, T1 arg1, T2 arg2)
        {
            if (!mEventDict.TryGetValue(eventName, out var handlers) || handlers.Count == 0)
                return;

            var snapshot = handlers.ToArray();
            foreach (var d in snapshot)
            {
                if (d is Action<T1, T2> action)
                    SafeInvoke(eventName, action, arg1, arg2);
            }
        }

        /// <summary>触发三参事件</summary>
        public static void Trigger<T1, T2, T3>(string eventName, T1 arg1, T2 arg2, T3 arg3)
        {
            if (!mEventDict.TryGetValue(eventName, out var handlers) || handlers.Count == 0)
                return;

            var snapshot = handlers.ToArray();
            foreach (var d in snapshot)
            {
                if (d is Action<T1, T2, T3> action)
                    SafeInvoke(eventName, action, arg1, arg2, arg3);
            }
        }

        // ==================== 清理 ====================

        /// <summary>移除某个事件下的全部回调（比如场景切换时按事件清理）</summary>
        public static void Clear(string eventName)
        {
            if (mEventDict.Remove(eventName))
                Debug.Log($"[EventCenter] 事件已清空: {eventName}");
        }

        /// <summary>清空所有事件（一般只在退出游戏 / 重置全局状态时调用，慎用）</summary>
        public static void ClearAll()
        {
            mEventDict.Clear();
        }

        /// <summary>查询某事件当前订阅数量（调试用）</summary>
        public static int GetListenerCount(string eventName)
        {
            return mEventDict.TryGetValue(eventName, out var handlers) ? handlers.Count : 0;
        }

        // ==================== 内部实现 ====================

        private static void AddListener(string eventName, Delegate handler)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                Debug.LogWarning("[EventCenter] 事件名不能为空");
                return;
            }
            if (handler == null)
            {
                Debug.LogWarning($"[EventCenter] 订阅 {eventName} 的回调为空");
                return;
            }

            if (!mEventDict.TryGetValue(eventName, out var handlers))
            {
                handlers = new List<Delegate>();
                mEventDict.Add(eventName, handlers);
            }

            // 防止同一回调重复订阅
            if (!handlers.Contains(handler))
                handlers.Add(handler);
            else
                Debug.LogWarning($"[EventCenter] 回调重复订阅了事件 {eventName}，已忽略");
        }

        private static void RemoveListener(string eventName, Delegate handler)
        {
            if (!mEventDict.TryGetValue(eventName, out var handlers))
                return;

            if (handlers.Remove(handler) && handlers.Count == 0)
                mEventDict.Remove(eventName);
        }

        /// <summary>异常隔离：单个回调出错不影响其余回调执行</summary>
        private static void SafeInvoke(string eventName, Action action)
        {
            try { action(); }
            catch (Exception e) { Debug.LogError($"[EventCenter] 事件 {eventName} 的回调异常: {e}"); }
        }

        private static void SafeInvoke<T>(string eventName, Action<T> action, T arg)
        {
            try { action(arg); }
            catch (Exception e) { Debug.LogError($"[EventCenter] 事件 {eventName} 的回调异常: {e}"); }
        }

        private static void SafeInvoke<T1, T2>(string eventName, Action<T1, T2> action, T1 arg1, T2 arg2)
        {
            try { action(arg1, arg2); }
            catch (Exception e) { Debug.LogError($"[EventCenter] 事件 {eventName} 的回调异常: {e}"); }
        }

        private static void SafeInvoke<T1, T2, T3>(string eventName, Action<T1, T2, T3> action, T1 arg1, T2 arg2, T3 arg3)
        {
            try { action(arg1, arg2, arg3); }
            catch (Exception e) { Debug.LogError($"[EventCenter] 事件 {eventName} 的回调异常: {e}"); }
        }
    }
}
