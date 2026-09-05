# 工位绿洲｜MVP 美术素材包

这是一套面向 Unity 6.3 LTS / WebGL 的首批可用素材，共 27 张生产图片，覆盖办公室背景、生态瓶、植物阶段、六种基础天气层、六种基础元素瓶和通用 UI。

## 使用方法

把整个 `DeskOasisArt` 文件夹拖入 Unity 项目的 `Assets` 目录。

建议导入设置：

- `Texture Type`: Sprite (2D and UI)
- `Sprite Mode`: Single
- `Alpha Is Transparency`: 开启（背景图除外）
- `Generate Mip Maps`: 关闭
- `Wrap Mode`: Clamp
- `Filter Mode`: Bilinear
- `Mesh Type`: Full Rect
- `Max Size`: 背景 2048；其他素材 1024；小 UI 可设 512
- WebGL 压缩：Normal Quality；出现透明边缘杂色时改为 High Quality

## 推荐层级

1. `bg_office_day`
2. 一至两个 `weather_*` 天气层
3. `terrarium_empty`（玻璃与土壤覆盖在天气前方）
4. 一个 `plant_*` 植物状态
5. UI

生态瓶、植物和天气层均使用 1024 × 1024 画布，中心坐标一致。将它们放进同一个 RectTransform，位置归零、尺寸相同即可叠加。

天气必须使用 `Masks/terrarium_weather_mask.png` 配合 UI Mask、Sprite Mask 或材质遮罩，避免粒子越出玻璃球。基础天气由两个 `weather_*` 层叠加得到；配方见 `Documentation/weather_recipe_manifest.csv`。

## 注意

- UI 底图没有烘焙文字；“合成”、资源数值和物品数量请用 TextMeshPro 添加。
- 这是 48 小时版本：植物先提供一套通用的种子、发芽、成熟、枯萎状态。其他植物品种可继续沿用这套画风扩展。
- 高清透明图是 AI 辅助统一制作，正式发布前建议由美术做一次线条和色彩终检。
