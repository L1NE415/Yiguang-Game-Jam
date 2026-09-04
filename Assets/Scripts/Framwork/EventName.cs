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

        // ---------- 元素合成系统 ----------

        /// <summary>元素合成成功（参数：Element 原料A，Element 原料B，Element 产物）</summary>
        public const string ElementCombined = "ElementCombined";

        /// <summary>元素合成失败（参数：Element 原料A，Element 原料B）</summary>
        public const string ElementCombineFailed = "ElementCombineFailed";

        /// <summary>元素被使用（参数：Element 被使用的元素）</summary>
        public const string ElementUsed = "ElementUsed";
    }
}
