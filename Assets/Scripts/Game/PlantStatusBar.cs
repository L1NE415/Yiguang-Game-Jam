using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 植物状态指示条 UI：把 <see cref="Plant"/> 的三项资源（水分/阳光/养分，0~100）
/// 实时显示为三根 Image Filled 模式的进度条。
///
/// 挂载位置：建议挂在状态条父物体上（如 PlantStatusBar），也支持挂任意 UI 节点。
///
/// 绑定方式（二选一）：
/// 1. Inspector 里把三根 Filled Image（各条目下名为 Fill 的子物体）拖到对应字段；
/// 2. 留空自动查找（AutoFind）：按名字在子物体里找 Water / Sunlight / Nutrition(或 Nutrient)
///    分组，再取其下名为 Fill 的 Filled Image。
///
/// 刷新策略：每帧读取并赋值 fillAmount（资源本身是逐帧消耗的，逐帧同步最直观）。
///
/// 低值预警：warnBelowRequirement 开启时，某项资源低于当前阶段需求门槛
/// （如缺水），对应条会变 warnColor 提示玩家"这项不达标"，达标后自动恢复原色。
///
/// 用法示例：
/// <code>
/// // 把三根 Fill Image 拖进 Inspector 即可，无需任何代码调用。
/// </code>
/// </summary>
[DisallowMultipleComponent]
public class PlantStatusBar : MonoBehaviour
{
    [Header("植物引用")]
    [Tooltip("植物逻辑组件：留空时自动在自身/父物体/场景里查找")]
    [SerializeField] private Plant plant;

    [Header("三根进度条（Image 的 Filled 模式，留空按名字自动查找）")]
    [Tooltip("水分条 Fill，映射 Plant.Water")]
    [SerializeField] private Image waterFill;

    [Tooltip("阳光条 Fill，映射 Plant.Sunlight")]
    [SerializeField] private Image sunlightFill;

    [Tooltip("养分条 Fill，映射 Plant.Nutrient")]
    [SerializeField] private Image nutrientFill;

    [Header("低值预警（可选）")]
    [Tooltip("低于当前阶段需求门槛时，对应进度条变 warnColor 提示")]
    [SerializeField] private bool warnBelowRequirement = true;

    [Tooltip("预警颜色（默认警示红）")]
    [SerializeField] private Color warnColor = new Color(1f, 0.25f, 0.2f, 1f);

    /// <summary>各进度条的原始颜色（恢复预警色用）</summary>
    private readonly Dictionary<Image, Color> _baseColors = new Dictionary<Image, Color>();

    private void Reset()
    {
        AutoFindReferences();
    }

    private void Awake()
    {
        AutoFindReferences();
        CaptureBaseColors();
    }

    private void Update()
    {
        // plant 没找到/被销毁时再找一次，仍找不到就静默跳过
        if (plant == null)
        {
            AutoFindReferences();
            if (plant == null) return;
        }

        var req = plant.CurrentRequirement;

        // 三项资源都是 0~100，fillAmount 归一化到 0~1
        if (waterFill != null)
        {
            waterFill.fillAmount = Mathf.Clamp01(plant.Water / 100f);
            ApplyWarn(waterFill, plant.Water, req.MinWater);
        }

        if (sunlightFill != null)
        {
            sunlightFill.fillAmount = Mathf.Clamp01(plant.Sunlight / 100f);
            ApplyWarn(sunlightFill, plant.Sunlight, req.MinSunlight);
        }

        if (nutrientFill != null)
        {
            nutrientFill.fillAmount = Mathf.Clamp01(plant.Nutrient / 100f);
            ApplyWarn(nutrientFill, plant.Nutrient, req.MinNutrient);
        }
    }

    // ==================== 内部实现 ====================

    /// <summary>低于需求门槛时染预警色，达标后恢复原色</summary>
    private void ApplyWarn(Image fill, float value, float minRequired)
    {
        if (!warnBelowRequirement || fill == null) return;

        bool insufficient = value < minRequired;
        Color baseColor;
        if (!_baseColors.TryGetValue(fill, out baseColor)) return;

        fill.color = insufficient ? warnColor : baseColor;
    }

    /// <summary>记录每根进度条的原始颜色（只记一次，避免预警后丢失原色）</summary>
    private void CaptureBaseColors()
    {
        TryCapture(waterFill);
        TryCapture(sunlightFill);
        TryCapture(nutrientFill);
    }

    private void TryCapture(Image fill)
    {
        if (fill != null && !_baseColors.ContainsKey(fill))
            _baseColors[fill] = fill.color;
    }

    /// <summary>查找未指定的 Plant 与三根进度条引用</summary>
    private void AutoFindReferences()
    {
        if (plant == null)
        {
            plant = GetComponent<Plant>();
            if (plant == null) plant = GetComponentInParent<Plant>(true);
            if (plant == null)
            {
                // 场景里找第一株活着的植物（单株游戏足够；多株时请在 Inspector 手动指定）
                foreach (var p in FindObjectsByType<Plant>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    plant = p;
                    break;
                }
            }
        }

        if (waterFill == null) waterFill = FindFill("Water");
        if (sunlightFill == null) sunlightFill = FindFill("Sunlight");
        if (nutrientFill == null)
        {
            nutrientFill = FindFill("Nutrition");
            if (nutrientFill == null) nutrientFill = FindFill("Nutrient");
        }
    }

    /// <summary>
    /// 按名字找到资源分组（如 Water），再取其子物体里名为 Fill 的 Filled Image。
    /// 分组物体本身是 Filled Image 时直接使用。
    /// </summary>
    private Image FindFill(string groupName)
    {
        if (string.IsNullOrEmpty(groupName)) return null;

        // 1. 找分组节点（自身或子孙按名字匹配）
        Transform group = FindTransformByName(transform, groupName);
        if (group == null) return null;

        // 2. 分组本身是 Filled Image → 直接用
        var selfImage = group.GetComponent<Image>();
        if (selfImage != null && selfImage.type == Image.Type.Filled)
            return selfImage;

        // 3. 找分组下名为 Fill 的 Filled Image（子孙递归）
        foreach (var img in group.GetComponentsInChildren<Image>(true))
        {
            if (img != null && img.type == Image.Type.Filled && img.name == "Fill")
                return img;
        }

        // 4. 兜底：分组下任意 Filled Image
        foreach (var img in group.GetComponentsInChildren<Image>(true))
        {
            if (img != null && img.type == Image.Type.Filled)
                return img;
        }

        return null;
    }

    /// <summary>按名字（忽略大小写）在自身与子孙里查找 Transform</summary>
    private static Transform FindTransformByName(Transform root, string name)
    {
        if (root.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
            return root;

        foreach (Transform child in root)
        {
            var found = FindTransformByName(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
