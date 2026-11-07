using System;
using System.Collections.Generic;
using WorldTree.Core.Common;
using WorldTree.Core.Energy;
using WorldTree.Core.Items;

namespace WorldTree.Core.Content
{
    /// <summary>
    /// Phase 1 content defined in code (prototype spec §2.3). It is the single source for the tests
    /// and for the Editor menu that creates the editable ScriptableObject assets.
    /// </summary>
    public static class PrototypeContent
    {
        public const string NatureChain = "chain.nature";
        public const string WaterChain = "chain.water";
        public const string SeedPouchChain = "chain.gen.seed_pouch";
        public const string MoonwellChain = "chain.gen.moonwell_spring";

        public const string SeedPouch1 = "gen.seed_pouch.1";
        public const string SeedPouch2 = "gen.seed_pouch.2";
        public const string Moonwell1 = "gen.moonwell_spring.1";

        public const int BoardWidth = 7;
        public const int BoardHeight = 9;

        /// <summary>English display names until the Localization package arrives (Phase 7).</summary>
        public static readonly IReadOnlyDictionary<string, string> DisplayNames = new Dictionary<string, string>
        {
            { "item.nature.01", "Glimmerseed" },
            { "item.nature.02", "Sprout" },
            { "item.nature.03", "Sapling" },
            { "item.nature.04", "Blossom Shrub" },
            { "item.nature.05", "Young Oak" },
            { "item.nature.06", "Elder Oak" },
            { "item.water.01", "Dewdrop" },
            { "item.water.02", "Water Flask" },
            { "item.water.03", "Moon Pitcher" },
            { "item.water.04", "Spring Basin" },
            { "item.water.05", "Crystal Fount" },
            { "item.water.06", "Moonwell" },
            { SeedPouch1, "Seed Pouch" },
            { SeedPouch2, "Seed Pouch" },
            { Moonwell1, "Moonwell Spring" },
        };

        public static EnergyConfig Energy => EnergyConfig.Default;

        public static Catalog BuildCatalog() => new Catalog(BuildChains());

        public static IReadOnlyList<ChainDef> BuildChains()
        {
            return new[]
            {
                ItemChain(NatureChain, "item.nature"),
                ItemChain(WaterChain, "item.water"),
                ChainDef.Create(SeedPouchChain, new[]
                {
                    new LevelSpec(SeedPouch1, 0, Generator(60, 20, "item.nature", 80, 18, 2)),
                    new LevelSpec(SeedPouch2, 0, Generator(66, 18, "item.nature", 70, 25, 5)),
                }),
                ChainDef.Create(MoonwellChain, new[]
                {
                    new LevelSpec(Moonwell1, 0, Generator(60, 20, "item.water", 80, 18, 2)),
                }),
            };
        }

        /// <summary>
        /// 7 × 9 starting board: both generators, a second Seed Pouch so generator merging can be tried,
        /// and a few level-1 items.
        /// </summary>
        public static BoardLayout StartingLayout() => new BoardLayout(BoardWidth, BoardHeight, new[]
        {
            new LayoutPlacement(new GridPos(2, 4), SeedPouch1),
            new LayoutPlacement(new GridPos(4, 4), Moonwell1),
            new LayoutPlacement(new GridPos(3, 7), SeedPouch1),
            new LayoutPlacement(new GridPos(1, 1), "item.nature.01"),
            new LayoutPlacement(new GridPos(5, 1), "item.nature.01"),
            new LayoutPlacement(new GridPos(1, 7), "item.water.01"),
            new LayoutPlacement(new GridPos(5, 7), "item.water.01"),
        });

        private static ChainDef ItemChain(string chainId, string idPrefix)
        {
            var levels = new LevelSpec[6];
            for (int i = 0; i < levels.Length; i++)
            {
                levels[i] = new LevelSpec(idPrefix + ".0" + (i + 1), 1 << i);
            }
            return ChainDef.Create(chainId, levels);
        }

        private static GeneratorDef Generator(int capacity, int refreshMinutes, string dropPrefix, params int[] levelWeights)
        {
            var entries = new List<DropEntry>();
            for (int i = 0; i < levelWeights.Length; i++)
            {
                if (levelWeights[i] > 0) entries.Add(new DropEntry(dropPrefix + ".0" + (i + 1), levelWeights[i]));
            }
            return new GeneratorDef(capacity, TimeSpan.FromMinutes(refreshMinutes), 1, new DropTable(entries));
        }
    }
}
