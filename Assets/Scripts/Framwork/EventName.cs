namespace Framwork
{
    /// <summary>
    /// 全局事件名常量集中定义，避免到处手写字符串导致拼写错误。
    /// Gamejam 过程中按需在这里追加事件名即可。
    /// </summary>
    public static class EventName
    {        
        // ---------- 植物 ----------

        /// <summary>植物生长阶段变化（参数：Plant 植物实例，GrowthStage 新阶段）</summary>
        public const string PlantStageChange = "PlantStageChange";

        /// <summary>植物完全成熟（参数：Plant 植物实例；三个阶段全部长满）</summary>
        public const string PlantFullyGrown = "PlantFullyGrown";

        /// <summary>
        /// 植物阶段超时失败（参数：Plant 植物实例，GrowthStage 失败的阶段）。
        /// 规定时间内未满足需求时触发，由游戏管理脚本订阅后判定游戏结束。
        /// </summary>
        public const string PlantFailed = "PlantFailed";

        /// <summary>植物被重置回种子阶段（参数：Plant 植物实例；收获后再种一轮 / 重开一局）</summary>
        public const string PlantReset = "PlantReset";

        /// <summary>
        /// 植物最终形态锁定（参数：Plant 植物实例，PlantFinalForm 锁定的形态）。
        /// 第二阶段期间某形态条件的属性区间连续保持超过判定时长时触发，锁定后不再变化。
        /// </summary>
        public const string PlantFinalFormDetermined = "PlantFinalFormDetermined";

        // ---------- 元素合成系统 ----------

        /// <summary>元素合成成功（参数：Element 原料A，Element 原料B，Element 产物）</summary>
        public const string ElementCombined = "ElementCombined";

        /// <summary>元素合成失败（参数：Element 原料A，Element 原料B）</summary>
        public const string ElementCombineFailed = "ElementCombineFailed";

        /// <summary>元素被使用（参数：Element 被使用的元素，Plant 目标植物；不是对植物使用时为 null）</summary>
        public const string ElementUsed = "ElementUsed";

        // ---------- 天气系统 ----------

        /// <summary>天气切换（参数：Weather 旧天气，Weather 新天气；开局第一个天气时旧天气为 null）</summary>
        public const string WeatherChanged = "WeatherChanged";

        /// <summary>天气开始（参数：Weather 本次天气，float 本次持续秒数）</summary>
        public const string WeatherStarted = "WeatherStarted";

        /// <summary>天气结束（参数：Weather 结束的天气）</summary>
        public const string WeatherEnded = "WeatherEnded";

        // ---------- 天气系统：各天气专属事件 ----------
        // 每种天气自己的事件名不在这里定义，直接以字符串填在 Weather 资产的
        // StartEventName / EndEventName 上，订阅方使用相同字符串即可，例如：
        // EventCenter.Subscribe<Weather>("WeatherStormStart", w => { ... });

        // ---------- Buff 系统 ----------

        /// <summary>Buff 生效（参数：PlantBuff 生效的 Buff，float 持续秒数；重复触发为刷新时长）</summary>
        public const string BuffApplied = "BuffApplied";

        /// <summary>Buff 结束（参数：PlantBuff 结束的 Buff，含到期与手动移除）</summary>
        public const string BuffRemoved = "BuffRemoved";

        // ---------- 背包系统 ----------

        /// <summary>背包加入元素（参数：Element 加入的元素，int 实际加入数量，int 加入后该元素总数）</summary>
        public const string BackpackItemAdded = "BackpackItemAdded";

        /// <summary>背包移除元素（参数：Element 移除的元素，int 实际移除数量，int 移除后该元素总数；归零时 newCount=0）</summary>
        public const string BackpackItemRemoved = "BackpackItemRemoved";

        /// <summary>背包任意变化的总开关（无参数；UI 刷新用这个最方便）</summary>
        public const string BackpackChanged = "BackpackChanged";

        // ---------- 突发事件系统 ----------

        /// <summary>
        /// 突发事件触发（参数：string 事件标题，string 提示文案）。
        /// 仅特殊事件触发（普通事件静默发放奖励），由提示框 UI 订阅后显示。
        /// </summary>
        public const string RandomEventTriggered = "RandomEventTriggered";
    }
}
