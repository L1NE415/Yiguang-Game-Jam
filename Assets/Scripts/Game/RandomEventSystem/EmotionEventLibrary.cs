using System;
using System.Collections.Generic;

/// <summary>
/// 情绪元素 + 突发事件的文案与配置库（纯代码定义，零 Inspector 配置）。
/// 想调整事件文案 / 奖励 / 新增事件，直接改本文件的 EmotionElements 与 Events 两个列表即可。
///
/// 设计约定：
/// - 情绪元素资产存放在 Assets/Data/Emotion/（Element_Emotion_Aunt 等 6 个，Type = Basic 存基础背包），
///   由 RandomEventSystem 按列表发放；资产未配置时会回退为运行时动态创建。
/// - 事件分两类：普通事件（静默发放奖励）与特殊事件（额外触发 EventName.RandomEventTriggered 弹提示框）
/// - 事件编号 E01~E14；其中 E01/E03/E06/E07/E10/E14 为特殊事件，分别发放对应的 6 种情绪元素，
///   与"基础元素 × 情绪元素"合成表（Element_C01~C36）的列一一对应。
/// </summary>
public static class EmotionEventLibrary
{
    // ==================== 数据结构 ====================

    /// <summary>情绪元素定义（与 Assets/Data/Emotion/ 下的资产对应）</summary>
    public class EmotionElementDef
    {
        public string Id;
        public string DisplayName;
        public string Description;

        public EmotionElementDef(string id, string displayName, string description)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
        }
    }

    /// <summary>突发事件定义</summary>
    public class EmotionEvent
    {
        /// <summary>事件唯一标识（日志用）</summary>
        public string Id;

        /// <summary>是否为特殊事件（特殊事件触发提示框 UI）</summary>
        public bool IsSpecial;

        /// <summary>提示框标题（仅特殊事件使用）</summary>
        public string Title;

        /// <summary>事件文案（仅特殊事件显示在提示框中）</summary>
        public string Message;

        /// <summary>奖励的情绪元素 Id（对应 EmotionElements 里的条目）</summary>
        public string RewardId;

        public EmotionEvent(string id, bool isSpecial, string title, string message, string rewardId)
        {
            Id = id;
            IsSpecial = isSpecial;
            Title = title;
            Message = message;
            RewardId = rewardId;
        }
    }

    // ==================== 情绪元素表 ====================

    /// <summary>全部情绪元素定义（Id 是 RewardId 的查找键，新增元素后事件即可引用）</summary>
    public static readonly List<EmotionElementDef> EmotionElements = new List<EmotionElementDef>
    {
        new EmotionElementDef("Emotion_Aunt",   "找凌玲的阿姨", "工区最强传说：阿姨一开口，整个楼层都能听见。"),
        new EmotionElementDef("Emotion_Mom",    "妈妈的60秒",   "一条 60 秒的语音方阵，句句都是爱与牵挂。"),
        new EmotionElementDef("Emotion_CarKey", "五排车钥匙",   "握住它，今晚的车队就差你一个了。"),
        new EmotionElementDef("Emotion_Roach",  "工位蟑螂王",   "盘踞在键盘下的王，杀不死，只会更强。"),
        new EmotionElementDef("Emotion_Intern", "迷路实习生",  "入职第 N 天，仍在寻找正确的会议室。"),
        new EmotionElementDef("Emotion_Cat",    "云监工猫",     "云端在岗的猫监工，摸鱼会被记进小本本。"),
    };

    // ==================== 突发事件表 ====================

    /// <summary>
    /// 全部突发事件。普通事件（IsSpecial=false）只静默发放情绪元素；
    /// 特殊事件（IsSpecial=true）会弹出提示框显示 Title + Message（持续时长见 RandomEventToastUI）。
    /// </summary>
    public static readonly List<EmotionEvent> Events = new List<EmotionEvent>
    {
        // ---------- 特殊事件（弹提示框，编号与合成表列对应） ----------
        new EmotionEvent("E01", true, "找凌玲的阿姨",
            "阿姨拎着大袋子出现在工区，一嗓子喊出了凌玲的全名。\n获得了情绪元素「找凌玲的阿姨」。",   "Emotion_Aunt"),
        new EmotionEvent("E03", true, "妈妈的60秒",
            "妈妈发来一条 60 秒语音，你深吸一口气，点开了它。\n获得了情绪元素「妈妈的60秒」。",       "Emotion_Mom"),
        new EmotionEvent("E06", true, "五排车钥匙",
            "车队已经就位，就差你那把车钥匙了——上分之夜正式开始。\n获得了情绪元素「五排车钥匙」。", "Emotion_CarKey"),
        new EmotionEvent("E07", true, "工位蟑螂王",
            "键盘缝隙里，蟑螂王缓缓探出触角，与你对视了三秒。\n获得了情绪元素「工位蟑螂王」。",     "Emotion_Roach"),
        new EmotionEvent("E10", true, "迷路实习生",
            "迷路三天的实习生，终于推开了正确的那扇门。\n获得了情绪元素「迷路实习生」。",           "Emotion_Intern"),
        new EmotionEvent("E14", true, "云监工猫",
            "云监工猫上线了，屏幕角落的摄像头亮起绿光。\n获得了情绪元素「云监工猫」。",             "Emotion_Cat"),

        // ---------- 普通事件（静默发放） ----------
        new EmotionEvent("E02", false, "", "保洁阿姨拖过的地板，亮得能照出人影。",       "Emotion_Aunt"),
        new EmotionEvent("E04", false, "", "手机弹出妈妈发来的早安表情包。",             "Emotion_Mom"),
        new EmotionEvent("E05", false, "", "同事探过头来：“晚上五排，速来。”",           "Emotion_CarKey"),
        new EmotionEvent("E08", false, "", "打印机又卡纸了，不知道和那位“王”有没有关系。", "Emotion_Roach"),
        new EmotionEvent("E09", false, "", "实习生抱着笔记本电脑在走廊里小跑。",         "Emotion_Intern"),
        new EmotionEvent("E11", false, "", "监工猫在摄像头后面打了个哈欠。",             "Emotion_Cat"),
        new EmotionEvent("E12", false, "", "下班前的最后一小时，时间过得最慢。",         "Emotion_CarKey"),
        new EmotionEvent("E13", false, "", "茶水间的绿萝，又悄悄冒出了新芽。",           "Emotion_Mom"),
    };

    // ==================== 查询辅助 ====================

    /// <summary>按 Id 查找情绪元素定义（找不到返回 null 并警告）</summary>
    public static EmotionElementDef GetElementDef(string id)
    {
        foreach (var def in EmotionElements)
        {
            if (def.Id == id) return def;
        }
        UnityEngine.Debug.LogWarning($"[EmotionEventLibrary] 未找到情绪元素定义: {id}");
        return null;
    }
}
