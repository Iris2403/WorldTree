using System;
using System.Collections.Generic;
using WorldTree.Core.Board;
using WorldTree.Core.Collection;
using WorldTree.Core.Common;
using WorldTree.Core.Content;
using WorldTree.Core.Economy;
using WorldTree.Core.Energy;
using WorldTree.Core.Generators;
using WorldTree.Core.Inventory;
using WorldTree.Core.Items;

namespace WorldTree.Core
{
    /// <summary>
    /// Command entry point for gameplay (architecture §4.1). Views never mutate models directly; they
    /// call these commands and react to the events. Every command validates first and mutates second,
    /// so a failed command leaves the state untouched. Each successful command increments
    /// <see cref="StateVersion"/>, which the save driver uses to decide when to write.
    /// </summary>
    public sealed class GameSession
    {
        private readonly IClock _clock;
        private readonly XorShiftRandom _random;
        private readonly List<GridPos> _cleansed = new List<GridPos>();
        private readonly List<GridPos> _revealed = new List<GridPos>();
        private readonly List<GridPos> _changedCells = new List<GridPos>();
        private SaleRecord _lastSale;

        public GameSession(ICatalog catalog, BoardModel board, EnergyModel energy, uint randomState, IClock clock)
            : this(catalog, board, energy, new Wallet(0), new Codex(), new Satchel(EconomyRules.StartingSatchelSlots), randomState, clock)
        {
        }

        public GameSession(ICatalog catalog, BoardModel board, EnergyModel energy, Wallet wallet, Codex codex, Satchel satchel,
            uint randomState, IClock clock, DailyGift dailyGift = null, DailyLimits dailyLimits = null)
        {
            DailyGift = dailyGift ?? new DailyGift(0, null);
            DailyLimits = dailyLimits ?? new DailyLimits(null, 0, 0);
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Board = board ?? throw new ArgumentNullException(nameof(board));
            Energy = energy ?? throw new ArgumentNullException(nameof(energy));
            Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            Codex = codex ?? throw new ArgumentNullException(nameof(codex));
            Satchel = satchel ?? throw new ArgumentNullException(nameof(satchel));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _random = new XorShiftRandom(randomState);
            Stats = new SessionStats(clock.UtcNow);
            DiscoverVisibleSilently();
        }

        /// <summary>Raised after an item moved to an empty cell: (from, to).</summary>
        public event Action<GridPos, GridPos> ItemMoved;

        /// <summary>Raised after two items swapped cells: (from, to).</summary>
        public event Action<GridPos, GridPos> ItemsSwapped;

        /// <summary>Raised after a merge: (from, to, result). The source cell is now empty.</summary>
        public event Action<GridPos, GridPos, ItemInstance> ItemsMerged;

        /// <summary>
        /// Raised after a merge for each cell that was cleansed or revealed, in row-major order,
        /// after <see cref="ItemsMerged"/> (GDD §3.3 rule 6).
        /// </summary>
        public event Action<GridPos, CellState> CellStateChanged;

        /// <summary>Raised after a generator produced an item: (generator, target, item).</summary>
        public event Action<GridPos, GridPos, ItemInstance> ItemSpawned;

        /// <summary>Raised on the first discovery of an item: (item ID, Glimmer reward).</summary>
        public event Action<string, int> ItemDiscovered;

        /// <summary>Raised after an item left the board into the satchel: (cell).</summary>
        public event Action<GridPos> ItemStored;

        /// <summary>Raised after an item came back from the satchel: (cell, item).</summary>
        public event Action<GridPos, ItemInstance> ItemRetrieved;

        /// <summary>Raised after an item was sold: (cell, Glimmer received).</summary>
        public event Action<GridPos, int> ItemSold;

        /// <summary>Raised after a sale was undone: (cell, item).</summary>
        public event Action<GridPos, ItemInstance> SaleUndone;

        /// <summary>Raised when Sap changed through a command or debug action (not on passive regeneration).</summary>
        public event Action EnergyChanged;

        /// <summary>Raised when Glimmer changed.</summary>
        public event Action WalletChanged;

        /// <summary>Raised when the satchel's contents changed.</summary>
        public event Action SatchelChanged;

        /// <summary>Raised when generator states changed outside a tap (skip, ad speed-up, debug refresh).</summary>
        public event Action GeneratorsChanged;

        /// <summary>Raised when a consumable generator used its last tap and vanished: (cell).</summary>
        public event Action<GridPos> GeneratorExhausted;

        /// <summary>Raised when a reward item was placed on the board: (cell, item). Rewards that went to the satchel raise <see cref="SatchelChanged"/>.</summary>
        public event Action<GridPos, ItemInstance> ItemAwarded;

        public ICatalog Catalog { get; }
        public BoardModel Board { get; }
        public EnergyModel Energy { get; }
        public Wallet Wallet { get; }
        public Codex Codex { get; }
        public Satchel Satchel { get; }
        public DailyGift DailyGift { get; }
        public DailyLimits DailyLimits { get; }
        public SessionStats Stats { get; }
        public long StateVersion { get; private set; }
        public uint RandomState => _random.State;
        public DateTime UtcNow => _clock.UtcNow;

        /// <summary>Current Sap including elapsed regeneration.</summary>
        public int Sap
        {
            get
            {
                Energy.Refresh(_clock.UtcNow);
                return Energy.Amount;
            }
        }

        /// <summary>Seconds left to undo the last sale, or 0 when nothing can be undone.</summary>
        public double SellUndoSecondsLeft
        {
            get
            {
                if (_lastSale == null) return 0;
                double left = (EconomyRules.SellUndoWindow - (_clock.UtcNow - _lastSale.SoldAtUtc)).TotalSeconds;
                return left > 0 ? left : 0;
            }
        }

        /// <summary>Moonpetal cost of the next Sap refill today (GDD §5).</summary>
        public int SapRefillCost
        {
            get
            {
                DailyLimits.RollOver(LocalDay.Today(_clock));
                return EconomyRules.SapRefillCost(DailyLimits.SapRefills);
            }
        }

        /// <summary>Rewarded-ad Sap claims left today.</summary>
        public int AdSapLeftToday
        {
            get
            {
                DailyLimits.RollOver(LocalDay.Today(_clock));
                return Math.Max(0, EconomyRules.AdSapDailyCap - DailyLimits.AdSapClaims);
            }
        }

        public bool CanClaimDailyGift => DailyGift.CanClaim(LocalDay.Today(_clock));

        public static GameSession NewGame(ICatalog catalog, BoardLayout layout, EnergyConfig energy, IClock clock, uint seed)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var board = new BoardModel(layout.Width, layout.Height);
            foreach (var placement in layout.Placements)
            {
                var def = catalog.GetItem(placement.ItemId);
                board.SetItem(placement.Pos, new ItemInstance(def.Id, def.IsGenerator ? GeneratorRules.CreateFull(def.Generator) : null));
                board.SetState(placement.Pos, placement.State);
            }
            return new GameSession(catalog, board, EnergyModel.Full(energy), seed, clock);
        }

        /// <summary>Drags the item at <paramref name="from"/> onto <paramref name="to"/> (GDD §3.2, §3.3, §4.3).</summary>
        public MoveDecision DragItem(GridPos from, GridPos to)
        {
            var decision = MergeRules.Resolve(Board, Catalog, from, to);
            switch (decision.Kind)
            {
                case MoveKind.Move:
                    Board.SetItem(to, Board.GetItem(from));
                    Board.SetItem(from, null);
                    Stats.Moves++;
                    Changed();
                    ItemMoved?.Invoke(from, to);
                    break;

                case MoveKind.Swap:
                    var a = Board.GetItem(from);
                    Board.SetItem(from, Board.GetItem(to));
                    Board.SetItem(to, a);
                    Stats.Swaps++;
                    Changed();
                    ItemsSwapped?.Invoke(from, to);
                    break;

                case MoveKind.Merge:
                    ApplyMerge(from, to, decision.ResultItemId);
                    break;
            }
            return decision;
        }

        /// <summary>
        /// Taps a cell. On a generator this produces an item, checking in the order of GDD §4.1:
        /// Open → has a charge → an empty Open cell exists → enough Sap. Any failed check changes nothing.
        /// </summary>
        public TapOutcome TapCell(GridPos pos)
        {
            if (!Board.IsInside(pos)) return TapOutcome.Fail(TapResult.OutOfBounds);
            var item = Board.GetItem(pos);
            if (item == null) return TapOutcome.Fail(TapResult.Empty);
            if (Board.GetState(pos) != CellState.Open) return TapOutcome.Fail(TapResult.Locked);
            var def = Catalog.GetItem(item.ItemId);
            if (!def.IsGenerator || item.Generator == null) return TapOutcome.Fail(TapResult.NotAGenerator);

            var now = _clock.UtcNow;
            GeneratorRules.Refresh(item.Generator, def.Generator, now);
            if (item.Generator.Charges <= 0) return TapOutcome.Fail(TapResult.NoCharges);
            if (!Board.TryFindNearestEmptyOpen(pos, out var target)) return TapOutcome.Fail(TapResult.BoardFull);
            if (!Energy.TrySpend(def.Generator.SapCost, now)) return TapOutcome.Fail(TapResult.NotEnoughSap);

            GeneratorRules.ConsumeCharge(item.Generator, def.Generator, now);
            var spawned = CreateInstance(Catalog.GetItem(def.Generator.Drops.Roll(_random)));
            Board.SetItem(target, spawned);

            bool exhausted = def.Generator.IsConsumable && item.Generator.Charges == 0;
            if (exhausted) Board.SetItem(pos, null);

            Stats.Taps++;
            Stats.SapSpent += def.Generator.SapCost;
            Changed();
            ItemSpawned?.Invoke(pos, target, spawned);
            if (exhausted) GeneratorExhausted?.Invoke(pos);
            EnergyChanged?.Invoke();
            DiscoverWithReward(spawned.ItemId);
            return new TapOutcome(TapResult.Spawned, target, spawned.ItemId);
        }

        /// <summary>Moves an Open item into the satchel (GDD §3.5).</summary>
        public StoreResult StoreInSatchel(GridPos pos)
        {
            if (!Board.IsInside(pos)) return StoreResult.OutOfBounds;
            var item = Board.GetItem(pos);
            if (item == null) return StoreResult.Empty;
            if (Board.GetState(pos) != CellState.Open) return StoreResult.Locked;
            if (Satchel.IsFull) return StoreResult.SatchelFull;

            Board.SetItem(pos, null);
            Satchel.Add(item);
            Changed();
            ItemStored?.Invoke(pos);
            SatchelChanged?.Invoke();
            return StoreResult.Stored;
        }

        /// <summary>
        /// Returns a satchel item to the nearest empty Open cell, measured from the bottom-centre of the
        /// board (where the satchel button sits).
        /// </summary>
        public RetrieveResult RetrieveFromSatchel(int slot, out GridPos placedAt)
        {
            placedAt = default;
            if (slot < 0 || slot >= Satchel.Count) return RetrieveResult.InvalidSlot;
            var origin = new GridPos(Board.Width / 2, Board.Height - 1);
            if (!Board.TryFindNearestEmptyOpen(origin, out placedAt)) return RetrieveResult.BoardFull;

            var item = Satchel.RemoveAt(slot);
            Board.SetItem(placedAt, item);
            Changed();
            ItemRetrieved?.Invoke(placedAt, item);
            SatchelChanged?.Invoke();
            return RetrieveResult.Retrieved;
        }

        /// <summary>"Return to Roots": sells an Open item for its sell value in Glimmer (GDD §3.5).</summary>
        public SellResult SellItem(GridPos pos)
        {
            if (!Board.IsInside(pos)) return SellResult.OutOfBounds;
            var item = Board.GetItem(pos);
            if (item == null) return SellResult.Empty;
            if (Board.GetState(pos) != CellState.Open) return SellResult.Locked;
            var def = Catalog.GetItem(item.ItemId);
            // Permanent generators can't be sold; consumable ones can (GDD §3.5).
            if (def.IsGenerator && !def.Generator.IsConsumable) return SellResult.NotSellable;

            Board.SetItem(pos, null);
            Wallet.AddGlimmer(def.SellValue);
            _lastSale = new SaleRecord(pos, item, def.SellValue, _clock.UtcNow);
            Changed();
            ItemSold?.Invoke(pos, def.SellValue);
            WalletChanged?.Invoke();
            return SellResult.Sold;
        }

        /// <summary>
        /// Undoes the last sale within <see cref="EconomyRules.SellUndoWindow"/>: the Glimmer is taken
        /// back and the item returns to its cell, or to the nearest empty Open cell if that one is taken.
        /// </summary>
        public UndoSellResult UndoLastSale()
        {
            if (_lastSale == null) return UndoSellResult.NothingToUndo;
            if (_clock.UtcNow - _lastSale.SoldAtUtc > EconomyRules.SellUndoWindow)
            {
                _lastSale = null;
                return UndoSellResult.Expired;
            }

            var pos = _lastSale.Pos;
            if (!Board.IsEmptyOpen(pos) && !Board.TryFindNearestEmptyOpen(pos, out pos)) return UndoSellResult.BoardFull;
            if (!Wallet.TrySpendGlimmer(_lastSale.Value)) return UndoSellResult.NotEnoughGlimmer;

            var item = _lastSale.Item;
            Board.SetItem(pos, item);
            _lastSale = null;
            Changed();
            SaleUndone?.Invoke(pos, item);
            WalletChanged?.Invoke();
            return UndoSellResult.Restored;
        }

        /// <summary>The generator state at <paramref name="pos"/> with any elapsed refresh applied, or null.</summary>
        public GeneratorState GetGeneratorState(GridPos pos)
        {
            if (!Board.IsInside(pos)) return null;
            return RefreshedGenerator(Board.GetItem(pos));
        }

        /// <summary>The generator state of a satchel item with any elapsed refresh applied, or null.</summary>
        public GeneratorState GetSatchelGeneratorState(int slot) =>
            slot >= 0 && slot < Satchel.Count ? RefreshedGenerator(Satchel.Items[slot]) : null;

        /// <summary>Moonpetal cost to finish the running refresh of the generator at <paramref name="pos"/> now, or 0.</summary>
        public int SkipCostAt(GridPos pos)
        {
            var state = GetGeneratorState(pos);
            return state == null ? 0 : EconomyRules.SkipCost(GeneratorRules.Remaining(state, _clock.UtcNow));
        }

        /// <summary>Spends Moonpetals to make a refreshing generator full now (GDD §4.1).</summary>
        public SkipResult SkipRefresh(GridPos pos)
        {
            if (!Board.IsInside(pos) || Board.GetItem(pos) == null) return SkipResult.NotAGenerator;
            if (Board.GetState(pos) != CellState.Open) return SkipResult.Locked;
            var item = Board.GetItem(pos);
            var def = Catalog.GetItem(item.ItemId);
            if (!def.IsGenerator || def.Generator.IsConsumable) return SkipResult.NotAGenerator;
            var state = GetGeneratorState(pos);
            int cost = EconomyRules.SkipCost(GeneratorRules.Remaining(state, _clock.UtcNow));
            if (cost == 0) return SkipResult.NothingToSkip;
            if (!Wallet.TrySpendMoonpetals(cost)) return SkipResult.NotEnoughMoonpetals;

            GeneratorRules.ForceRefresh(state, def.Generator);
            Changed();
            GeneratorsChanged?.Invoke();
            WalletChanged?.Invoke();
            return SkipResult.Skipped;
        }

        /// <summary>
        /// Rewarded-ad speed-up: halves a generator's remaining refresh time, once per cycle. Call only
        /// after the ad was watched to the end; Core doesn't know about ads (architecture §4.2).
        /// </summary>
        public PurchaseResult ApplyAdSpeedUp(GridPos pos)
        {
            var state = GetGeneratorState(pos);
            if (state == null || Board.GetState(pos) != CellState.Open) return PurchaseResult.NotApplicable;
            var def = Catalog.GetItem(Board.GetItem(pos).ItemId).Generator;
            if (!GeneratorRules.TryAdSpeedUp(state, def, _clock.UtcNow)) return PurchaseResult.NotApplicable;
            Changed();
            GeneratorsChanged?.Invoke();
            return PurchaseResult.Done;
        }

        /// <summary>+100 Sap for Moonpetals; the cost doubles with each refill on the same local day (GDD §5).</summary>
        public PurchaseResult RefillSap()
        {
            int cost = SapRefillCost; // also rolls the daily counters over
            if (!Wallet.TrySpendMoonpetals(cost)) return PurchaseResult.NotEnoughMoonpetals;
            DailyLimits.CountSapRefill();
            Energy.Add(EconomyRules.SapRefillAmount, _clock.UtcNow);
            Changed();
            EnergyChanged?.Invoke();
            WalletChanged?.Invoke();
            return PurchaseResult.Done;
        }

        /// <summary>
        /// +25 Sap for a watched rewarded ad, at most 5 times per local day (GDD §5). Call only after the
        /// ad was watched to the end.
        /// </summary>
        public PurchaseResult GrantAdSap()
        {
            if (AdSapLeftToday <= 0) return PurchaseResult.DailyLimitReached; // also rolls the counters over
            DailyLimits.CountAdSapClaim();
            Energy.Add(EconomyRules.AdSapAmount, _clock.UtcNow);
            Changed();
            EnergyChanged?.Invoke();
            return PurchaseResult.Done;
        }

        /// <summary>
        /// Claims today's Daily Gift (GDD §5). Item rewards go to the nearest empty Open cell from the
        /// bottom-centre, or to the satchel if the board is full. If they can't all fit, nothing is granted.
        /// </summary>
        public ClaimResult ClaimDailyGift()
        {
            int today = LocalDay.Today(_clock);
            if (!DailyGift.CanClaim(today)) return ClaimResult.AlreadyClaimedToday;
            var rewards = DailyGift.RewardsFor(DailyGift.NextDayIndex);

            int items = 0;
            foreach (var r in rewards) if (r.Kind == RewardKind.Item) items++;
            if (items > CountEmptyOpen() + (Satchel.Capacity - Satchel.Count)) return ClaimResult.NoRoom;

            DailyGift.MarkClaimed(today);
            Changed();
            foreach (var r in rewards) Grant(r);
            return ClaimResult.Claimed;
        }

        public void DebugAddMoonpetals(int amount)
        {
            Wallet.AddMoonpetals(amount);
            Changed();
            WalletChanged?.Invoke();
        }

        /// <summary>Debug: lets today's Daily Gift be claimed again, to step through the calendar.</summary>
        public void DebugAllowDailyGiftAgain()
        {
            DailyGift.DebugAllowClaimAgain();
            Changed();
        }

        public void DebugAddSap(int amount)
        {
            Energy.Add(amount, _clock.UtcNow);
            Changed();
            EnergyChanged?.Invoke();
        }

        public void DebugRefreshAllGenerators()
        {
            for (int y = 0; y < Board.Height; y++)
            {
                for (int x = 0; x < Board.Width; x++)
                {
                    var item = Board.GetItem(new GridPos(x, y));
                    if (item?.Generator == null) continue;
                    GeneratorRules.ForceRefresh(item.Generator, Catalog.GetItem(item.ItemId).Generator);
                }
            }
            foreach (var item in Satchel.Items)
            {
                if (item.Generator != null) GeneratorRules.ForceRefresh(item.Generator, Catalog.GetItem(item.ItemId).Generator);
            }
            Changed();
            GeneratorsChanged?.Invoke();
        }

        private void ApplyMerge(GridPos from, GridPos to, string resultItemId)
        {
            // Cleansing is computed from the board as it was before the merge (GDD §3.3 rule 4).
            Cleansing.Compute(Board, Catalog, to, _cleansed, _revealed);

            // Generator merges always produce a full generator of the new level, with no timer running;
            // the inputs' charges and timers are discarded (GDD §4.3).
            var result = CreateInstance(Catalog.GetItem(resultItemId));
            Board.SetItem(from, null);
            Board.SetItem(to, result);
            foreach (var c in _cleansed) Board.SetState(c, CellState.Open);
            foreach (var r in _revealed) Board.SetState(r, CellState.Withered);

            Stats.Merges++;
            Changed();
            ItemsMerged?.Invoke(from, to, result);

            _changedCells.Clear();
            _changedCells.AddRange(_cleansed);
            _changedCells.AddRange(_revealed);
            _changedCells.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            foreach (var c in _changedCells) CellStateChanged?.Invoke(c, Board.GetState(c));

            DiscoverWithReward(result.ItemId);
            foreach (var c in _cleansed)
            {
                var freed = Board.GetItem(c);
                if (freed != null) DiscoverWithReward(freed.ItemId);
            }
        }

        private void Grant(Reward reward)
        {
            switch (reward.Kind)
            {
                case RewardKind.Sap:
                    Energy.Add(reward.Amount, _clock.UtcNow);
                    EnergyChanged?.Invoke();
                    break;
                case RewardKind.Glimmer:
                    Wallet.AddGlimmer(reward.Amount);
                    WalletChanged?.Invoke();
                    break;
                case RewardKind.Moonpetals:
                    Wallet.AddMoonpetals(reward.Amount);
                    WalletChanged?.Invoke();
                    break;
                case RewardKind.Item:
                    var item = CreateInstance(Catalog.GetItem(reward.ItemId));
                    var origin = new GridPos(Board.Width / 2, Board.Height - 1);
                    if (Board.TryFindNearestEmptyOpen(origin, out var cell))
                    {
                        Board.SetItem(cell, item);
                        ItemAwarded?.Invoke(cell, item);
                    }
                    else
                    {
                        Satchel.Add(item);
                        SatchelChanged?.Invoke();
                    }
                    DiscoverWithReward(item.ItemId);
                    break;
            }
        }

        private int CountEmptyOpen()
        {
            int n = 0;
            for (int y = 0; y < Board.Height; y++)
                for (int x = 0; x < Board.Width; x++)
                    if (Board.IsEmptyOpen(new GridPos(x, y))) n++;
            return n;
        }

        private void DiscoverWithReward(string itemId)
        {
            if (!Codex.Discover(itemId)) return;
            int reward = Catalog.GetItem(itemId).Level * EconomyRules.DiscoveryGlimmerPerLevel;
            Wallet.AddGlimmer(reward);
            ItemDiscovered?.Invoke(itemId, reward);
            WalletChanged?.Invoke();
        }

        /// <summary>
        /// Items the player can already see and use (Open cells, satchel) count as discovered without a
        /// reward. Also brings saves from before the Codex existed up to date.
        /// </summary>
        private void DiscoverVisibleSilently()
        {
            for (int y = 0; y < Board.Height; y++)
            {
                for (int x = 0; x < Board.Width; x++)
                {
                    var p = new GridPos(x, y);
                    var item = Board.GetItem(p);
                    if (item != null && Board.GetState(p) == CellState.Open) Codex.Discover(item.ItemId);
                }
            }
            foreach (var item in Satchel.Items) Codex.Discover(item.ItemId);
        }

        private GeneratorState RefreshedGenerator(ItemInstance item)
        {
            if (item?.Generator == null) return null;
            GeneratorRules.Refresh(item.Generator, Catalog.GetItem(item.ItemId).Generator, _clock.UtcNow);
            return item.Generator;
        }

        private static ItemInstance CreateInstance(ItemDef def) =>
            new ItemInstance(def.Id, def.IsGenerator ? GeneratorRules.CreateFull(def.Generator) : null);

        private void Changed() => StateVersion++;

        private sealed class SaleRecord
        {
            public SaleRecord(GridPos pos, ItemInstance item, int value, DateTime soldAtUtc)
            {
                Pos = pos;
                Item = item;
                Value = value;
                SoldAtUtc = soldAtUtc;
            }

            public GridPos Pos { get; }
            public ItemInstance Item { get; }
            public int Value { get; }
            public DateTime SoldAtUtc { get; }
        }
    }
}
