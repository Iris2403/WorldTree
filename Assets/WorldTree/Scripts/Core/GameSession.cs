using System;
using WorldTree.Core.Board;
using WorldTree.Core.Common;
using WorldTree.Core.Content;
using WorldTree.Core.Energy;
using WorldTree.Core.Generators;
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

        public GameSession(ICatalog catalog, BoardModel board, EnergyModel energy, uint randomState, IClock clock)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Board = board ?? throw new ArgumentNullException(nameof(board));
            Energy = energy ?? throw new ArgumentNullException(nameof(energy));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _random = new XorShiftRandom(randomState);
            Stats = new SessionStats(clock.UtcNow);
        }

        /// <summary>Raised after an item moved to an empty cell: (from, to).</summary>
        public event Action<GridPos, GridPos> ItemMoved;

        /// <summary>Raised after two items swapped cells: (from, to).</summary>
        public event Action<GridPos, GridPos> ItemsSwapped;

        /// <summary>Raised after a merge: (from, to, result). The source cell is now empty.</summary>
        public event Action<GridPos, GridPos, ItemInstance> ItemsMerged;

        /// <summary>Raised after a generator produced an item: (generator, target, item).</summary>
        public event Action<GridPos, GridPos, ItemInstance> ItemSpawned;

        /// <summary>Raised when Sap changed through a command or debug action (not on passive regeneration).</summary>
        public event Action EnergyChanged;

        /// <summary>Raised when generator states changed outside a tap (debug refresh).</summary>
        public event Action GeneratorsChanged;

        public ICatalog Catalog { get; }
        public BoardModel Board { get; }
        public EnergyModel Energy { get; }
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

        public static GameSession NewGame(ICatalog catalog, BoardLayout layout, EnergyConfig energy, IClock clock, uint seed)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var board = new BoardModel(layout.Width, layout.Height);
            foreach (var placement in layout.Placements)
            {
                var def = catalog.GetItem(placement.ItemId);
                var generator = def.IsGenerator ? GeneratorRules.CreateFull(def.Generator) : null;
                board.SetItem(placement.Pos, new ItemInstance(def.Id, generator));
                board.SetState(placement.Pos, placement.State);
            }
            return new GameSession(catalog, board, EnergyModel.Full(energy), seed, clock);
        }

        /// <summary>Drags the item at <paramref name="from"/> onto <paramref name="to"/> (GDD §3.2, §4.3).</summary>
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
                    var resultDef = Catalog.GetItem(decision.ResultItemId);
                    // Generator merges always produce a full generator of the new level, with no timer
                    // running; the inputs' charges and timers are discarded (GDD §4.3).
                    var resultGenerator = resultDef.IsGenerator ? GeneratorRules.CreateFull(resultDef.Generator) : null;
                    var result = new ItemInstance(resultDef.Id, resultGenerator);
                    Board.SetItem(from, null);
                    Board.SetItem(to, result);
                    Stats.Merges++;
                    Changed();
                    ItemsMerged?.Invoke(from, to, result);
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
            var spawnedDef = Catalog.GetItem(def.Generator.Drops.Roll(_random));
            var spawnedGenerator = spawnedDef.IsGenerator ? GeneratorRules.CreateFull(spawnedDef.Generator) : null;
            var spawned = new ItemInstance(spawnedDef.Id, spawnedGenerator);
            Board.SetItem(target, spawned);

            Stats.Taps++;
            Stats.SapSpent += def.Generator.SapCost;
            Changed();
            ItemSpawned?.Invoke(pos, target, spawned);
            EnergyChanged?.Invoke();
            return new TapOutcome(TapResult.Spawned, target, spawned.ItemId);
        }

        /// <summary>The generator state at <paramref name="pos"/> with any elapsed refresh applied, or null.</summary>
        public GeneratorState GetGeneratorState(GridPos pos)
        {
            if (!Board.IsInside(pos)) return null;
            var item = Board.GetItem(pos);
            if (item?.Generator == null) return null;
            GeneratorRules.Refresh(item.Generator, Catalog.GetItem(item.ItemId).Generator, _clock.UtcNow);
            return item.Generator;
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
            Changed();
            GeneratorsChanged?.Invoke();
        }

        private void Changed() => StateVersion++;
    }
}
