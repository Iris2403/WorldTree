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

        private static readonly Dictionary<string, Color> ChainColors = new Dictionary<string, Color>
        {
            { PrototypeContent.NatureChain, new Color(0.45f, 0.8f, 0.4f) },
            { PrototypeContent.WaterChain, new Color(0.4f, 0.65f, 0.98f) },
            { PrototypeContent.SeedPouchChain, new Color(0.9f, 0.7f, 0.35f) },
            { PrototypeContent.MoonwellChain, new Color(0.45f, 0.9f, 0.85f) },
        };

        [MenuItem("WorldTree/Setup/Build Prototype Scene")]
        public static void BuildPrototypeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var database = CreateContentAssets();
            BuildScene(database);
            Debug.Log("[WorldTree] Prototype scene ready at " + ScenePath + ". Press Play.");
        }

        [MenuItem("WorldTree/Setup/Create Content Assets Only")]
        public static void CreateContentAssetsMenu()
        {
            Selection.activeObject = CreateContentAssets();
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

        private static GameDatabase CreateContentAssets()
        {
            EnsureFolder(DataRoot + "/Chains");
            EnsureFolder(DataRoot + "/Generators");
            EnsureFolder(DataRoot + "/Boards");
            EnsureFolder(DataRoot + "/Config");

            var chainAssets = new List<ItemChainDefinition>();
            var generatorAssets = new List<GeneratorDefinition>();
            foreach (var chain in PrototypeContent.BuildChains())
            {
                string chainPath = DataRoot + "/Chains/Chain_" + AssetName(chain.Id) + ".asset";
                chainAssets.Add(LoadOrCreate<ItemChainDefinition>(chainPath, asset =>
                {
                    var levels = new List<ItemChainDefinition.Level>();
                    foreach (var item in chain.Levels)
                    {
                        PrototypeContent.DisplayNames.TryGetValue(item.Id, out var displayName);
                        levels.Add(new ItemChainDefinition.Level { id = item.Id, displayName = displayName, sellValue = item.SellValue });
                    }
                    asset.Configure(chain.Id, ChainColors.TryGetValue(chain.Id, out var c) ? c : Color.white, levels);
                }));

                foreach (var item in chain.Levels)
                {
                    if (!item.IsGenerator) continue;
                    string genPath = DataRoot + "/Generators/Gen_" + AssetName(item.Id) + ".asset";
                    generatorAssets.Add(LoadOrCreate<GeneratorDefinition>(genPath, asset => ConfigureGenerator(asset, item)));
                }
            }

            var layoutDef = PrototypeContent.StartingLayout();
            var layout = LoadOrCreate<BoardLayoutDefinition>(DataRoot + "/Boards/Board_Prototype.asset", asset =>
            {
                var cells = new List<BoardLayoutDefinition.Placement>();
                foreach (var p in layoutDef.Placements)
                    cells.Add(new BoardLayoutDefinition.Placement { x = p.Pos.X, y = p.Pos.Y, itemId = p.ItemId, state = p.State });
                asset.Configure(layoutDef.Width, layoutDef.Height, cells);
            });

            var energy = LoadOrCreate<EnergyConfigDefinition>(DataRoot + "/Config/Energy.asset", asset =>
                asset.Configure(PrototypeContent.Energy.Cap, (float)PrototypeContent.Energy.RegenInterval.TotalSeconds));

            var database = LoadOrCreate<GameDatabase>(DataRoot + "/GameDatabase.asset", asset => { });
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
            asset.Configure(item.Id, item.Generator.Capacity, (float)item.Generator.RefreshDuration.TotalMinutes, item.Generator.SapCost, drops);
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
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
                if (s.path == path) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static T LoadOrCreate<T>(string path, System.Action<T> configureNew) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            configureNew(asset);
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
