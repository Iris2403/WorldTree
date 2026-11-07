using System;
using System.Collections.Generic;
using WorldTree.Core.Board;
using WorldTree.Core.Common;
using WorldTree.Core.Energy;
using WorldTree.Core.Generators;
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

                    ItemDef def = null;
                    if (cell.ItemId != null && !catalog.TryGetItem(cell.ItemId, out def))
                        warnings?.Add("Unknown item '" + cell.ItemId + "' at " + pos + " skipped");

                    if (def != null)
                    {
                        GeneratorState generator = null;
                        if (def.IsGenerator)
                        {
                            int charges = Math.Max(0, Math.Min(def.Generator.Capacity, cell.Charges ?? def.Generator.Capacity));
                            var refreshEnds = FromTicks(cell.RefreshEndsUtcTicks, warnings);
                            // A partly used generator must have a running cycle, or it would never refresh.
                            if (charges < def.Generator.Capacity && refreshEnds == null)
                                refreshEnds = clock.UtcNow + def.Generator.RefreshDuration;
                            generator = new GeneratorState(charges, refreshEnds);
                        }
                        board.SetItem(pos, new ItemInstance(def.Id, generator));
                    }

                    // Locked cells must hold an item (GDD §3.1); otherwise the cell is opened.
                    board.SetState(pos, board.GetItem(pos) == null ? CellState.Open : cell.State);
                }
            }

            var energy = new EnergyModel(energyConfig, Math.Max(0, data.Sap), FromTicks(data.SapNextTickUtcTicks, warnings));
            return new GameSession(catalog, board, energy, data.RandomState, clock);
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
