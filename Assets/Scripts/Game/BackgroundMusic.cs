using UnityEngine;

namespace Framwork
{
    /// <summary>
    /// 背景音乐管理器（跨场景持续播放）：
    /// - Awake 时对自身 DontDestroyOnLoad，切换场景不会销毁、音乐不中断
    /// - 单例去重：如果目标场景里也放了一个 BGM 物体，会保留正在播放的旧实例、销毁新来的，避免叠音
    ///
    /// 使用方式：挂在一个带 AudioSource 的物体上（StartScene 的 BGM 物体已配置好），
    /// AudioSource 建议勾选 Play On Awake + Loop。其他场景不需要再放 BGM。
    /// </summary>
    public class BackgroundMusic : MonoBehaviour
    {
        /// <summary>全局唯一实例（其他系统如需控制音量/切歌可通过它访问）</summary>
        public static BackgroundMusic Instance { get; private set; }

        private void Awake()
        {
            // 单例去重：已存在实例时，销毁后加载进来的这个
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 兜底：确保音乐在播（比如 Play On Awake 被关掉、或从代码动态创建的情况）
            AudioSource source = GetComponent<AudioSource>();
            if (source != null && source.clip != null && !source.isPlaying)
            {
                source.loop = true;
                source.Play();
            }
        }
    }
}
