using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 植物状态指示条（血条式 UI）。
///
/// 挂到任意一个 GameObject 上（建议挂到 Canvas 下装状态条的父物体即可），
/// 负责把 <see cref="Plant"/> 的三项资源（0~100）实时显示成三根可填充的进度条。
///
/// 实现方式：使用三个 UnityEngine.UI.Slider（推荐），也能兼容 Image(Type=Filled) 方案——
/// 只要填 <see cref="waterBar"/>/<see cref="sunlightBar"/>/<see cref="nutrientBar"/> 即可，
/// 脚本统一按 Slider 的 0~1 归一化 value 去驱动。
///
/// 绑定方式（二选一）：
/// 1. 在 Inspector 里手动把三根 Slider 拖到对应字段；
/// 2. 留空自动查找（AutoFind）：按名字在子物体里找 WaterSlider/SunlightSlider/NutrientSlider，
///    找不到的项就跳过，不报错。
///
/// 刷新策略：每帧读取并赋值（Update 里同步）。资源值本身就是逐个 Update 消耗的，
/// 这里做每帧同步最直观；如果后续想省性能，可以改成订阅 BackpackChanged 之类的事件按需刷新。
///
/// 用法示例：
/// <code>
/// // 直接把三根 Slider 拖进 Inspector，或按上面的命名规则放在子物体下即可。
/// // 无需任何代码调用。
/// </code>
/// </summary>
[DisallowMultipleComponent]
public class PlantStatusBar : MonoBehaviour
{
    [Header("植物引用")]
    [Tooltip("植物逻辑组件：留空时自动在自身/子物体上查找")]
    [SerializeField] private Plant plant;

    [Header("进度条（推荐用 Slider，留空按名字自动查找）")]
    [Tooltip("水分条，映射 Plant.Water")]
    [SerializeField] private Slider waterBar;

    [Tooltip("阳光条，映射 Plant.Sunlight")]
    [SerializeField] private Slider sunlightBar;

    [Tooltip("养分条，映射 Plant.Nutrient")]
    [SerializeField] private Slider nutrientBar;

    private void Reset()
    {
        AutoFindReferences();
    }

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Update()
    {
        // plant 没找到/被销毁时静默跳过，不刷
        if (plant == null)
        {
            AutoFindReferences();
            if (plant == null) return;
        }

        // 三项资源都是 0~100，Slider 归一化到 0~1
        if (waterBar != null) waterBar.value = Mathf.Clamp01(plant.Water / 100f);
        if (sunlightBar != null) sunlightBar.value = Mathf.Clamp01(plant.Sunlight / 100f);
        if (nutrientBar != null) nutrientBar.value = Mathf.Clamp01(plant.Nutrient / 100f);
    }

    /// <summary>查找未指定的 Plant 与三根进度条引用</summary>
    private void AutoFindReferences()
    {
        if (plant == null) plant = GetComponent<Plant>();
        if (plant == null) plant = GetComponentInParent<Plant>(true);

        if (waterBar == null) waterBar = FindBar("Water");
        if (sunlightBar == null) sunlightBar = FindBar("Sunlight");
        if (nutrientBar == null) nutrientBar = FindBar("Nutrient");
    }

    /// <summary>按名字前缀在当前子树里查找 Slider；找不到返回 null</summary>
    private Slider FindBar(string keyword)
    {
        if (string.IsNullOrEmpty(keyword)) return null;

        foreach (var s in GetComponentsInChildren<Slider>(true))
        {
            if (s == null) continue;
            if (s.name.Contains(keyword, System.StringComparison.OrdinalIgnoreCase))
                return s;
        }
        return null;
    }
}
