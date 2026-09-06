using System.Collections;
using Framwork;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 结算面板 UI：植物进入第三阶段（成熟期）后延迟 1.5 秒激活结算面板，
/// 面板提供「重新开始」与「退出游戏」两个按钮（面板本体自己做，本脚本只负责激活与按钮逻辑）。
/// 挂在场景里任意【激活的】物体上（建议与 GameOverUI 同一个物体，不要挂在失活面板自己身上），
/// 然后在 Inspector 把做好的失活面板拖进 Settlement Panel 字段即可。
///
/// 触发链路：
///   Plant.OnStageComplete（第二阶段长满进入第三阶段）
///     → EventCenter 触发 PlantStageChange(Plant, GrowthStage.Mature)
///     → 本类订阅 → 等待 ShowDelay 秒（默认 1.5，走未缩放时间，不怕暂停）
///     → 激活 Settlement Panel + 暂停游戏
///
/// 按钮行为：
///   - 重新开始：恢复时间流速 → 重新加载当前场景（背包/植物/天气等全部回到初始状态）
///   - 退出游戏：退出程序（编辑器下停止 Play 模式）
///
/// 按钮可不手动指定：留空时自动在面板下按名字找
/// （含"重新开始 / Restart / Retry"的按钮 = 重新开始；含"退出 / Quit / Exit"的按钮 = 退出游戏）。
///
/// 面板内容（同样可留空自动找）：
///   - 植物第三阶段图片（名字含 Plant / 植物的物体上的 Image）：弹出时自动填入植物当前贴图
///   - 名称标题（名字含 Title / 标题 / 名称 的 TMP 或 Legacy Text）：弹出时自动填入最终形态中文名
///
/// 边界处理：
///   - 延迟等待期间植物死亡（勾选了 MatureRequiresResources 的旧玩法）→ 取消本次结算，让 GameOverUI 接管
///   - 已死亡/已结算后进入第三阶段 → 不再弹结算面板
/// </summary>
public class SettlementUI : MonoBehaviour
{
    [Header("结算面板")]
    [Tooltip("结算面板（保持失活，植物进入第三阶段并延迟后自动激活）")]
    [SerializeField] private GameObject settlementPanel;

    [Header("延迟")]
    [Tooltip("进入第三阶段后到弹出结算面板的等待秒数（未缩放时间）")]
    [SerializeField] private float showDelay = 1.5f;

    [Header("选项按钮（可留空自动查找）")]
    [Tooltip("重新开始按钮：重新加载当前场景；留空则自动按名字找")]
    [SerializeField] private Button restartButton;

    [Tooltip("退出游戏按钮：退出程序（编辑器下停止 Play）；留空则自动按名字找")]
    [SerializeField] private Button quitButton;

    [Header("其他")]
    [Tooltip("面板弹出时是否暂停游戏（Time.timeScale = 0）；重新开始/退出时会自动恢复")]
    [SerializeField] private bool pauseOnShow = true;

    [Header("面板内容（可留空自动查找）")]
    [Tooltip("植物第三阶段图片：弹出面板时自动填入植物当前（最终形态）贴图")]
    [SerializeField] private Image plantImage;

    [Tooltip("植物名称标题（TextMeshPro）：弹出面板时自动填入最终形态名称；与下面 Legacy Text 二选一")]
    [SerializeField] private TMP_Text titleTextTMP;

    [Tooltip("植物名称标题（Legacy Text）：弹出面板时自动填入最终形态名称")]
    [SerializeField] private Text titleText;

    /// <summary>本局是否已触发结算（防止重复触发）</summary>
    private bool _shown;

    /// <summary>延迟激活协程句柄（用于死亡时取消）</summary>
    private Coroutine _pendingShow;

    /// <summary>触发结算的植物（弹面板时取贴图和形态名称用）</summary>
    private Plant _plant;

    private void OnEnable()
    {
        EventCenter.Subscribe<Plant, GrowthStage>(EventName.PlantStageChange, OnPlantStageChange);
        EventCenter.Subscribe<Plant, GrowthStage>(EventName.PlantFailed, OnPlantFailed);
    }

    private void OnDisable()
    {
        EventCenter.Unsubscribe<Plant, GrowthStage>(EventName.PlantStageChange, OnPlantStageChange);
        EventCenter.Unsubscribe<Plant, GrowthStage>(EventName.PlantFailed, OnPlantFailed);
    }

    private void Start()
    {
        if (settlementPanel == null)
        {
            Debug.LogError("[SettlementUI] 未指定 Settlement Panel，植物进入第三阶段时无法弹出结算面板");
            return;
        }

        // 面板保持关闭（用户忘了失活也能兜住）
        settlementPanel.SetActive(false);

        // 面板内容兜底查找：未指定时按名字在面板下找
        if (plantImage == null || (titleTextTMP == null && titleText == null))
            AutoFindContent();

        // 按钮兜底查找：未指定时按名字在面板下找
        if (restartButton == null || quitButton == null)
            AutoFindButtons();

        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartClicked);
        else
            Debug.LogWarning("[SettlementUI] 未找到重新开始按钮");

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);
        else
            Debug.LogWarning("[SettlementUI] 未找到退出游戏按钮");
    }

    private void OnDestroy()
    {
        if (restartButton != null) restartButton.onClick.RemoveListener(OnRestartClicked);
        if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitClicked);
    }

    // ==================== 事件回调 ====================

    /// <summary>植物阶段变化：进入第三阶段（成熟期）→ 延迟后弹出结算面板</summary>
    private void OnPlantStageChange(Plant plant, GrowthStage newStage)
    {
        if (_shown || newStage != GrowthStage.Mature) return;

        // 已死亡 / 已完全成熟的植物进入第三阶段不结算（正常流程不会出现，防御一下）
        if (plant != null && (plant.IsDead || plant.IsFullyGrown)) return;

        _shown = true;
        _plant = plant;
        Debug.Log($"[SettlementUI] 植物进入第三阶段（成熟期），{showDelay:F1} 秒后弹出结算面板");
        _pendingShow = StartCoroutine(ShowAfterDelay());
    }

    /// <summary>植物死亡：取消还在等待中的结算，让 GameOverUI 接管</summary>
    private void OnPlantFailed(Plant plant, GrowthStage failedStage)
    {
        if (_pendingShow == null) return;

        StopCoroutine(_pendingShow);
        _pendingShow = null;
        _shown = false;
        Debug.Log("[SettlementUI] 等待期间植物死亡，取消结算面板，交给 GameOverUI 处理");
    }

    /// <summary>延迟激活协程：用未缩放时间计时，期间即使被暂停也能正常弹出</summary>
    private IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSecondsRealtime(showDelay);
        _pendingShow = null;

        Debug.Log("[SettlementUI] 弹出结算面板");
        if (settlementPanel != null)
            settlementPanel.SetActive(true);

        // 填充面板内容：植物第三阶段图片 + 名称标题
        FillPanelContent();

        if (pauseOnShow)
            Time.timeScale = 0f;
    }

    // ==================== 面板内容 ====================

    /// <summary>
    /// 填充面板内容：
    /// - 图片：取植物 SpriteRenderer 当前贴图（进入第三阶段时 PlantVisualChanger 已换成最终形态贴图）
    /// - 名称：按最终形态显示中文名（仙人掌 / 多肉植物 / 薰衣草 / 绿萝 / 龟背竹 / 捕蝇草）
    /// </summary>
    private void FillPanelContent()
    {
        if (_plant == null) return;

        // 名称标题：最终形态 → 中文名（进入第三阶段时形态一定已锁定，None 仅作兜底）
        string plantName = _plant.FinalForm switch
        {
            PlantFinalForm.Cactus    => "仙人掌",
            PlantFinalForm.Succulent => "多肉植物",
            PlantFinalForm.Lavender  => "薰衣草",
            PlantFinalForm.Pothos    => "绿萝",
            PlantFinalForm.Monstera  => "龟背竹",
            PlantFinalForm.Flytrap   => "捕蝇草",
            _ => _plant.gameObject.name,
        };

        if (titleTextTMP != null) titleTextTMP.text = plantName;
        if (titleText != null) titleText.text = plantName;
        if (titleTextTMP == null && titleText == null)
            Debug.LogWarning("[SettlementUI] 未找到名称标题文本，植物名称没有地方显示：" + plantName);

        // 图片：植物当前显示的贴图就是第三阶段贴图
        if (plantImage != null)
        {
            var sr = _plant.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
                plantImage.sprite = sr.sprite;
            else
                Debug.LogWarning("[SettlementUI] 植物身上没有 SpriteRenderer 或贴图为空，结算面板图片未更新");
        }
    }

    /// <summary>
    /// 按名字自动在面板下找内容控件：
    /// 名字含"Plant / 植物"的物体上的 Image = 植物图片；
    /// 名字含"Title / 标题 / 名称"的物体上的 TMP_Text / Legacy Text = 名称标题。
    /// </summary>
    private void AutoFindContent()
    {
        if (settlementPanel == null) return;

        if (plantImage == null)
        {
            foreach (var img in settlementPanel.GetComponentsInChildren<Image>(true))
            {
                string n = img.gameObject.name.ToLower();
                if (n.Contains("plant") || img.gameObject.name.Contains("植物"))
                {
                    plantImage = img;
                    break;
                }
            }
        }

        if (titleTextTMP == null && titleText == null)
        {
            foreach (var tmp in settlementPanel.GetComponentsInChildren<TMP_Text>(true))
            {
                string n = tmp.gameObject.name.ToLower();
                if (n.Contains("title") || tmp.gameObject.name.Contains("标题") || tmp.gameObject.name.Contains("名称"))
                {
                    titleTextTMP = tmp;
                    break;
                }
            }

            if (titleTextTMP == null)
            {
                foreach (var txt in settlementPanel.GetComponentsInChildren<Text>(true))
                {
                    string n = txt.gameObject.name.ToLower();
                    if (n.Contains("title") || txt.gameObject.name.Contains("标题") || txt.gameObject.name.Contains("名称"))
                    {
                        titleText = txt;
                        break;
                    }
                }
            }
        }
    }

    // ==================== 按钮行为 ====================

    /// <summary>重新开始：恢复时间流速并重载当前场景</summary>
    private void OnRestartClicked()
    {
        Time.timeScale = 1f;
        Debug.Log("[SettlementUI] 重新开始：重载场景 " + SceneManager.GetActiveScene().name);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>退出游戏：编辑器下停止 Play，打包后退出程序</summary>
    private void OnQuitClicked()
    {
        Time.timeScale = 1f;
        Debug.Log("[SettlementUI] 退出游戏");

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
        if (settlementPanel == null) return;

        // GetComponentInChildren(true) 包含失活子物体，面板此时是关闭的也能找到
        foreach (var btn in settlementPanel.GetComponentsInChildren<Button>(true))
        {
            string n = btn.gameObject.name;
            if (restartButton == null && (n.Contains("重新开始") || n.ToLower().Contains("restart") || n.ToLower().Contains("retry")))
                restartButton = btn;
            else if (quitButton == null && (n.Contains("退出") || n.ToLower().Contains("quit") || n.ToLower().Contains("exit")))
                quitButton = btn;
        }
    }
}
