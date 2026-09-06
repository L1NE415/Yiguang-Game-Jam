using UnityEngine;
using UnityEngine.UI;

namespace Framwork
{
    /// <summary>
    /// UI 按钮点击音效：挂在带 Button 的物体上，点击时播放指定音效。
    ///
    /// 使用方式：
    /// - 挂到任意带 Button 组件的 GameObject 上，
    /// - 把音效 AudioClip 拖到 Inspector 的 Click Clip 槽（已预配好 sfx_ui_click），
    /// - 无需手动配置 onClick 回调，Awake 时自动订阅 Button.onClick。
    ///
    /// 播放源：优先复用同物体上已有的 AudioSource，否则自动新建一个 2D 音源。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class UIButtonClickSfx : MonoBehaviour
    {
        [Header("点击音效")]
        [Tooltip("点击按钮时播放的音频")]
        [SerializeField] private AudioClip clickClip;

        [Tooltip("音量（0~1）")]
        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        private AudioSource source;
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();

            // 音效播放源：复用同物体上已有的，否则新建一个 2D AudioSource
            if (!TryGetComponent(out source))
            {
                source = gameObject.AddComponent<AudioSource>();
            }
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D，不随空间位置衰减

            button.onClick.AddListener(PlayClick);
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(PlayClick);
            }
        }

        /// <summary>播放一次点击音效</summary>
        public void PlayClick()
        {
            if (clickClip != null)
            {
                source.PlayOneShot(clickClip, volume);
            }
        }
    }
}
