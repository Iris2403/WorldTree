using System.Text;
using UnityEditor;
using UnityEngine;
using WorldTree.Data;

namespace WorldTree.EditorTools
{
    /// <summary>Lists every item level that still uses a generated stand-in icon (docs/art/placeholders.md).</summary>
    public static class ArtReport
    {
        [MenuItem("WorldTree/Art/Report Placeholders")]
        public static void ReportPlaceholders()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/WorldTree/Data/Resources/GameDatabase.asset");
            if (database == null)
            {
                EditorUtility.DisplayDialog("Report Placeholders", "No GameDatabase found. Run WorldTree → Setup → Build Prototype Scene.", "OK");
                return;
            }
            var sb = new StringBuilder();
            int missing = 0, total = 0;
            foreach (var chain in database.Chains)
            {
                if (chain == null) continue;
                foreach (var level in chain.Levels)
                {
                    total++;
                    if (level.icon != null) continue;
                    missing++;
                    sb.Append(chain.ChainId).Append(": ").Append(level.id).Append(" (").Append(level.displayName).Append(")\n");
                }
            }
            Debug.Log("[WorldTree] Item icons still using stand-ins: " + missing + " of " + total + "\n" + sb +
                      "Audio, UI skin, font, area art and portraits are tracked in docs/art/placeholders.md.");
            EditorUtility.DisplayDialog("Report Placeholders", missing + " of " + total + " item icons still use stand-ins. Details in the Console.", "OK");
        }
    }
}
