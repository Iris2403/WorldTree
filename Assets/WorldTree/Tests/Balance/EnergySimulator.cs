using System;
using System.Collections.Generic;
using WorldTree.Core.Energy;
using WorldTree.Core.Generators;
using WorldTree.Core.Items;

namespace WorldTree.Tests.Balance
{
    /// <summary>
    /// C# port of tools/balance/energy_sim.py (roadmap D22), running on the real Core
    /// <see cref="EnergyModel"/> and <see cref="GeneratorRules"/>. The step order mirrors the Python
    /// model exactly so their totals can be compared number for number.
    /// </summary>
    public sealed class EnergySimulator
    {
        public const int DaySeconds = 24 * 3600;
        public const int Days = 7;
        public const int MaxSessionSeconds = 20 * 60;
        private const int ActiveOrders = 3;
        private static readonly DateTime Epoch = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static int Hm(int h, int m = 0) => h * 3600 + m * 60;

        public static readonly Dictionary<string, int[]> Profiles = new Dictionary<string, int[]>
        {
            { "Light", new[] { Hm(8), Hm(20) } },
            { "Core", new[] { Hm(8), Hm(12, 30), Hm(17, 30), Hm(21, 30) } },
            { "Engaged", new[] { Hm(8), Hm(10, 30), Hm(13), Hm(15, 30), Hm(18), Hm(20, 30) } },
            { "Reference", new[] { 0, Hm(3, 20), Hm(6, 40), Hm(10), Hm(13, 20), Hm(16, 40), Hm(20), Hm(23, 20) } },
        };

        /// <summary>(generator index, taps), repeating. Same as ORDER_PATTERNS in the Python script.</summary>
        public static readonly Dictionary<string, (int Gen, int Taps)[]> Patterns = new Dictionary<string, (int, int)[]>
        {
            { "even", new[] { (0, 12), (1, 10), (0, 8), (1, 14), (0, 16), (1, 8), (0, 10), (1, 12) } },
            { "a-heavy", new[] { (0, 12), (0, 8), (1, 10), (0, 16), (0, 10), (1, 8), (0, 14), (1, 12), (0, 20) } },
            { "capped-60", new[] { (0, 12), (0, 8), (1, 10), (0, 16), (1, 8), (0, 10), (0, 14), (1, 12), (1, 10) } },
            { "capped-55", new[] { (0, 12), (1, 10), (0, 8), (0, 14), (1, 12), (0, 10), (1, 9), (0, 16), (1, 18) } },
        };

        public struct Totals
        {
            public int Spent, Regenerated, Stalls, Stranded, Orders, Lifebloom;
        }

        /// <summary>Supplies the next order as (generator index, taps).</summary>
        public interface IOrderSource
        {
            /// <param name="charges">Current charges per generator.</param>
            /// <param name="openTaps">Taps still needed by the active orders, per generator.</param>
            (int Gen, int Taps) Next(int[] charges, int[] openTaps);

            /// <summary>Lifebloom paid by the order most recently returned by <see cref="Next"/> (0 if not tracked).</summary>
            int LastLifebloom { get; }
        }

        public sealed class PatternSource : IOrderSource
        {
            private readonly (int Gen, int Taps)[] _pattern;
            private int _next;
            public PatternSource(string name) => _pattern = Patterns[name];
            public (int Gen, int Taps) Next(int[] charges, int[] openTaps) => _pattern[_next++ % _pattern.Length];
            public int LastLifebloom => 0;
        }

        public static Totals Run(int[] schedule, IOrderSource orders, string strategy, int[] capacities, int secondsPerSap)
        {
            var config = new EnergyConfig(100, TimeSpan.FromSeconds(120));
            var energy = EnergyModel.Full(config);
            var drops = new DropTable(new[] { new DropEntry("item.nature.01", 1) });
            var defs = new GeneratorDef[capacities.Length];
            var gens = new GeneratorState[capacities.Length];
            for (int i = 0; i < capacities.Length; i++)
            {
                defs[i] = new GeneratorDef(capacities[i], TimeSpan.FromMinutes(20), 1, drops);
                gens[i] = GeneratorRules.CreateFull(defs[i]);
            }
            var active = new List<int[]>(); // [generator, remaining taps, Lifebloom]
            while (active.Count < ActiveOrders) Draw(active, orders, gens);

            var sorted = (int[])schedule.Clone();
            Array.Sort(sorted);
            var totals = new Totals();
            for (int d = 0; d < Days; d++)
            {
                foreach (int start in sorted)
                {
                    int t = d * DaySeconds + start;
                    int sessionStart = t;
                    while (t - sessionStart < MaxSessionSeconds)
                    {
                        var now = Epoch.AddSeconds(t);
                        energy.Refresh(now);
                        for (int i = 0; i < gens.Length; i++) GeneratorRules.Refresh(gens[i], defs[i], now);
                        if (energy.Amount <= 0) break;
                        int pick = Choose(strategy, gens, active);
                        if (pick < 0)
                        {
                            totals.Stalls++;
                            totals.Stranded += energy.Amount;
                            break;
                        }
                        energy.TrySpend(1, now);
                        GeneratorRules.ConsumeCharge(gens[pick], defs[pick], now);
                        ApplyTap(active, orders, gens, pick, ref totals);
                        totals.Spent++;
                        t += secondsPerSap;
                    }
                }
            }
            energy.Refresh(Epoch.AddSeconds(Days * DaySeconds));
            totals.Regenerated = totals.Spent + energy.Amount - config.Cap;
            return totals;
        }

        private static void Draw(List<int[]> active, IOrderSource orders, GeneratorState[] gens)
        {
            var charges = new int[gens.Length];
            var open = new int[gens.Length];
            for (int i = 0; i < gens.Length; i++) charges[i] = gens[i].Charges;
            foreach (var o in active) open[o[0]] += o[1];
            var (gen, taps) = orders.Next(charges, open);
            active.Add(new[] { gen, taps, orders.LastLifebloom });
        }

        private static void ApplyTap(List<int[]> active, IOrderSource orders, GeneratorState[] gens, int gen, ref Totals totals)
        {
            foreach (var order in active)
            {
                if (order[0] != gen) continue;
                order[1]--;
                if (order[1] == 0)
                {
                    active.Remove(order);
                    totals.Orders++;
                    totals.Lifebloom += order[2];
                    Draw(active, orders, gens);
                }
                return;
            }
        }

        private static int Choose(string strategy, GeneratorState[] gens, List<int[]> active)
        {
            switch (strategy)
            {
                case "balanced":
                    int best = 0;
                    for (int i = 1; i < gens.Length; i++) if (gens[i].Charges > gens[best].Charges) best = i;
                    return gens[best].Charges > 0 ? best : -1;
                case "focused":
                    return gens[0].Charges > 0 ? 0 : -1;
                case "order-driven":
                    foreach (var order in active) if (gens[order[0]].Charges > 0) return order[0];
                    return -1;
                case "strict-order":
                    return gens[active[0][0]].Charges > 0 ? active[0][0] : -1;
                default:
                    throw new ArgumentException(strategy);
            }
        }
    }
}
