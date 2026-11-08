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
using WorldTree.Core.Orders;
using WorldTree.Core.Progression;
using WorldTree.Core.Story;

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
                Moonpetals = session.Wallet.Moonpetals,
                DailyGiftNextDay = session.DailyGift.NextDayIndex,
                DailyGiftLastClaimDay = session.DailyGift.LastClaimDay,
                LimitsDay = session.DailyLimits.Day,
                SapRefillsToday = session.DailyLimits.SapRefills,
                AdSapClaimsToday = session.DailyLimits.AdSapClaims,
                Lifebloom = session.Wallet.Lifebloom,
                Level = session.Progress.Level,
                Xp = session.Progress.Xp,
                NextOrderId = session.Orders.NextId,
                OrdersCompleted = session.Orders.Completed,
                LastOrderSwapUtcTicks = session.Orders.LastSwapUtc?.Ticks,
            };
            data.AreaIndex = session.Quest.AreaIndex;
            data.CompletedTasks.AddRange(Sorted(session.Quest.CompletedTasks));
            data.StoryFlags.AddRange(Sorted(session.Quest.Flags));
            data.PendingDialogues.AddRange(session.Quest.PendingDialogues);
            data.RecentOrderChains.AddRange(session.Orders.RecentChains);
            data.DemandWindowChains.AddRange(session.Orders.WindowChains);
            data.DemandWindowValues.AddRange(session.Orders.WindowValues);
            foreach (var order in session.Orders.Active)
            {
                var od = new OrderData
                {
                    Id = order.Id,
                    CustomerId = order.CustomerId,
                    ChainId = order.ChainId,
                    Glimmer = order.Glimmer,
                    Xp = order.Xp,
                    Lifebloom = order.Lifebloom,
                    BonusItemId = order.BonusItemId,
                };
                foreach (var r in order.Requirements)
                {
                    od.Items.Add(r.ItemId);
                    od.Fulfilled.Add(r.Fulfilled);
                }
                data.Orders.Add(od);
            }
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
                        AdSpeedUpUsed = item?.Generator != null && item.Generator.AdSpeedUpUsed ? true : (bool?)null,
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
                    AdSpeedUpUsed = item.Generator != null && item.Generator.AdSpeedUpUsed ? true : (bool?)null,
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
        public static GameSession Restore(SaveData data, ICatalog catalog, EnergyConfig energyConfig, IClock clock, List<string> warnings,
            StoryCatalog story = null)
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
                    var item = RestoreItem(cell.ItemId, cell.Charges, cell.RefreshEndsUtcTicks, cell.AdSpeedUpUsed, catalog, clock, warnings, pos.ToString());
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
                    var item = RestoreItem(stored.ItemId, stored.Charges, stored.RefreshEndsUtcTicks, stored.AdSpeedUpUsed, catalog, clock, warnings, "satchel");
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
            var orders = new OrderBoard(data.NextOrderId, data.OrdersCompleted, FromTicks(data.LastOrderSwapUtcTicks, warnings));
            if (data.RecentOrderChains != null) foreach (var c in data.RecentOrderChains) if (c != null) orders.RestoreRecentChain(c);
            if (data.DemandWindowChains != null && data.DemandWindowValues != null)
                for (int i = 0; i < data.DemandWindowChains.Count && i < data.DemandWindowValues.Count; i++)
                    if (data.DemandWindowChains[i] != null) orders.RestoreWindowEntry(data.DemandWindowChains[i], Math.Max(0, data.DemandWindowValues[i]));
            if (data.Orders != null)
            {
                foreach (var od in data.Orders)
                {
                    var order = RestoreOrder(od, catalog, warnings);
                    if (order != null) orders.Restore(order);
                }
            }

            return new GameSession(catalog, board, energy,
                new Wallet(Math.Max(0, data.Glimmer), Math.Max(0, data.Moonpetals), Math.Max(0, data.Lifebloom)), codex, satchel,
                data.RandomState, clock, new DailyGift(data.DailyGiftNextDay, data.DailyGiftLastClaimDay),
                new DailyLimits(data.LimitsDay, data.SapRefillsToday, data.AdSapClaimsToday),
                new PlayerProgress(data.Level, data.Xp), orders, story, RestoreQuest(data, story, warnings));
        }

        private static QuestState RestoreQuest(SaveData data, StoryCatalog story, List<string> warnings)
        {
            int areas = story?.Areas.Count ?? 0;
            var quest = new QuestState(Math.Min(Math.Max(0, data.AreaIndex), areas));
            if (data.CompletedTasks != null) foreach (var t in data.CompletedTasks) if (t != null) quest.CompleteTask(t);
            if (data.StoryFlags != null) foreach (var f in data.StoryFlags) if (f != null) quest.SetFlag(f);
            if (data.PendingDialogues != null)
                foreach (var d in data.PendingDialogues)
                {
                    if (story != null && story.TryGetScene(d, out _)) quest.QueueDialogue(d);
                    else if (d != null) warnings?.Add("Unknown dialogue scene '" + d + "' dropped");
                }
            return quest;
        }

        private static List<string> Sorted(IEnumerable<string> values)
        {
            var list = new List<string>(values);
            list.Sort(StringComparer.Ordinal); // stable output: equal states produce equal files
            return list;
        }

        private static Order RestoreOrder(OrderData od, ICatalog catalog, List<string> warnings)
        {
            if (od?.Items == null || od.Items.Count == 0) return null;
            var requirements = new List<OrderRequirement>();
            for (int i = 0; i < od.Items.Count; i++)
            {
                if (od.Items[i] == null || !catalog.TryGetItem(od.Items[i], out _))
                {
                    warnings?.Add("Order " + od.Id + " requests unknown item '" + od.Items[i] + "'; order dropped");
                    return null;
                }
                bool fulfilled = od.Fulfilled != null && i < od.Fulfilled.Count && od.Fulfilled[i];
                requirements.Add(new OrderRequirement(od.Items[i], fulfilled));
            }
            string bonus = od.BonusItemId != null && catalog.TryGetItem(od.BonusItemId, out _) ? od.BonusItemId : null;
            return new Order(od.Id, od.CustomerId ?? OrderRules.Customers[0], od.ChainId, requirements,
                Math.Max(0, od.Glimmer), Math.Max(0, od.Xp), Math.Max(0, od.Lifebloom), bonus);
        }

        private static ItemInstance RestoreItem(string itemId, int? charges, long? refreshTicks, bool? adUsed, ICatalog catalog, IClock clock,
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
            if (def.Generator.IsConsumable)
            {
                // Consumables never refresh; an empty one would have vanished, so it is dropped.
                if (restoredCharges == 0) return null;
                return new ItemInstance(def.Id, new GeneratorState(restoredCharges, null));
            }
            var refreshEnds = FromTicks(refreshTicks, warnings);
            // A partly used generator must have a running cycle, or it would never refresh.
            if (restoredCharges < def.Generator.Capacity && refreshEnds == null)
                refreshEnds = clock.UtcNow + def.Generator.RefreshDuration;
            var state = new GeneratorState(restoredCharges, refreshEnds) { AdSpeedUpUsed = adUsed == true };
            return new ItemInstance(def.Id, state);
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
