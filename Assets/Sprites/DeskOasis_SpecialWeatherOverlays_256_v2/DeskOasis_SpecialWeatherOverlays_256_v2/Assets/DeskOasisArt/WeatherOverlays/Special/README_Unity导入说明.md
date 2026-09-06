# 工位绿洲｜15种特殊天气层

本目录含 `export.zip` 中15种混合天气瓶对应的天气效果层。每张为 256 × 256 RGBA 透明 PNG，不含瓶子、文字和背景，可直接覆盖在植物与生态瓶之间。

## Unity 6.3 LTS 导入设置

- Texture Type：Sprite (2D and UI)
- Sprite Mode：Single
- Alpha Is Transparency：开启
- Generate Mip Maps：关闭
- Wrap Mode：Clamp
- Filter Mode：Bilinear
- Compression：Normal Quality；透明边缘出现杂色时改为 High Quality
- Pixels Per Unit：按项目现有天气层设置保持一致

## 推荐层级

1. 生态瓶后层与土壤
2. 植物 Sprite
3. 本目录中的特殊天气层
4. `terrarium_glass_front` 玻璃前景层

天气层与生态瓶中心对齐，并通过包内 `Masks/terrarium_weather_mask_256.png` 或项目现有 UI Mask 限制在瓶体内部。所有天气层使用相同的 256 × 256 RectTransform，锚点和 Pivot 均设为 `(0.5, 0.5)`。

建议天气出现时用 0.25～0.4 秒淡入，停留约 0.8～1.5 秒，再用 0.4～0.7 秒淡出。雷类可以短暂闪烁一次；风、沙、雪类可轻微平移或旋转，但不要让图层越出遮罩。
