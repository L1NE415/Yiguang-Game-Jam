using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 局内暂停面板：玩家按 Esc 打开/关闭暂停面板，面板上提供「继续游戏」与「结束游戏」两个按钮。
/// 挂在场景里任意【激活的】物体上（建议 Canvas 根，与 GameOverUI 同级），
/// 面板本身保持失活，由本类在按 Esc 时显隐切换。
///
/// 行为约定：
///   - Esc：面板关 → 打开并暂停（Time.timeScale = 0）；面板开 → 关闭并恢复
///   - 继续游戏按钮：关闭面板并恢复时间流速
///   - 结束游戏按钮：退出程序（编辑器下停止 Play 模式）
///
/// 与 GameOverUI / SettlementUI 的暂停互不干扰：
/// 打开时记录当时的 timeScale，关闭时原样恢复——游戏结束时已被置 0 的时间
/// 不会因为暂停面板的开关而被错误地恢复成 1。
/// </summary>
public class PauseUI : MonoBehaviour
{
    [Header("暂停面板")]
    [Tooltip("暂停面板（保持失活，按 Esc 时自动显隐）")]
    [SerializeField] private GameObject pausePanel;

    [Header("选项按钮（可留空自动查找）")]
    [Tooltip("继续游戏按钮：关闭面板并恢复时间流速；留空则自动按名字找")]
    [SerializeField] private Button continueButton;

    [Tooltip("结束游戏按钮：退出程序（编辑器下停止 Play）；留空则自动按名字找")]
    [SerializeField] private Button endButton;

    [Header("其他")]
    [Tooltip("打开面板时是否暂停游戏（Time.timeScale = 0）")]
    [SerializeField] private bool pauseOnOpen = true;

    /// <summary>面板当前是否打开</summary>
    private bool _isOpen;

    /// <summary>打开面板前的时间流速（关闭时原样恢复，不破坏其他系统的暂停状态）</summary>
    private float _timeScaleBeforePause = 1f;

    private void Start()
    {
        if (pausePanel == null)
        {
            Debug.LogError("[PauseUI] 未指定 Pause Panel，Esc 将无法打开暂停面板");
            enabled = false;
            return;
        }

        // 面板保持关闭（用户忘了失活也能兜住）
        pausePanel.SetActive(false);
        _isOpen = false;

        // 按钮兜底查找：未指定时按名字在面板下找
        if (continueButton == null || endButton == null)
            AutoFindButtons();

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);
        else
            Debug.LogWarning("[PauseUI] 未找到继续游戏按钮");

        if (endButton != null)
            endButton.onClick.AddListener(OnEndClicked);
        else
            Debug.LogWarning("[PauseUI] 未找到结束游戏按钮");
    }

    private void OnDestroy()
    {
        if (continueButton != null) continueButton.onClick.RemoveListener(OnContinueClicked);
        if (endButton != null) endButton.onClick.RemoveListener(OnEndClicked);
    }

    private void Update()
    {
        // Esc 切换暂停面板（项目为 Input System 后端，用 Keyboard API）
        if (pausePanel != null
            && Keyboard.current != null
            && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (_isOpen) ClosePause();
            else OpenPause();
        }
    }

    /// <summary>打开暂停面板并暂停游戏</summary>
    public void OpenPause()
    {
        if (_isOpen) return;
        _isOpen = true;

        _timeScaleBeforePause = Time.timeScale;
        if (pauseOnOpen)
            Time.timeScale = 0f;

        pausePanel.SetActive(true);
        Debug.Log("[PauseUI] 暂停");
    }

    /// <summary>关闭暂停面板并恢复时间流速（继续游戏按钮 / 再次按 Esc）</summary>
    public void ClosePause()
    {
        if (!_isOpen) return;
        _isOpen = false;

        Time.timeScale = _timeScaleBeforePause;
        pausePanel.SetActive(false);
        Debug.Log("[PauseUI] 继续游戏");
    }

    /// <summary>结束游戏：编辑器下停止 Play，打包后退出程序</summary>
    private void OnEndClicked()
    {
        Time.timeScale = 1f;
        Debug.Log("[PauseUI] 结束游戏");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnContinueClicked() => ClosePause();

    /// <summary>
    /// 按名字自动在面板下找两个按钮：
    /// 含"继续 / Continue"→ 继续游戏；含"结束 / End / 退出 / Quit / Exit"→ 结束游戏。
    /// </summary>
    private void AutoFindButtons()
    {
        if (pausePanel == null) return;

        foreach (var btn in pausePanel.GetComponentsInChildren<Button>(true))
        {
            string n = btn.gameObject.name.ToLower();
            if (continueButton == null && (n.Contains("continue") || n.Contains("继续")))
                continueButton = btn;
            else if (endButton == null && (n.Contains("end") || n.Contains("结束") || n.Contains("退出") || n.Contains("quit") || n.Contains("exit")))
                endButton = btn;
        }
    }
}
