using System;
using WorldTree.Core.Items;

namespace WorldTree.Core.Generators
{
    /// <summary>
    /// The refresh-cycle rules of GDD §4.1: the first tap on a full generator starts a fixed timer;
    /// when it ends the generator is full again, however many charges are left. State is evaluated
    /// lazily from absolute UTC timestamps, so it keeps running offline.
    /// </summary>
    public static class GeneratorRules
    {
        public static GeneratorState CreateFull(GeneratorDef def) => new GeneratorState(def.Capacity, null);

        /// <summary>Applies an elapsed refresh and clamps timers distorted by the device clock moving backwards.</summary>
        public static void Refresh(GeneratorState state, GeneratorDef def, DateTime nowUtc)
        {
            if (def.IsConsumable || state.RefreshEndsUtc == null) return;
            DateTime end = state.RefreshEndsUtc.Value;

            // Clock moved backwards: never wait longer than one full cycle (never faster, never stuck).
            if (end - nowUtc > def.RefreshDuration)
            {
                end = nowUtc + def.RefreshDuration;
                state.RefreshEndsUtc = end;
            }

            if (nowUtc >= end)
            {
                state.Charges = def.Capacity;
                state.RefreshEndsUtc = null;
                state.AdSpeedUpUsed = false;
            }
        }

        /// <summary>Uses one charge. The first tap of a cycle (no timer running) starts the refresh timer.</summary>
        public static void ConsumeCharge(GeneratorState state, GeneratorDef def, DateTime nowUtc)
        {
            if (state.Charges <= 0) throw new InvalidOperationException("Generator has no charges.");
            if (!def.IsConsumable && state.RefreshEndsUtc == null) state.RefreshEndsUtc = nowUtc + def.RefreshDuration;
            state.Charges--;
        }

        /// <summary>Completes a running cycle immediately (Moonpetal skip, debug). Consumables are never refilled.</summary>
        public static void ForceRefresh(GeneratorState state, GeneratorDef def)
        {
            if (def.IsConsumable) return;
            state.Charges = def.Capacity;
            state.RefreshEndsUtc = null;
            state.AdSpeedUpUsed = false;
        }

        /// <summary>Remaining time of the running cycle, or zero when none is running.</summary>
        public static TimeSpan Remaining(GeneratorState state, DateTime nowUtc) =>
            state.RefreshEndsUtc.HasValue && state.RefreshEndsUtc.Value > nowUtc ? state.RefreshEndsUtc.Value - nowUtc : TimeSpan.Zero;

        /// <summary>Rewarded-ad speed-up: halves the remaining time, once per cycle (GDD §4.1).</summary>
        public static bool TryAdSpeedUp(GeneratorState state, GeneratorDef def, DateTime nowUtc)
        {
            if (def.IsConsumable || state.AdSpeedUpUsed) return false;
            var remaining = Remaining(state, nowUtc);
            if (remaining <= TimeSpan.Zero) return false;
            state.RefreshEndsUtc = nowUtc + TimeSpan.FromTicks(remaining.Ticks / 2);
            state.AdSpeedUpUsed = true;
            return true;
        }
    }
}
