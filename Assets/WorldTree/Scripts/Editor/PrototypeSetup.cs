using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WorldTree.Core.Content;
using WorldTree.Core.Items;
using WorldTree.Data;
using WorldTree.Game;

namespace WorldTree.EditorTools
{
    /// <summary>
    /// Builds the Phase 1 prototype without hand-written scene or asset YAML (CLAUDE.md constraint):
    /// creates the ScriptableObject content from <see cref="PrototypeContent"/> (existing assets are
    /// kept, so designer edits survive) and a Main scene with a camera and the bootstrapper.
    /// </summary>
    public static class PrototypeSetup
    {
        private const string Root = "Assets/WorldTree";
        private const string DataRoot = Root + "/Data";
        private const string ScenePath = Root + "/Scenes/Main.unity";
        private const string DatabasePath = DataRoot + "/Resources/" + GameDatabase.ResourcesName + ".asset";
        private const string OldDatabasePath = DataRoot + "/GameDatabase.asset";

        private static readonly Dictionary<string, Color> ChainColors = new Dictionary<string, Color>
        {
            { PrototypeContent.NatureChain, new Color(0.45f, 0.8f, 0.4f) },
            { PrototypeContent.WaterChain, new Color(0.4f, 0.65f, 0.98f) },
            { PrototypeContent.SeedPouchChain, new Color(0.9f, 0.7f, 0.35f) },
            { PrototypeContent.MoonwellChain, new Color(0.45f, 0.9f, 0.85f) },
            { PrototypeContent.CraftChain, new Color(0.85f, 0.62f, 0.4f) },
            { PrototypeContent.StumpChain, new Color(0.75f, 0.5f, 0.3f) },
            { PrototypeContent.AcornChain, new Color(0.95f, 0.8f, 0.35f) },
        };

        [MenuItem("WorldTree/Setup/Build Prototype Scene")]
        public static void BuildPrototypeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            bool overwrite = false;
            if (AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath) != null || AssetDatabase.LoadAssetAtPath<GameDatabase>(OldDatabasePath) != null)
            {
                overwrite = EditorUtility.DisplayDialog("Update content assets?",
                    "Content assets already exist. Update them to the latest defaults from the code?\n\n" +
                    "Choose Update after pulling a new phase. Choose Keep if you edited the assets by hand.",
                    "Update", "Keep");
            }
            var database = CreateContentAssets(overwrite);
            BuildScene(database);
            Debug.Log("[WorldTree] Prototype scene ready at " + ScenePath + ". Press Play.");
        }

        [MenuItem("WorldTree/Setup/Update Content Assets to Code Defaults")]
        public static void UpdateContentAssetsMenu()
        {
            if (!EditorUtility.DisplayDialog("Update content assets", "Overwrite all WorldTree content assets with the defaults from PrototypeContent?", "Update", "Cancel")) return;
            Selection.activeObject = CreateContentAssets(overwrite: true);
            Debug.Log("[WorldTree] Content assets updated. Use the Debug overlay's Reset save to start on the new board.");
        }

        [MenuItem("WorldTree/Validate Data")]
        public static void ValidateData()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (database == null)
            {
                EditorUtility.DisplayDialog("Validate Data", "No GameDatabase at " + DatabasePath + ". Run WorldTree → Setup → Build Prototype Scene.", "OK");
                return;
            }
            var errors = new List<string>();
            try
            {
                var catalog = database.BuildCatalog();
                errors.AddRange(BoardLayoutValidator.Validate(database.BuildLayout(), catalog));
            }
            catch (System.Exception e)
            {
                errors.Add(e.Message);
            }
            foreach (var e in errors) Debug.LogError("[WorldTree] Data: " + e);
            EditorUtility.DisplayDialog("Validate Data", errors.Count == 0 ? "All data is valid." : errors.Count + " problem(s). See the Console.", "OK");
        }

        [MenuItem("WorldTree/Debug/Delete Save Files")]
        public static void DeleteSaveFiles()
        {
            string dir = Path.Combine(Application.persistentDataPath, "save");
            if (!Directory.Exists(dir))
            {
                Debug.Log("[WorldTree] No save folder at " + dir);
                return;
            }
            if (!EditorUtility.DisplayDialog("Delete save files", "Delete all WORLD TREE save files in\n" + dir + "?", "Delete", "Cancel")) return;
            Directory.Delete(dir, true);
            Debug.Log("[WorldTree] Deleted " + dir);
        }

        private static GameDatabase CreateContentAssets(bool overwrite)
        {
            EnsureFolder(DataRoot + "/Chains");
            EnsureFolder(DataRoot + "/Generators");
            EnsureFolder(DataRoot + "/Boards");
            EnsureFolder(DataRoot + "/Config");
            EnsureFolder(DataRoot + "/Resources");

            // Earlier versions created the database outside Resources; move it so the runtime fallback finds it.
            if (AssetDatabase.LoadAssetAtPath<GameDatabase>(OldDatabasePath) != null && AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath) == null)
                AssetDatabase.MoveAsset(OldDatabasePath, DatabasePath);

            var chainAssets = new List<ItemChainDefinition>();
            var generatorAssets = new List<GeneratorDefinition>();
            foreach (var chain in PrototypeContent.BuildChains())
            {
                string chainPath = DataRoot + "/Chains/Chain_" + AssetName(chain.Id) + ".asset";
                chainAssets.Add(LoadOrCreate<ItemChainDefinition>(chainPath, overwrite, asset =>
                {
                    var levels = new List<ItemChainDefinition.Level>();
                    foreach (var item in chain.Levels)
                    {
                        var displayName = PrototypeContent.NameOf(item.Id);
                        levels.Add(new ItemChainDefinition.Level { id = item.Id, displayName = displayName, sellValue = item.SellValue });
                    }
                    asset.Configure(chain.Id, ChainColors.TryGetValue(chain.Id, out var c) ? c : Color.white, levels);
                }));

                foreach (var item in chain.Levels)
                {
                    if (!item.IsGenerator) continue;
                    string genPath = DataRoot + "/Generators/Gen_" + AssetName(item.Id) + ".asset";
                    generatorAssets.Add(LoadOrCreate<GeneratorDefinition>(genPath, overwrite, asset => ConfigureGenerator(asset, item)));
                }
            }

            var layoutDef = PrototypeContent.StartingLayout();
            var layout = LoadOrCreate<BoardLayoutDefinition>(DataRoot + "/Boards/Board_Prototype.asset", overwrite, asset =>
            {
                var cells = new List<BoardLayoutDefinition.Placement>();
                foreach (var p in layoutDef.Placements)
                    cells.Add(new BoardLayoutDefinition.Placement { x = p.Pos.X, y = p.Pos.Y, itemId = p.ItemId, state = p.State });
                asset.Configure(layoutDef.Width, layoutDef.Height, cells);
            });

            var energy = LoadOrCreate<EnergyConfigDefinition>(DataRoot + "/Config/Energy.asset", overwrite, asset =>
                asset.Configure(PrototypeContent.Energy.Cap, (float)PrototypeContent.Energy.RegenInterval.TotalSeconds));

            var database = LoadOrCreate<GameDatabase>(DatabasePath, false, asset => { });
            database.Configure(chainAssets, generatorAssets, layout, energy);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            // Fail early in the Editor if the authored data is inconsistent.
            database.BuildCatalog();
            database.BuildLayout();
            return database;
        }

        private static void ConfigureGenerator(GeneratorDefinition asset, ItemDef item)
        {
            var drops = new List<GeneratorDefinition.Drop>();
            foreach (var d in item.Generator.Drops.Entries) drops.Add(new GeneratorDefinition.Drop { itemId = d.ItemId, weight = d.Weight });
            float minutes = item.Generator.IsConsumable ? 0f : (float)item.Generator.RefreshDuration.TotalMinutes;
            asset.Configure(item.Id, item.Generator.Capacity, minutes, item.Generator.SapCost, drops, item.Generator.IsConsumable);
        }

        private static void BuildScene(GameDatabase database)
        {
            EnsureFolder(Root + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.07f, 0.06f);
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            cameraGo.AddComponent<BoardCameraFitter>();

            var bootGo = new GameObject("Bootstrapper");
            var boot = bootGo.AddComponent<Bootstrapper>();
            var so = new SerializedObject(boot);
            so.FindProperty("database").objectReferenceValue = database;
            so.FindProperty("boardCamera").objectReferenceValue = camera;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);

            if (so.FindProperty("database").objectReferenceValue == null)
                Debug.LogError("[WorldTree] The Bootstrapper's GameDatabase reference wasn't set; it will fall back to Resources/" + GameDatabase.ResourcesName + ".");
            Selection.activeObject = bootGo;
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
                if (s.path == path) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Loads an asset, creating it if missing. With <paramref name="overwrite"/> an existing asset is reconfigured.</summary>
        private static T LoadOrCreate<T>(string path, bool overwrite, System.Action<T> configure) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                if (overwrite)
                {
                    configure(existing);
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            }
            var asset = ScriptableObject.CreateInstance<T>();
            configure(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string AssetName(string id)
        {
            var parts = id.Split('.', '_');
            var sb = new System.Text.StringBuilder();
            foreach (var part in parts)
            {
                if (part.Length == 0 || part == "chain" || part == "gen") continue;
                sb.Append(char.ToUpperInvariant(part[0])).Append(part.Substring(1));
            }
            return sb.ToString();
        }
    }
}
