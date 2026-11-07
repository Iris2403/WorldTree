using System;
using System.Collections.Generic;
using WorldTree.Core.Board;
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
        public const string CraftChain = "chain.craft";
        public const string StumpChain = "chain.gen.woodwright_stump";

        public const string SeedPouch1 = "gen.seed_pouch.1";
        public const string SeedPouch2 = "gen.seed_pouch.2";
        public const string Moonwell1 = "gen.moonwell_spring.1";
        public const string Stump1 = "gen.woodwright_stump.1";

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
            { "item.nature.07", "Silverbark Tree" },
            { "item.nature.08", "Great Elderwood" },
            { "item.water.01", "Dewdrop" },
            { "item.water.02", "Water Flask" },
            { "item.water.03", "Moon Pitcher" },
            { "item.water.04", "Spring Basin" },
            { "item.water.05", "Crystal Fount" },
            { "item.water.06", "Moonwell" },
            { "item.water.07", "Starlit Pool" },
            { "item.water.08", "Moonwell Fountain" },
            { "item.craft.01", "Twig" },
            { "item.craft.02", "Twig Bundle" },
            { "item.craft.03", "Carved Plank" },
            { "item.craft.04", "Wooden Crate" },
            { "item.craft.05", "Lantern Frame" },
            { "item.craft.06", "Elven Lantern" },
            { SeedPouch1, "Seed Pouch" },
            { SeedPouch2, "Seed Pouch" },
            { Moonwell1, "Moonwell Spring" },
            { Stump1, "Woodwright's Stump" },
        };

        public static EnergyConfig Energy => EnergyConfig.Default;

        public static Catalog BuildCatalog() => new Catalog(BuildChains());

        public static IReadOnlyList<ChainDef> BuildChains()
        {
            return new[]
            {
                ItemChain(NatureChain, "item.nature", 8),
                ItemChain(WaterChain, "item.water", 8),
                ItemChain(CraftChain, "item.craft", 6),
                ChainDef.Create(SeedPouchChain, new[]
                {
                    new LevelSpec(SeedPouch1, 0, Generator(60, 20, "item.nature", 80, 18, 2)),
                    new LevelSpec(SeedPouch2, 0, Generator(66, 18, "item.nature", 70, 25, 5)),
                }),
                ChainDef.Create(MoonwellChain, new[]
                {
                    new LevelSpec(Moonwell1, 0, Generator(60, 20, "item.water", 80, 18, 2)),
                }),
                ChainDef.Create(StumpChain, new[]
                {
                    new LevelSpec(Stump1, 0, Generator(60, 20, "item.craft", 80, 18, 2)),
                }),
            };
        }

        /// <summary>
        /// 7 × 9 starting board (Phase 2). Rows 3–8 are Open, with the Seed Pouch, the Moonwell Spring
        /// and a second Seed Pouch for generator merging. Row 2 is Withered, rows 0–1 are Sealed, and
        /// the Woodwright's Stump waits under the roots in the top row, so the Craft chain is earned by
        /// cleansing the blight (GDD §3.3).
        /// </summary>
        public static BoardLayout StartingLayout()
        {
            var cells = new List<LayoutPlacement>
            {
                new LayoutPlacement(new GridPos(2, 5), SeedPouch1),
                new LayoutPlacement(new GridPos(4, 5), Moonwell1),
                new LayoutPlacement(new GridPos(3, 8), SeedPouch1),
                new LayoutPlacement(new GridPos(1, 4), "item.nature.01"),
                new LayoutPlacement(new GridPos(5, 4), "item.nature.01"),
                new LayoutPlacement(new GridPos(1, 7), "item.water.01"),
                new LayoutPlacement(new GridPos(5, 7), "item.water.01"),
            };

            // Row 2: Withered, items visible. Same-ID items on the board can cleanse them directly.
            string[] withered = { "item.nature.02", "item.water.01", "item.nature.01", "item.craft.02", "item.water.02", "item.nature.01", "item.water.01" };
            for (int x = 0; x < BoardWidth; x++) cells.Add(new LayoutPlacement(new GridPos(x, 2), withered[x], CellState.Withered));

            // Rows 0–1: Sealed, items hidden until revealed.
            string[] row1 = { "item.craft.01", "item.nature.03", "item.water.02", "item.craft.01", "item.nature.02", "item.water.03", "item.craft.02" };
            string[] row0 = { "item.water.04", "item.craft.03", "item.nature.04", Stump1, "item.water.03", "item.craft.01", "item.nature.05" };
            for (int x = 0; x < BoardWidth; x++)
            {
                cells.Add(new LayoutPlacement(new GridPos(x, 1), row1[x], CellState.Sealed));
                cells.Add(new LayoutPlacement(new GridPos(x, 0), row0[x], CellState.Sealed));
            }
            return new BoardLayout(BoardWidth, BoardHeight, cells);
        }

        private static ChainDef ItemChain(string chainId, string idPrefix, int levelCount)
        {
            var levels = new LevelSpec[levelCount];
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
