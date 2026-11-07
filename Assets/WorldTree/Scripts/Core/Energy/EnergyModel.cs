using System;

namespace WorldTree.Core.Energy
{
    /// <summary>
    /// Sap with the exact timer rule of GDD §5: the regen timer starts when Sap drops below the cap
    /// (spending at t schedules +1 at t + interval) and stops when Sap reaches the cap. Rewards may
    /// push Sap above the cap; the cap only limits regeneration. Evaluated lazily from UTC timestamps.
    /// </summary>
    public sealed class EnergyModel
    {
        private int _amount;
        private DateTime? _nextTickUtc;

        public EnergyModel(EnergyConfig config, int amount, DateTime? nextTickUtc)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            _amount = amount;
            _nextTickUtc = amount >= config.Cap ? null : nextTickUtc;
        }

        public EnergyConfig Config { get; }

        /// <summary>Raw stored amount. Call <see cref="Refresh"/> first to include elapsed regeneration.</summary>
        public int Amount => _amount;

        /// <summary>Time of the next +1, or null while at or above the cap.</summary>
        public DateTime? NextTickUtc => _nextTickUtc;

        public static EnergyModel Full(EnergyConfig config) => new EnergyModel(config, config.Cap, null);

        /// <summary>Applies regeneration up to <paramref name="nowUtc"/>.</summary>
        public void Refresh(DateTime nowUtc)
        {
            if (_amount >= Config.Cap)
            {
                _nextTickUtc = null;
                return;
            }

            if (_nextTickUtc == null)
            {
                // Below the cap without a timer (for example a hand-edited save): start one now.
                _nextTickUtc = nowUtc + Config.RegenInterval;
                return;
            }

            // Clock moved backwards: the next tick is never more than one interval away.
            if (_nextTickUtc.Value - nowUtc > Config.RegenInterval) _nextTickUtc = nowUtc + Config.RegenInterval;

            while (_nextTickUtc != null && _nextTickUtc.Value <= nowUtc)
            {
                _amount++;
                if (_amount >= Config.Cap) _nextTickUtc = null;
                else _nextTickUtc = _nextTickUtc.Value + Config.RegenInterval;
            }
        }

        /// <summary>Spends Sap if enough is available. Atomic: on failure nothing changes.</summary>
        public bool TrySpend(int cost, DateTime nowUtc)
        {
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
            Refresh(nowUtc);
            if (_amount < cost) return false;
            _amount -= cost;
            if (_amount < Config.Cap && _nextTickUtc == null) _nextTickUtc = nowUtc + Config.RegenInterval;
            return true;
        }

        /// <summary>Adds Sap from a reward. May exceed the cap.</summary>
        public void Add(int amount, DateTime nowUtc)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Refresh(nowUtc);
            _amount += amount;
            if (_amount >= Config.Cap) _nextTickUtc = null;
        }
    }
}
