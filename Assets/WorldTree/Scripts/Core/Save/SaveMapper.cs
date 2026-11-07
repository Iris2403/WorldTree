using System;
using System.Collections.Generic;
using WorldTree.Core.Board;
using WorldTree.Core.Collection;
using WorldTree.Core.Common;
using WorldTree.Core.Economy;
using WorldTree.Core.Energy;
using WorldTree.Core.Generators;
using WorldTree.Core.Inventory;
using WorldTree.Core.Items;

namespace WorldTree.Core.Save
{
    /// <summary>Converts between the live <see cref="GameSession"/> and its <see cref="SaveData"/> snapshot.</summary>
    public static class SaveMapper
    {
        public static SaveData Capture(GameSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            session.Energy.Refresh(session.UtcNow);
            var board = session.Board;
            var data = new SaveData
            {
                Width = board.Width,
                Height = board.Height,
                Sap = session.Energy.Amount,
                SapNextTickUtcTicks = session.Energy.NextTickUtc?.Ticks,
                RandomState = session.RandomState,
                Glimmer = session.Wallet.Glimmer,
            };
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var pos = new GridPos(x, y);
                    var item = board.GetItem(pos);
                    var state = board.GetState(pos);
                    if (item == null && state == CellState.Open) continue;
                    data.Cells.Add(new CellData
                    {
                        X = x,
                        Y = y,
                        State = state,
                        ItemId = item?.ItemId,
                        Charges = item?.Generator?.Charges,
                        RefreshEndsUtcTicks = item?.Generator?.RefreshEndsUtc?.Ticks,
                    });
                }
            }
            foreach (var item in session.Satchel.Items)
            {
                data.Satchel.Add(new SatchelItemData
                {
                    ItemId = item.ItemId,
                    Charges = item.Generator?.Charges,
                    RefreshEndsUtcTicks = item.Generator?.RefreshEndsUtc?.Ticks,
                });
            }
            var discovered = new List<string>(session.Codex.Discovered);
            discovered.Sort(StringComparer.Ordinal); // stable output: equal states produce equal files
            data.Discovered.AddRange(discovered);
            return data;
        }

        /// <summary>
        /// Rebuilds a session from saved data. Unknown item IDs and out-of-range cells are skipped and
        /// reported in <paramref name="warnings"/> instead of failing the whole load.
        /// Throws <see cref="FormatException"/> if the data is structurally unusable.
        /// </summary>
        public static GameSession Restore(SaveData data, ICatalog catalog, EnergyConfig energyConfig, IClock clock, List<string> warnings)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Width <= 0 || data.Height <= 0 || data.Width > 64 || data.Height > 64)
                throw new FormatException("Invalid board size " + data.Width + "x" + data.Height);

            var board = new BoardModel(data.Width, data.Height);
            if (data.Cells != null)
            {
                foreach (var cell in data.Cells)
                {
                    if (cell == null) continue;
                    var pos = new GridPos(cell.X, cell.Y);
                    if (!board.IsInside(pos))
                    {
                        warnings?.Add("Cell outside the board skipped: " + pos);
                        continue;
                    }
                    var item = RestoreItem(cell.ItemId, cell.Charges, cell.RefreshEndsUtcTicks, catalog, clock, warnings, pos.ToString());
                    if (item != null) board.SetItem(pos, item);

                    // Locked cells must hold an item (GDD §3.1); otherwise the cell is opened.
                    board.SetState(pos, board.GetItem(pos) == null ? CellState.Open : cell.State);
                }
            }

            var satchel = new Satchel(EconomyRules.StartingSatchelSlots);
            if (data.Satchel != null)
            {
                foreach (var stored in data.Satchel)
                {
                    if (stored == null) continue;
                    var item = RestoreItem(stored.ItemId, stored.Charges, stored.RefreshEndsUtcTicks, catalog, clock, warnings, "satchel");
                    if (item != null) satchel.Restore(item);
                }
            }

            var codex = new Codex();
            if (data.Discovered != null)
            {
                foreach (var id in data.Discovered)
                {
                    if (id != null && catalog.TryGetItem(id, out _)) codex.Discover(id);
                }
            }

            var energy = new EnergyModel(energyConfig, Math.Max(0, data.Sap), FromTicks(data.SapNextTickUtcTicks, warnings));
            return new GameSession(catalog, board, energy, new Wallet(Math.Max(0, data.Glimmer)), codex, satchel, data.RandomState, clock);
        }

        private static ItemInstance RestoreItem(string itemId, int? charges, long? refreshTicks, ICatalog catalog, IClock clock,
            List<string> warnings, string where)
        {
            if (itemId == null) return null;
            if (!catalog.TryGetItem(itemId, out var def))
            {
                warnings?.Add("Unknown item '" + itemId + "' at " + where + " skipped");
                return null;
            }
            if (!def.IsGenerator) return new ItemInstance(def.Id);

            int restoredCharges = Math.Max(0, Math.Min(def.Generator.Capacity, charges ?? def.Generator.Capacity));
            var refreshEnds = FromTicks(refreshTicks, warnings);
            // A partly used generator must have a running cycle, or it would never refresh.
            if (restoredCharges < def.Generator.Capacity && refreshEnds == null)
                refreshEnds = clock.UtcNow + def.Generator.RefreshDuration;
            return new ItemInstance(def.Id, new GeneratorState(restoredCharges, refreshEnds));
        }

        private static DateTime? FromTicks(long? ticks, List<string> warnings)
        {
            if (ticks == null) return null;
            if (ticks.Value < DateTime.MinValue.Ticks || ticks.Value > DateTime.MaxValue.Ticks)
            {
                warnings?.Add("Invalid timestamp ignored");
                return null;
            }
            return new DateTime(ticks.Value, DateTimeKind.Utc);
        }
    }
}
