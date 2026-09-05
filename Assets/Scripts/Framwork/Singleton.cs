using UnityEngine;

namespace Framwork
{
    /// <summary>
    /// MonoBehaviour 场景单例基类。
    /// 业务类继承 Singleton&lt;自身&gt; 即可获得 Instance，不用再手写 Awake 判重逻辑：
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T instance;

        /// <summary>全局唯一实例（未创建时为 null）</summary>
        public static T Instance => instance;

        protected virtual void Awake()
        {
            if (instance == null)
            {
                instance = this as T;
                // 只有真正存活的实例才需要跨场景保留
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            // 清空静态引用，避免场景卸载后 Instance 指向已销毁的对象
            if (instance == this as T)
                instance = null;
        }
    }
}
