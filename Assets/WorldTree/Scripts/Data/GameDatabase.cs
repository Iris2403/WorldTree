using System;
using System.Collections.Generic;
using UnityEngine;
using WorldTree.Core.Common;
using WorldTree.Core.Content;
using WorldTree.Core.Energy;
using WorldTree.Core.Items;
using WorldTree.Core.Story;

namespace WorldTree.Data
{
    /// <summary>
    /// Root data asset. Converts the authoring ScriptableObjects into immutable Core definitions at
    /// boot (architecture rule 3). Invalid data fails here, with a clear error, before gameplay starts.
    /// </summary>
    [CreateAssetMenu(menuName = "WorldTree/Game Database", fileName = "GameDatabase")]
    public sealed class GameDatabase : ScriptableObject
    {
        /// <summary>Asset name inside a Resources folder, used as the Bootstrapper's fallback.</summary>
        public const string ResourcesName = "GameDatabase";

        [SerializeField] private List<ItemChainDefinition> chains = new List<ItemChainDefinition>();
        [SerializeField] private List<GeneratorDefinition> generators = new List<GeneratorDefinition>();
        [SerializeField] private BoardLayoutDefinition startingLayout;
        [SerializeField] private EnergyConfigDefinition energy;
        [Tooltip("Areas in play order (Phase 5).")]
        [SerializeField] private List<AreaDefinition> areas = new List<AreaDefinition>();
        [SerializeField] private List<DialogueDefinition> dialogues = new List<DialogueDefinition>();

        public IReadOnlyList<ItemChainDefinition> Chains => chains;

        public void Configure(List<ItemChainDefinition> chainAssets, List<GeneratorDefinition> generatorAssets,
            BoardLayoutDefinition layout, EnergyConfigDefinition energyConfig)
        {
            chains = chainAssets;
            generators = generatorAssets;
            startingLayout = layout;
            energy = energyConfig;
        }

        /// <summary>Phase 5 story assets. Kept separate from <see cref="Configure"/> so older call sites still work.</summary>
        public void ConfigureStory(List<AreaDefinition> areaAssets, List<DialogueDefinition> dialogueAssets)
        {
            areas = areaAssets;
            dialogues = dialogueAssets;
        }

        /// <summary>Builds the Core story catalog, or null when no areas are configured.</summary>
        public StoryCatalog BuildStory()
        {
            if (areas == null || areas.Count == 0) return null;
            var areaDefs = new List<AreaDef>();
            foreach (var a in areas)
            {
                if (a == null) continue;
                var tasks = new List<RestorationTask>();
                foreach (var t in a.Tasks) tasks.Add(new RestorationTask(t.id, t.name, t.lifebloomCost, Empty(t.dialogueId)));
                areaDefs.Add(new AreaDef(a.AreaId, a.DisplayName, tasks, Empty(a.IntroDialogueId), Empty(a.CompletionDialogueId),
                    new List<string>(a.RewardItemIds), Empty(a.MemoryLeafId)));
            }
            var scenes = new List<DialogueScene>();
            foreach (var d in dialogues)
            {
                if (d == null) continue;
                var lines = new List<DialogueLine>();
                foreach (var l in d.Lines) lines.Add(new DialogueLine(l.speaker, l.text));
                scenes.Add(new DialogueScene(d.SceneId, lines));
            }
            return new StoryCatalog(areaDefs, scenes);
        }

        private static string Empty(string s) => string.IsNullOrEmpty(s) ? null : s;

        public Catalog BuildCatalog()
        {
            var generatorById = new Dictionary<string, GeneratorDef>(StringComparer.Ordinal);
            foreach (var g in generators)
            {
                if (g == null) continue;
                var entries = new List<DropEntry>();
                foreach (var d in g.Drops) entries.Add(new DropEntry(d.itemId, d.weight));
                var def = g.Consumable
                    ? GeneratorDef.Consumable(g.Capacity, g.SapCost, new DropTable(entries))
                    : new GeneratorDef(g.Capacity, TimeSpan.FromMinutes(g.RefreshMinutes), g.SapCost, new DropTable(entries));
                if (generatorById.ContainsKey(g.ItemId)) throw new InvalidOperationException("Two generator assets for " + g.ItemId);
                generatorById.Add(g.ItemId, def);
            }

            var chainDefs = new List<ChainDef>();
            foreach (var chain in chains)
            {
                if (chain == null) continue;
                var levels = new List<LevelSpec>();
                foreach (var level in chain.Levels)
                {
                    generatorById.TryGetValue(level.id, out var generator);
                    levels.Add(new LevelSpec(level.id, level.sellValue, generator));
                }
                chainDefs.Add(ChainDef.Create(chain.ChainId, levels));
            }
            return new Catalog(chainDefs);
        }

        public BoardLayout BuildLayout()
        {
            if (startingLayout == null) throw new InvalidOperationException("GameDatabase has no starting layout.");
            var placements = new List<LayoutPlacement>();
            foreach (var p in startingLayout.Placements)
                placements.Add(new LayoutPlacement(new GridPos(p.x, p.y), p.itemId, p.state));
            return new BoardLayout(startingLayout.Width, startingLayout.Height, placements);
        }

        public EnergyConfig BuildEnergyConfig() =>
            energy == null ? EnergyConfig.Default : new EnergyConfig(energy.Cap, TimeSpan.FromSeconds(energy.RegenSeconds));

        /// <summary>Display data for an item, for views. Returns false for unknown IDs.</summary>
        public bool TryGetVisual(string itemId, out ItemChainDefinition chain, out ItemChainDefinition.Level level)
        {
            foreach (var c in chains)
            {
                if (c == null) continue;
                foreach (var l in c.Levels)
                {
                    if (l.id == itemId)
                    {
                        chain = c;
                        level = l;
                        return true;
                    }
                }
            }
            chain = null;
            level = null;
            return false;
        }
    }
}
