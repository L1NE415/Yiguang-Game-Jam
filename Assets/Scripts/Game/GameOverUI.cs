using Framwork;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 游戏结束面板 UI：植物死亡时激活面板，提供「重新开始」与「退出游戏」两个按钮。
/// 挂在场景里任意【激活的】物体上（建议 Canvas 根或专门的管理物体，不要挂在失活面板自己身上），
/// 然后在 Inspector 把做好的失活面板拖进 Game Over Panel 字段即可。
///
/// 触发链路：
///   Plant.FailCurrentStage（某阶段超时未满足需求，植物死亡）
///     → EventCenter 触发 PlantFailed(Plant, GrowthStage)
///     → 本类订阅 → 激活 Game Over Panel + 暂停游戏
///
/// 按钮行为：
///   - 重新开始：恢复时间流速 → 重新加载当前场景（背包/植物/天气等全部回到初始状态）
///   - 退出游戏：退出程序（编辑器下停止 Play 模式）
///
/// 按钮可不手动指定：留空时自动在面板下按名字找
/// （含"重新开始 / Restart / Retry"的按钮 = 重新开始；含"退出 / Quit / Exit"的按钮 = 退出游戏）。
/// </summary>
public class GameOverUI : MonoBehaviour
{
    [Header("游戏结束面板")]
    [Tooltip("游戏结束面板（保持失活，植物死亡时自动激活）")]
    [SerializeField] private GameObject gameOverPanel;

    [Header("选项按钮（可留空自动查找）")]
    [Tooltip("重新开始按钮：重新加载当前场景；留空则自动按名字找")]
    [SerializeField] private Button restartButton;

    [Tooltip("退出游戏按钮：退出程序（编辑器下停止 Play）；留空则自动按名字找")]
    [SerializeField] private Button quitButton;

    [Header("提示文本（两种任拖一个，也可都留空自动找）")]
    [Tooltip("游戏结束提示文本（TextMeshPro）：内容会按植物阶段 + 死因自动生成")]
    [SerializeField] private TMP_Text gameOverTextTMP;

    [Tooltip("游戏结束提示文本（Legacy Text）：内容会按植物阶段 + 死因自动生成")]
    [SerializeField] private Text gameOverText;

    [Header("其他")]
    [Tooltip("面板弹出时是否暂停游戏（Time.timeScale = 0）；重新开始/退出时会自动恢复")]
    [SerializeField] private bool pauseOnGameOver = true;

    /// <summary>本局是否已结束（防止多株植物重复触发）</summary>
    private bool _gameOver;

    private void OnEnable()
    {
        EventCenter.Subscribe<Plant, GrowthStage>(EventName.PlantFailed, OnPlantFailed);
    }

    private void OnDisable()
    {
        EventCenter.Unsubscribe<Plant, GrowthStage>(EventName.PlantFailed, OnPlantFailed);
    }

    private void Start()
    {
        if (gameOverPanel == null)
        {
            Debug.LogError("[GameOverUI] 未指定 Game Over Panel，植物死亡时无法弹出游戏结束面板");
            return;
        }

        // 面板保持关闭（用户忘了失活也能兜住）
        gameOverPanel.SetActive(false);

        // 按钮兜底查找：未指定时按名字在面板下找
        if (restartButton == null || quitButton == null)
            AutoFindButtons();

        // 提示文本兜底查找：未指定时取面板下第一个 TMP_Text / Legacy Text
        if (gameOverTextTMP == null && gameOverText == null)
            AutoFindTexts();

        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartClicked);
        else
            Debug.LogWarning("[GameOverUI] 未找到重新开始按钮");

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);
        else
            Debug.LogWarning("[GameOverUI] 未找到退出游戏按钮");
    }

    private void OnDestroy()
    {
        if (restartButton != null) restartButton.onClick.RemoveListener(OnRestartClicked);
        if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitClicked);
    }

    /// <summary>植物死亡 → 游戏结束</summary>
    private void OnPlantFailed(Plant plant, GrowthStage failedStage)
    {
        if (_gameOver) return;
        _gameOver = true;

        Debug.Log($"[GameOverUI] 植物在 {failedStage} 阶段死亡，游戏结束");

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        // 按植物阶段 + 死因生成提示文本
        SetGameOverText(BuildMessage(plant, failedStage));

        if (pauseOnGameOver)
            Time.timeScale = 0f;
    }

    // ==================== 提示文本生成 ====================

    /// <summary>写入提示文本：TMP 与 Legacy Text 谁有值写谁</summary>
    private void SetGameOverText(string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        if (gameOverTextTMP != null)
            gameOverTextTMP.text = message;
        if (gameOverText != null)
            gameOverText.text = message;

        if (gameOverTextTMP == null && gameOverText == null)
            Debug.LogWarning("[GameOverUI] 未指定提示文本，本条内容没有地方显示：" + message);
    }

    /// <summary>
    /// 按植物死亡时的阶段与死因生成提示文本。
    /// 死因取自 Plant.MissingResourceText（死亡瞬间未满足的第一项需求），
    /// 植物死亡后 Update 直接返回、资源不再变化，所以此时读取仍是死亡时刻的状态。
    /// </summary>
    private string BuildMessage(Plant plant, GrowthStage failedStage)
    {
        string stageName = failedStage switch
        {
            GrowthStage.Seed   => "种子期",
            GrowthStage.Sprout => "发芽期",
            GrowthStage.Mature => "成熟期",
            _ => failedStage.ToString(),
        };

        string stageFlavor = failedStage switch
        {
            GrowthStage.Seed   => "种子还没来得及发芽，就永远沉睡了……",
            GrowthStage.Sprout => "嫩芽刚探出头，却没能撑过这一关……",
            GrowthStage.Mature => "距离收获只剩最后一步，实在太可惜了……",
            _ => string.Empty,
        };

        // 死因：死亡瞬间第一项未满足的需求（缺水/缺阳光/缺养分）
        string cause = plant != null ? plant.MissingResourceText : string.Empty;
        string causeLine = string.IsNullOrEmpty(cause)
            ? "阶段限时结束，未能满足生长需求"
            : $"长时间「{cause}」，需求始终未能满足";

        // 该阶段最终生长进度（0~100%）
        string progressLine = plant != null
            ? $"本阶段生长进度：{Mathf.RoundToInt(plant.StageProgress * 100f)}%"
            : string.Empty;

        return $"游戏结束\n\n" +
               $"你的植物在「{stageName}」枯萎了\n" +
               $"{stageFlavor}\n\n" +
               $"死因：{causeLine}\n" +
               $"{progressLine}";
    }

    /// <summary>重新开始：恢复时间流速并重载当前场景</summary>
    private void OnRestartClicked()
    {
        Time.timeScale = 1f;
        Debug.Log("[GameOverUI] 重新开始：重载场景 " + SceneManager.GetActiveScene().name);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>退出游戏：编辑器下停止 Play，打包后退出程序</summary>
    private void OnQuitClicked()
    {
        Time.timeScale = 1f;
        Debug.Log("[GameOverUI] 退出游戏");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// 按名字自动在面板下找两个按钮：
    /// 含"重新开始 / Restart / Retry"→ 重新开始；含"退出 / Quit / Exit"→ 退出游戏。
    /// </summary>
    private void AutoFindButtons()
    {
        if (gameOverPanel == null) return;

        // GetComponentInChildren(true) 包含失活子物体，面板此时是关闭的也能找到
        foreach (var btn in gameOverPanel.GetComponentsInChildren<Button>(true))
        {
            string n = btn.gameObject.name;
            if (restartButton == null && (n.Contains("重新开始") || n.ToLower().Contains("restart") || n.ToLower().Contains("retry")))
                restartButton = btn;
            else if (quitButton == null && (n.Contains("退出") || n.ToLower().Contains("quit") || n.ToLower().Contains("exit")))
                quitButton = btn;
        }
    }

    /// <summary>提示文本兜底查找：优先 TMP_Text，没有再找 Legacy Text（含失活子物体）</summary>
    private void AutoFindTexts()
    {
        if (gameOverPanel == null) return;

        gameOverTextTMP = gameOverPanel.GetComponentInChildren<TMP_Text>(true);
        if (gameOverTextTMP == null)
            gameOverText = gameOverPanel.GetComponentInChildren<Text>(true);
    }
}
