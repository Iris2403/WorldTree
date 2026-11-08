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
        public const string AcornChain = "chain.gen.mystery_acorn";
        public const string FoodChain = "chain.food";
        public const string OvenChain = "chain.gen.hearthstone_oven";

        public const string SeedPouch1 = "gen.seed_pouch.1";
        public const string SeedPouch2 = "gen.seed_pouch.2";
        public const string Moonwell1 = "gen.moonwell_spring.1";
        public const string Stump1 = "gen.woodwright_stump.1";
        public const string MysteryAcorn = "gen.mystery_acorn";
        public const string Oven1 = "gen.hearthstone_oven.1";

        /// <summary>Taps in a Mystery Acorn before it vanishes (GDD §4.1).</summary>
        public const int MysteryAcornTaps = 8;

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
            { "item.food.01", "Wild Berry" },
            { "item.food.02", "Berry Bowl" },
            { "item.food.03", "Berry Tart" },
            { "item.food.04", "Honey Cake" },
            { "item.food.05", "Festival Pie" },
            { "item.food.06", "Festival Feast" },
            { MysteryAcorn, "Mystery Acorn" },
        };

        public static EnergyConfig Energy => EnergyConfig.Default;

        public static Catalog BuildCatalog() => new Catalog(BuildChains());

        /// <summary>Display name for an ID, including every generator level ("Seed Pouch" for gen.seed_pouch.3).</summary>
        public static string NameOf(string id)
        {
            if (DisplayNames.TryGetValue(id, out var name)) return name;
            if (id.StartsWith("gen.seed_pouch.", StringComparison.Ordinal)) return "Seed Pouch";
            if (id.StartsWith("gen.moonwell_spring.", StringComparison.Ordinal)) return "Moonwell Spring";
            if (id.StartsWith("gen.woodwright_stump.", StringComparison.Ordinal)) return "Woodwright's Stump";
            if (id.StartsWith("gen.hearthstone_oven.", StringComparison.Ordinal)) return "Hearthstone Oven";
            return null;
        }

        public static IReadOnlyList<ChainDef> BuildChains()
        {
            return new[]
            {
                ItemChain(NatureChain, "item.nature", 8),
                ItemChain(WaterChain, "item.water", 8),
                ItemChain(CraftChain, "item.craft", 6),
                ItemChain(FoodChain, "item.food", 6),
                GeneratorChain(SeedPouchChain, "gen.seed_pouch", "item.nature"),
                GeneratorChain(MoonwellChain, "gen.moonwell_spring", "item.water"),
                GeneratorChain(StumpChain, "gen.woodwright_stump", "item.craft"),
                GeneratorChain(OvenChain, "gen.hearthstone_oven", "item.food"),
                // Consumable: a single level, so it is always "max level" and never merges (GDD §4.3).
                ChainDef.Create(AcornChain, new[]
                {
                    new LevelSpec(MysteryAcorn, 3, GeneratorDef.Consumable(MysteryAcornTaps, 1, new DropTable(new[]
                    {
                        new DropEntry("item.nature.01", 30), new DropEntry("item.water.01", 30), new DropEntry("item.craft.01", 20),
                        new DropEntry("item.nature.02", 8), new DropEntry("item.water.02", 8), new DropEntry("item.craft.02", 4),
                    }))),
                }),
            };
        }

        /// <summary>
        /// 7 × 9 starting board (roadmap D31). Only the 3 × 3 centre is Open: the Seed Pouch in the middle
        /// and 8 free places around it. The ring around the centre is Withered, mostly Nature items the
        /// Seed Pouch can match, plus the Moonwell Spring right next to the open area, so the first merge
        /// beside it unlocks the second generator. Everything else is Sealed, revealed as the ring opens:
        /// the Woodwright's Stump at the top and a second Seed Pouch in a corner.
        /// </summary>
        public static BoardLayout StartingLayout()
        {
            // One row per board row. "." = Open and empty, otherwise the item ID; the prefix sets the state:
            // "o:" Open, "w:" Withered, "s:" Sealed.
            string[][] rows =
            {
                new[] { "s:item.craft.01", "s:item.water.03", "s:item.nature.04", "s:" + Stump1, "s:item.water.02", "s:item.craft.02", "s:item.nature.03" },
                new[] { "s:item.water.02", "s:item.nature.02", "s:item.craft.01", "s:item.nature.03", "s:item.craft.01", "s:item.water.03", "s:item.nature.01" },
                new[] { "s:item.water.01", "w:item.nature.02", "w:item.nature.01", "w:" + Moonwell1, "w:item.nature.01", "w:item.nature.02", "s:item.nature.02" },
                new[] { "s:item.nature.02", "w:item.nature.01", ".", ".", ".", "w:item.nature.01", "s:item.water.02" },
                new[] { "s:item.water.04", "w:item.nature.03", ".", "o:" + SeedPouch1, ".", "w:item.water.01", "s:item.nature.04" },
                new[] { "s:item.nature.01", "w:item.nature.01", ".", ".", ".", "w:item.nature.02", "s:item.water.03" },
                new[] { "s:item.water.02", "w:item.water.02", "w:item.nature.01", "w:item.water.01", "w:item.nature.02", "w:item.water.01", "s:item.nature.01" },
                new[] { "s:item.nature.03", "s:item.water.01", "s:item.craft.02", "s:item.nature.05", "s:item.water.02", "s:item.nature.02", "s:item.craft.03" },
                new[] { "s:" + SeedPouch1, "s:item.nature.02", "s:item.water.03", "s:item.craft.01", "s:item.nature.04", "s:item.water.05", "s:item.water.01" },
            };

            var cells = new List<LayoutPlacement>();
            for (int y = 0; y < rows.Length; y++)
            {
                for (int x = 0; x < rows[y].Length; x++)
                {
                    string code = rows[y][x];
                    if (code == ".") continue;
                    var state = code[0] == 'w' ? CellState.Withered : code[0] == 's' ? CellState.Sealed : CellState.Open;
                    cells.Add(new LayoutPlacement(new GridPos(x, y), code.Substring(2), state));
                }
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

        /// <summary>
        /// GDD §4.2 generator table: capacity, refresh minutes and L1–L4 drop weights per level 1–5.
        /// </summary>
        private static readonly int[][] GeneratorTable =
        {
            new[] { 60, 20, 80, 18, 2, 0 },
            new[] { 66, 18, 70, 25, 5, 0 },
            new[] { 72, 16, 60, 30, 9, 1 },
            new[] { 80, 14, 50, 35, 13, 2 },
            new[] { 90, 12, 40, 38, 18, 4 },
        };

        public const int MaxGeneratorLevel = 5;

        private static ChainDef GeneratorChain(string chainId, string idPrefix, string dropPrefix)
        {
            var levels = new LevelSpec[MaxGeneratorLevel];
            for (int i = 0; i < levels.Length; i++)
            {
                var row = GeneratorTable[i];
                levels[i] = new LevelSpec(idPrefix + "." + (i + 1), 0, Generator(row[0], row[1], dropPrefix, row[2], row[3], row[4], row[5]));
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
