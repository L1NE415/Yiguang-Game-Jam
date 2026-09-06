using UnityEngine;
using UnityEngine.SceneManagement;

namespace Framwork
{
    /// <summary>
    /// 主菜单（开始界面）按钮逻辑：
    /// - StartGame：切换到游戏主场景（默认 UI 场景）
    /// - QuitGame：退出游戏进程（编辑器下表现为停止 Play Mode）
    ///
    /// 使用方式：挂到按钮物体上，在 Button.onClick 里选择对应方法。
    /// StartScene 中的 Start / End 两个按钮已预先接好线。
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("场景配置")]
        [Tooltip("点击开始游戏后要加载的场景名（需已加入 Build Settings）")]
        [SerializeField] private string gameSceneName = "UI";

        /// <summary>开始游戏：切换到游戏主场景</summary>
        public void StartGame()
        {
            SceneManager.LoadScene(gameSceneName);
        }

        /// <summary>结束游戏：退出应用（编辑器下为停止 Play Mode）</summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
