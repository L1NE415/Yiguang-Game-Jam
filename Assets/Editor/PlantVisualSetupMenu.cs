using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 编辑器工具：为植物一键挂载 PlantVisualChanger 并自动填好阶段精灵。
///
/// 用法：
///   1. 在 Hierarchy 里选中要设置的植物 GameObject（需带 Plant + SpriteRenderer）
///   2. 菜单 Tools → 植物 → 为选中物体挂载阶段精灵（自动取图集）
///
/// 精灵来自 Assets/Sprites/Potato-Sheet.png（已切片为 4 帧）：
///   _0 = 种子  _1 = 发芽  _2 = 成熟  _3 = 枯萎
/// </summary>
public static class PlantVisualSetupMenu
{
    /// <summary>精灵图集路径（4 帧切片）</summary>
    private const string SheetPath = "Assets/Sprites/Potato-Sheet.png";

    [MenuItem("Tools/植物/▸ 为选中物体挂载阶段精灵（自动取图集）")]
    private static void SetupSelectedPlants()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected.Length == 0)
        {
            EditorUtility.DisplayDialog("植物可视化", "请先在 Hierarchy 里选中要设置的植物 GameObject（需包含 Plant 与 SpriteRenderer）。", "好的");
            return;
        }

        Sprite[] sprites = LoadSheetSprites();
        if (sprites == null || sprites.Length < 4)
        {
            EditorUtility.DisplayDialog("植物可视化", $"未能从 {SheetPath} 读取到 4 帧精灵，请检查图集是否已切片（Sprite Mode = Multiple）。", "好的");
            return;
        }

        int done = 0;
        foreach (GameObject go in selected)
        {
            var plant = go.GetComponent<Plant>() ?? go.GetComponentInChildren<Plant>(true);
            var sr = go.GetComponent<SpriteRenderer>() ?? go.GetComponentInChildren<SpriteRenderer>(true);
            if (plant == null || sr == null)
            {
                Debug.LogWarning($"[PlantVisualSetup] {go.name} 缺少 Plant 或 SpriteRenderer，已跳过", go);
                continue;
            }

            Undo.RegisterCompleteObjectUndo(go, "Setup Plant Visual");
            var vc = go.GetComponent<PlantVisualChanger>();
            if (vc == null) vc = Undo.AddComponent<PlantVisualChanger>(go);

            var so = new SerializedObject(vc);
            so.FindProperty("seedSprite").objectReferenceValue = sprites[0];
            so.FindProperty("sproutSprite").objectReferenceValue = sprites[1];
            so.FindProperty("matureSprite").objectReferenceValue = sprites[2];
            so.FindProperty("deadSprite").objectReferenceValue = sprites[3];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(vc);
            done++;
        }

        EditorUtility.DisplayDialog("植物可视化",
            $"已为 {done} 个植物挂载并填好阶段精灵。\n\n映射：种子=_0  发芽=_1  成熟=_2  枯萎=_3\n点击顶部 Play 即可看到植物随生长阶段自动换贴图。",
            "好的");
    }

    /// <summary>读取图集内所有切片精灵，按名字排序（_0.._3）</summary>
    private static Sprite[] LoadSheetSprites()
    {
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(SheetPath);
        return all != null ? all.OfType<Sprite>().OrderBy(s => s.name).ToArray() : null;
    }
}
