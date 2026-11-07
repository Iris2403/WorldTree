using System;
using System.Collections.Generic;
using UnityEngine;
using WorldTree.Core.Common;
using WorldTree.Core.Content;
using WorldTree.Core.Energy;
using WorldTree.Core.Items;

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

        public IReadOnlyList<ItemChainDefinition> Chains => chains;

        public void Configure(List<ItemChainDefinition> chainAssets, List<GeneratorDefinition> generatorAssets,
            BoardLayoutDefinition layout, EnergyConfigDefinition energyConfig)
        {
            chains = chainAssets;
            generators = generatorAssets;
            startingLayout = layout;
            energy = energyConfig;
        }

        public Catalog BuildCatalog()
        {
            var generatorById = new Dictionary<string, GeneratorDef>(StringComparer.Ordinal);
            foreach (var g in generators)
            {
                if (g == null) continue;
                var entries = new List<DropEntry>();
                foreach (var d in g.Drops) entries.Add(new DropEntry(d.itemId, d.weight));
                var def = new GeneratorDef(g.Capacity, TimeSpan.FromMinutes(g.RefreshMinutes), g.SapCost, new DropTable(entries));
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
