using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Framwork
{
    /// <summary>
    /// 音频管理器（场景单例，继承 Framwork.Singleton）。
    /// 挂到场景中一个常驻 GameObject 上即可（建议和其他系统管理器放同一物体）。
    ///
    /// 职责：统一管理 BGM（循环、淡入淡出）与 SFX（对象池重叠播放），
    /// 并提供「EventCenter 事件名 → 音效」的绑定列表——事件一触发自动出声，
    /// 与项目的事件驱动范式一致，大多数情况零代码配置音效。
    ///
    /// 用法示例：
    /// <code>
    /// // 代码播放
    /// AudioManager.Instance.PlayBGM(bgmClip);
    /// AudioManager.Instance.PlaySFX(clickClip);
    /// AudioManager.Instance.PlaySFX(clip, volumeScale: 0.6f, pitch: 1.2f);
    ///
    /// // 零代码播放：Inspector 的 EventSounds 列表里加一条
    /// //   EventName = EventName.RandomEventTriggered（或任意资产上自配的事件名字符串）
    /// //   Clip      = 对应音效，ArgContains 留空
    /// // 事件触发即播放；若事件带 string 首参且多个绑定共用同一事件名，
    /// // 用 ArgContains 区分（如按 RandomEventTriggered 的标题分别配音效）。
    ///
    /// // 音量控制（属性，赋值即生效）
    /// AudioManager.Instance.BgmVolume = 0.3f;
    /// AudioManager.Instance.SfxVolume = 1f;
    /// AudioManager.Instance.BgmMute = true;
    /// </code>
    ///
    /// 事件绑定说明：只自动订阅「无参事件」与「首参为 string 的事件」（0~3 个 string 参数都支持）。
    /// 带类型参数的事件（如 WeatherChanged(Weather, Weather)）请在自己的订阅回调里调 PlaySFX。
    /// </summary>
    public class AudioManager : Singleton<AudioManager>
    {
        // ==================== 数据结构 ====================

        /// <summary>事件音效绑定：事件名 → 音效（OnEnable 自动订阅，OnDisable 自动取消）</summary>
        [Serializable]
        public class EventSoundBinding
        {
            [Tooltip("要监听的 EventCenter 事件名：可用 Framwork.EventName 常量对应的字符串，也可填资产上自配的事件名（如 WeatherRainStart）")]
            public string EventName;

            [Tooltip("事件触发时播放的音效")]
            public AudioClip Clip;

            [Tooltip("该条绑定的音量 0~1（最终音量 = SFX 总音量 × 该音量）")]
            [Range(0f, 1f)] public float Volume = 1f;

            [Tooltip("仅当事件带 string 首参且首参包含该关键字时才播放（留空 = 一律播放）。典型用法：RandomEventTriggered 的首参是标题，按标题给每个突发事件配专属音效")]
            public string ArgContains;
        }

        // ==================== Inspector 配置 ====================

        [Header("BGM 设置")]
        [Tooltip("可选：Awake 时自动循环播放的背景音乐；留空则由代码调 PlayBGM 或 Inspector 其他时机播放")]
        [SerializeField] private AudioClip autoBgmClip;

        [Tooltip("BGM 音量 0~1")]
        [Range(0f, 1f)] [SerializeField] private float bgmVolume = 0.5f;

        [Header("音效设置")]
        [Tooltip("SFX 总音量 0~1（所有音效的最终音量都乘它）")]
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

        [Tooltip("SFX 播放器数量（同一时刻可重叠播放的音效条数，用完轮转复用最旧的）")]
        [Range(1, 16)] [SerializeField] private int sfxSourceCount = 8;

        [Header("BGM 淡入淡出")]
        [Tooltip("切换 / 停止 BGM 时的淡入淡出时长（秒）；0 = 立即切换")]
        [SerializeField] private float bgmFadeSeconds = 1f;

        [Header("事件音效绑定")]
        [Tooltip("事件名 → 音效 绑定列表：OnEnable 自动订阅 EventCenter，事件触发即播放")]
        [SerializeField] private List<EventSoundBinding> eventSounds = new List<EventSoundBinding>();

        // ==================== 运行时状态 ====================

        /// <summary>BGM 播放器（Awake 自动创建，loop 循环）</summary>
        public AudioSource BgmSource { get; private set; }

        /// <summary>BGM 音量（赋值即生效）</summary>
        public float BgmVolume
        {
            get => bgmVolume;
            set
            {
                bgmVolume = Mathf.Clamp01(value);
                if (BgmSource != null && !isBgmFading)
                    BgmSource.volume = bgmVolume;
            }
        }

        /// <summary>SFX 总音量（对之后播放的音效立即生效）</summary>
        public float SfxVolume
        {
            get => sfxVolume;
            set => sfxVolume = Mathf.Clamp01(value);
        }

        /// <summary>BGM 静音开关（不暂停播放，只是无声）</summary>
        public bool BgmMute
        {
            get => BgmSource != null && BgmSource.mute;
            set { if (BgmSource != null) BgmSource.mute = value; }
        }

        /// <summary>当前 BGM 是否正在播放</summary>
        public bool IsBgmPlaying => BgmSource != null && BgmSource.isPlaying;

        // 淡入淡出期间音量由协程接管，避免属性赋值打断动画
        private bool isBgmFading;
        private readonly List<AudioSource> sfxPool = new List<AudioSource>();
        private int sfxRoundRobinIndex;

        // 每条绑定生成的四组回调（反订阅必须用同一委托实例）
        private readonly List<BoundHandlers> boundHandlers = new List<BoundHandlers>();

        private class BoundHandlers
        {
            public Action NoArg;
            public Action<string> OneArg;
            public Action<string, string> TwoArg;
            public Action<string, string, string> ThreeArg;
        }

        // ==================== 生命周期 ====================

        protected override void Awake()
        {
            base.Awake();
            // 基类判重：重复实例被 Destroy 后不再初始化
            if (Instance != this)
                return;

            EnsureSources();

            if (autoBgmClip != null)
                PlayBGM(autoBgmClip);
        }

        private void OnEnable()
        {
            SubscribeEventSounds();
        }

        private void OnDisable()
        {
            UnsubscribeEventSounds();
        }

        // ==================== 播放源管理 ====================

        /// <summary>确保 BGM / SFX 播放器存在（运行时自动创建，无需在 Inspector 摆 AudioSource）</summary>
        private void EnsureSources()
        {
            if (BgmSource == null)
            {
                BgmSource = CreateSource("BGM");
                BgmSource.loop = true;
                BgmSource.volume = bgmVolume;
            }

            if (sfxPool.Count == 0)
            {
                // 场景旧组件反序列化可能得到 0，兜底至少 1 个
                int count = Mathf.Max(1, sfxSourceCount);
                for (int i = 0; i < count; i++)
                    sfxPool.Add(CreateSource("SFX_" + i));
            }
        }

        private AudioSource CreateSource(string childName)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D：UI / 全局音效不随距离衰减
            return source;
        }

        /// <summary>取一个 SFX 播放器：优先空闲的，全忙则轮转复用最旧的</summary>
        private AudioSource GetSfxSource()
        {
            for (int i = 0; i < sfxPool.Count; i++)
            {
                if (!sfxPool[i].isPlaying)
                    return sfxPool[i];
            }

            var source = sfxPool[sfxRoundRobinIndex % sfxPool.Count];
            sfxRoundRobinIndex++;
            return source;
        }

        // ==================== BGM ====================

        /// <summary>
        /// 播放背景音乐（循环）。同一首已在播且 restartIfSame=false 时不打断。
        /// 切换时先淡出旧曲再淡入新曲（时长见 BgmFadeSeconds）。
        /// </summary>
        public void PlayBGM(AudioClip clip, bool restartIfSame = false)
        {
            if (clip == null || BgmSource == null)
                return;

            if (BgmSource.clip == clip && BgmSource.isPlaying && !restartIfSame)
                return;

            StopCoroutineSafe();
            StartCoroutine(BGMSwitchRoutine(clip));
        }

        /// <summary>停止背景音乐（带淡出）</summary>
        public void StopBGM()
        {
            if (BgmSource == null)
                return;

            StopCoroutineSafe();
            StartCoroutine(BGMStopRoutine());
        }

        /// <summary>暂停背景音乐（无淡出，可 Resume）</summary>
        public void PauseBGM()
        {
            if (BgmSource != null && BgmSource.isPlaying)
                BgmSource.Pause();
        }

        /// <summary>恢复被暂停的背景音乐</summary>
        public void ResumeBGM()
        {
            if (BgmSource != null && !BgmSource.isPlaying && BgmSource.clip != null)
                BgmSource.UnPause();
        }

        private void StopCoroutineSafe()
        {
            // 停掉正在进行的淡入淡出协程，防止与新动画叠加抢音量
            isBgmFading = false;
            StopAllCoroutines();
        }

        private IEnumerator BGMSwitchRoutine(AudioClip clip)
        {
            float fade = Mathf.Max(0f, bgmFadeSeconds);
            isBgmFading = true;

            // 旧曲淡出
            if (BgmSource.isPlaying && fade > 0f)
                yield return FadeRoutine(BgmSource.volume, 0f, fade);

            BgmSource.clip = clip;
            BgmSource.loop = true;
            BgmSource.Play();

            // 新曲淡入
            if (fade > 0f)
                yield return FadeRoutine(0f, bgmVolume, fade);

            isBgmFading = false;
        }

        private IEnumerator BGMStopRoutine()
        {
            float fade = Mathf.Max(0f, bgmFadeSeconds);
            isBgmFading = true;

            if (BgmSource.isPlaying && fade > 0f)
                yield return FadeRoutine(BgmSource.volume, 0f, fade);

            BgmSource.Stop();
            BgmSource.clip = null;
            BgmSource.volume = bgmVolume;
            isBgmFading = false;
        }

        private IEnumerator FadeRoutine(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                BgmSource.volume = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            BgmSource.volume = to;
        }

        // ==================== SFX ====================

        /// <summary>
        /// 播放一个音效（可多条重叠，互不打断）。
        /// 最终音量 = SfxVolume × volumeScale。
        /// </summary>
        public void PlaySFX(AudioClip clip, float volumeScale = 1f, float pitch = 1f)
        {
            if (clip == null)
                return;

            EnsureSources();

            var source = GetSfxSource();
            source.pitch = pitch;
            source.PlayOneShot(clip, Mathf.Clamp01(volumeScale) * sfxVolume);
        }

        /// <summary>停止所有正在播放的音效（不影响 BGM）</summary>
        public void StopAllSFX()
        {
            foreach (var source in sfxPool)
                source.Stop();
        }

        // ==================== 事件音效绑定 ====================

        private void SubscribeEventSounds()
        {
            if (eventSounds == null)
                return;

            foreach (var binding in eventSounds)
            {
                if (binding == null || string.IsNullOrEmpty(binding.EventName) || binding.Clip == null)
                    continue;

                // 闭包捕获当前条目
                var b = binding;
                var handlers = new BoundHandlers
                {
                    NoArg = () => PlaySFX(b.Clip, b.Volume),
                    OneArg = arg => { if (MatchArg(b, arg)) PlaySFX(b.Clip, b.Volume); },
                    TwoArg = (arg1, _) => { if (MatchArg(b, arg1)) PlaySFX(b.Clip, b.Volume); },
                    ThreeArg = (arg1, _, _) => { if (MatchArg(b, arg1)) PlaySFX(b.Clip, b.Volume); }
                };

                // 同一事件名只会有一种签名被触发（EventCenter 按签名分发），四种全订阅不会重复播放
                EventCenter.Subscribe(b.EventName, handlers.NoArg);
                EventCenter.Subscribe<string>(b.EventName, handlers.OneArg);
                EventCenter.Subscribe<string, string>(b.EventName, handlers.TwoArg);
                EventCenter.Subscribe<string, string, string>(b.EventName, handlers.ThreeArg);

                boundHandlers.Add(handlers);
            }
        }

        private void UnsubscribeEventSounds()
        {
            if (eventSounds == null)
                return;

            for (int i = 0; i < eventSounds.Count && i < boundHandlers.Count; i++)
            {
                var eventName = eventSounds[i] != null ? eventSounds[i].EventName : null;
                if (string.IsNullOrEmpty(eventName))
                    continue;

                var handlers = boundHandlers[i];
                EventCenter.Unsubscribe(eventName, handlers.NoArg);
                EventCenter.Unsubscribe<string>(eventName, handlers.OneArg);
                EventCenter.Unsubscribe<string, string>(eventName, handlers.TwoArg);
                EventCenter.Unsubscribe<string, string, string>(eventName, handlers.ThreeArg);
            }

            boundHandlers.Clear();
        }

        /// <summary>string 首参与绑定关键字匹配（关键字为空 = 一律匹配）</summary>
        private static bool MatchArg(EventSoundBinding binding, string arg)
        {
            if (string.IsNullOrEmpty(binding.ArgContains))
                return true;
            return arg != null && arg.Contains(binding.ArgContains);
        }
    }
}
