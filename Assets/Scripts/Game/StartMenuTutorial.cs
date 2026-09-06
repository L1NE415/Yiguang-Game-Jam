using UnityEngine;
using UnityEngine.InputSystem;

namespace Framwork
{
    /// <summary>
    /// 开始菜单新手教程面板控制：
    /// - OpenTutorial / CloseTutorial：供 Button.onClick 调用，切换教程面板显隐
    /// - Update：教程面板打开时，按下键盘 Esc 等同于点击关闭
    ///
    /// 使用方式：挂在 Canvas 上（StartScene 已配置），TutorialPanel 拖入教程面板物体。
    /// 面板默认隐藏，打开时覆盖在菜单上层，关闭后回到开始菜单界面。
    /// </summary>
    public class StartMenuTutorial : MonoBehaviour
    {
        [Header("面板引用")]
        [Tooltip("新手教程面板（默认隐藏，打开时覆盖菜单）")]
        [SerializeField] private GameObject tutorialPanel;

        private void Update()
        {
            // Esc 关闭教程（项目使用 Input System 后端）
            if (tutorialPanel != null
                && tutorialPanel.activeSelf
                && Keyboard.current != null
                && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseTutorial();
            }
        }

        /// <summary>打开新手教程面板（接在教程按钮的 onClick 上）</summary>
        public void OpenTutorial()
        {
            if (tutorialPanel != null)
            {
                tutorialPanel.SetActive(true);
            }
        }

        /// <summary>关闭新手教程面板，回到开始菜单（接在关闭按钮的 onClick 上）</summary>
        public void CloseTutorial()
        {
            if (tutorialPanel != null)
            {
                tutorialPanel.SetActive(false);
            }
        }
    }
}
