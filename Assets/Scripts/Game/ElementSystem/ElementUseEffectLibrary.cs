using System.Collections.Generic;
using Framwork;
using UnityEngine;

/// <summary>
/// 单个元素"被使用"事件的效果定义：
/// 对目标植物的水分 / 阳光 / 养分三项增减量（支持负数 = 削弱）。
/// </summary>
public class ElementUseEffect
{
    /// <summary>事件名（与元素资产上填写的 UseEventName 一致，如 UseLight / UseP01）</summary>
    public string EventName;

    /// <summary>天气/元素名称（日志显示用，如 晴天 / 烈日）</summary>
    public string WeatherName;

    /// <summary>水分变化量</summary>
    public float Water;

    /// <summary>阳光变化量</summary>
    public float Sunlight;

    /// <summary>养分变化量</summary>
    public float Nutrient;

    /// <summary>设计定位描述（日志显示用）</summary>
    public string Description;

    public override string ToString() =>
        $"{WeatherName}(水分{Water:+0;-#;0} 阳光{Sunlight:+0;-#;0} 养分{Nutrient:+0;-#;0}) {Description}";
}

/// <summary>
/// 元素使用事件库：集中存放全部 27 种元素（6 基础 B01~B06 + 21 特殊 P01~P21）
/// 的"被使用"事件内容——即对植物水分/阳光/养分的三项增减数值表。
///
/// 工作方式（全事件驱动，无需挂到场景）：
/// - 启动时（RuntimeInitializeOnLoadMethod）向 EventCenter 订阅全部事件名，
///   事件名与元素资产上的 UseEventName 一一对应（UseLight / UseWater / ... / UseP21）。
/// - 拖动元素图标到植物检测范围松手时，DragHandle 会触发
///   EventCenter.Trigger(element.UseEventName, element, plant)，
///   本库收到后按表把数值应用到 plant.ApplyWeatherEffect(...)。
/// - 新增元素效果只需在本文件的 Effects 表里加一行，不用改任何其他代码。
///
/// 数值来源：设计表「基础元素天气倾向 B01~B06」+「基础＋基础 21 种天气瓶 P01~P21」。
/// </summary>
public static class ElementUseEffectLibrary
{
    /// <summary>效果总表：事件名 → 效果（B01~B06 + P01~P21 共 27 条）</summary>
    private static readonly Dictionary<string, ElementUseEffect> Effects =
        new Dictionary<string, ElementUseEffect>
        {
            // ---------- 基础元素 B01~B06（每种基础元素的天气倾向） ----------
            { "UseLight",   new ElementUseEffect { EventName = "UseLight",   WeatherName = "晴天",   Water = -2, Sunlight = +6, Nutrient = +2,  Description = "强阳光，轻度失水" } },
            { "UseWater",   new ElementUseEffect { EventName = "UseWater",   WeatherName = "雨天",   Water = +6, Sunlight = -2, Nutrient = +2,  Description = "强补水，压低阳光" } },
            { "UseSand",    new ElementUseEffect { EventName = "UseSand",    WeatherName = "沙尘",   Water = -2, Sunlight = -1, Nutrient = +6,  Description = "偏养分，轻度缺水/遮光" } },
            { "UseWind",    new ElementUseEffect { EventName = "UseWind",    WeatherName = "大风",   Water = -2, Sunlight = +4, Nutrient = +2,  Description = "通风增光，轻度失水" } },
            { "UseThunder", new ElementUseEffect { EventName = "UseThunder", WeatherName = "雷雨",   Water = +3, Sunlight = -3, Nutrient = +7,  Description = "偏养分，兼顾少量水分" } },
            { "UseSnow",    new ElementUseEffect { EventName = "UseSnow",    WeatherName = "雪天",   Water = +4, Sunlight = -4, Nutrient = +2,  Description = "补水明显，阳光受压" } },

            // ---------- 特殊元素 P01~P21（基础＋基础合成的天气瓶） ----------
            { "UseP01", new ElementUseEffect { EventName = "UseP01", WeatherName = "烈日",     Water = -5, Sunlight = +9, Nutrient = +4,  Description = "极强阳光，明显失水" } },
            { "UseP02", new ElementUseEffect { EventName = "UseP02", WeatherName = "太阳雨",   Water = +3, Sunlight = +4, Nutrient = +4,  Description = "均衡型，适合救场" } },
            { "UseP03", new ElementUseEffect { EventName = "UseP03", WeatherName = "烈日扬沙", Water = -4, Sunlight = +6, Nutrient = +5,  Description = "高光＋养分，牺牲水分" } },
            { "UseP04", new ElementUseEffect { EventName = "UseP04", WeatherName = "干热风",   Water = -4, Sunlight = +8, Nutrient = +3,  Description = "高阳光，高失水" } },
            { "UseP05", new ElementUseEffect { EventName = "UseP05", WeatherName = "耀斑雷光", Water =  0, Sunlight = +4, Nutrient = +8,  Description = "偏养分爆发" } },
            { "UseP06", new ElementUseEffect { EventName = "UseP06", WeatherName = "冬日晴",   Water = +1, Sunlight = +3, Nutrient = +3,  Description = "温和均衡" } },
            { "UseP07", new ElementUseEffect { EventName = "UseP07", WeatherName = "暴雨",     Water = +9, Sunlight = -4, Nutrient = +3,  Description = "强补水，明显遮光" } },
            { "UseP08", new ElementUseEffect { EventName = "UseP08", WeatherName = "泥雨",     Water = +5, Sunlight = -3, Nutrient = +6,  Description = "水分＋养分双补" } },
            { "UseP09", new ElementUseEffect { EventName = "UseP09", WeatherName = "风雨",     Water = +6, Sunlight =  0, Nutrient = +3,  Description = "稳定补水" } },
            { "UseP10", new ElementUseEffect { EventName = "UseP10", WeatherName = "雷暴雨",   Water = +8, Sunlight = -4, Nutrient = +8,  Description = "强水分＋强养分" } },
            { "UseP11", new ElementUseEffect { EventName = "UseP11", WeatherName = "冻雨",     Water = +7, Sunlight = -5, Nutrient = +3,  Description = "高水分，强压阳光" } },
            { "UseP12", new ElementUseEffect { EventName = "UseP12", WeatherName = "沙尘暴",   Water = -4, Sunlight = -3, Nutrient = +9,  Description = "极偏养分，高风险" } },
            { "UseP13", new ElementUseEffect { EventName = "UseP13", WeatherName = "沙暴",     Water = -4, Sunlight = -1, Nutrient = +8,  Description = "偏养分，持续失水" } },
            { "UseP14", new ElementUseEffect { EventName = "UseP14", WeatherName = "雷沙暴",   Water = -2, Sunlight = -5, Nutrient = +10, Description = "最高养分爆发" } },
            { "UseP15", new ElementUseEffect { EventName = "UseP15", WeatherName = "沙雪",     Water = +2, Sunlight = -5, Nutrient = +6,  Description = "养分较高，阳光受压" } },
            { "UseP16", new ElementUseEffect { EventName = "UseP16", WeatherName = "狂风",     Water = -3, Sunlight = +7, Nutrient = +3,  Description = "高阳光，轻度失水" } },
            { "UseP17", new ElementUseEffect { EventName = "UseP17", WeatherName = "雷暴大风", Water = +2, Sunlight = -3, Nutrient = +9,  Description = "偏养分爆发" } },
            { "UseP18", new ElementUseEffect { EventName = "UseP18", WeatherName = "风雪",     Water = +3, Sunlight = -4, Nutrient = +4,  Description = "中等水分＋养分" } },
            { "UseP19", new ElementUseEffect { EventName = "UseP19", WeatherName = "连环雷",   Water = +5, Sunlight = -5, Nutrient = +10, Description = "极强养分，高风险" } },
            { "UseP20", new ElementUseEffect { EventName = "UseP20", WeatherName = "雷雪",     Water = +5, Sunlight = -5, Nutrient = +8,  Description = "水分＋养分双高" } },
            { "UseP21", new ElementUseEffect { EventName = "UseP21", WeatherName = "暴雪",     Water = +7, Sunlight = -5, Nutrient = +2,  Description = "强补水，明显压光" } },
        };

    /// <summary>
    /// 启动时自动订阅全部元素使用事件（不需要在场景里挂任何东西）。
    /// AfterSceneLoad 确保订阅常驻整个运行期；静态构造保证只初始化一次。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        // 触碰一下自身，保证静态构造（订阅）已完成
        _ = Effects.Count;
    }

    static ElementUseEffectLibrary()
    {
        foreach (var kv in Effects)
        {
            var effect = kv.Value;
            // 事件签名：(Element 使用的元素, Plant 目标植物)
            EventCenter.Subscribe<Element, Plant>(effect.EventName,
                (element, plant) => Apply(effect, element, plant));
        }
        Debug.Log($"[ElementUseEffectLibrary] 已订阅 {Effects.Count} 个元素使用事件（6 基础 + 21 特殊）");
    }

    /// <summary>按事件名查询效果（调试/其他系统复用；没有返回 null）</summary>
    public static ElementUseEffect GetEffect(string eventName)
        => eventName != null && Effects.TryGetValue(eventName, out var e) ? e : null;

    /// <summary>
    /// 把效果应用到目标植物（由事件回调触发）。
    /// plant 为空（不是对植物使用）时跳过；数值应用后打印一条日志方便核对。
    /// </summary>
    private static void Apply(ElementUseEffect effect, Element element, Plant plant)
    {
        if (plant == null)
        {
            Debug.LogWarning($"[ElementUseEffectLibrary] {effect.EventName} 触发但没有目标植物，效果未生效");
            return;
        }

        plant.ApplyWeatherEffect(effect.Water, effect.Sunlight, effect.Nutrient);
        Debug.Log($"[ElementUseEffectLibrary] {element.DisplayName} → {plant.name}：" +
                  $"水分{effect.Water:+0;-#;0} 阳光{effect.Sunlight:+0;-#;0} 养分{effect.Nutrient:+0;-#;0}" +
                  $"（{effect}） | 植物当前 水{plant.Water:F0}/阳{plant.Sunlight:F0}/养{plant.Nutrient:F0}");
    }
}
