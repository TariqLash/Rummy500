using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

#if UNITY_EDITOR
/// <summary>
/// Renames sliced sprites on the card tilesheet to a consistent naming convention.
///
/// Sheet layout (15 cols × 5 rows):
///   Row 0: A–K of Hearts (cols 0-12), Joker1 (col 13), Joker2 (col 14)
///   Row 1: A–K of Diamonds (cols 0-12), empty (cols 13-14)
///   Row 2: A–K of Spades   (cols 0-12), empty (cols 13-14)
///   Row 3: A–K of Clubs    (cols 0-12), empty (cols 13-14)
///   Row 4: card_back_1…card_back_8 (cols 0-7), empty (cols 8-14)
///
/// Usage:
///   1. Import sheet: Texture Type = Sprite (2D and UI), Sprite Mode = Multiple.
///   2. Sprite Editor → Slice → Grid By Cell Count → C=15 R=5 → Slice → Apply.
///   3. Select the texture asset, then Menu: Rummy500 → Rename Card Sheet Sprites.
/// </summary>
public static class CardSheetRenamer
{
    const int NumCols = 13;
    const int NumRows = 4;

    static readonly string[] SuitByRow  = { "Hearts", "Diamonds", "Spades", "Clubs" }; // rows 0-3
    static readonly string[] RankByCol  = { "A","2","3","4","5","6","7","8","9","10","J","Q","K" }; // cols 0-12

    [MenuItem("Rummy500/Rename Card Sheet Sprites")]
    static void RenameSprites()
    {
        var tex = Selection.activeObject as Texture2D;
        if (tex == null)
        {
            EditorUtility.DisplayDialog("Card Sheet Renamer",
                "Select the card tilesheet texture in the Project panel first.", "OK");
            return;
        }

        var path     = AssetDatabase.GetAssetPath(tex);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null || importer.spriteImportMode != SpriteImportMode.Multiple)
        {
            EditorUtility.DisplayDialog("Card Sheet Renamer",
                "Texture must have Sprite Mode = Multiple and be sliced first.\n" +
                "Sprite Editor → Slice → Grid By Cell Count → C=15 R=5.", "OK");
            return;
        }

        var sprites = importer.spritesheet;
        if (sprites.Length == 0)
        {
            EditorUtility.DisplayDialog("Card Sheet Renamer",
                "No sprites found. Click Apply in the Sprite Editor after slicing.", "OK");
            return;
        }

        float cellW = tex.width  / (float)NumCols;
        float cellH = tex.height / (float)NumRows;

        var renamed = new List<string>();
        var skipped = new List<string>();

        for (int i = 0; i < sprites.Length; i++)
        {
            var r       = sprites[i].rect;
            int col     = Mathf.FloorToInt(r.x / cellW + 0.01f);
            int rowFlip = Mathf.FloorToInt(r.y / cellH + 0.01f);
            int row     = NumRows - 1 - rowFlip;   // flip: Unity Y=0 is bottom

            string newName = null;

            if (row <= 3 && col <= 12)
            {
                // Card face
                newName = $"card_{RankByCol[col]}_{SuitByRow[row]}";
            }
            else if (row == 0 && col == 13) newName = "card_joker_1";
            else if (row == 0 && col == 14) newName = "card_joker_2";
            else if (row == 4 && col <= 7)  newName = $"card_back_{col + 1}";
            // else: empty cells — leave unchanged

            if (newName != null)
            {
                renamed.Add($"  {sprites[i].name} → {newName}");
                sprites[i].name = newName;
            }
            else
            {
                skipped.Add(sprites[i].name);
            }
        }

        importer.spritesheet = sprites;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        Debug.Log($"✅ Renamed {renamed.Count} sprites, skipped {skipped.Count}.\n"
                + string.Join("\n", renamed));
    }
}
#endif
