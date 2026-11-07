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
            if (state.RefreshEndsUtc == null) return;
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
            }
        }

        /// <summary>Uses one charge. The first tap of a cycle (no timer running) starts the refresh timer.</summary>
        public static void ConsumeCharge(GeneratorState state, GeneratorDef def, DateTime nowUtc)
        {
            if (state.Charges <= 0) throw new InvalidOperationException("Generator has no charges.");
            if (state.RefreshEndsUtc == null) state.RefreshEndsUtc = nowUtc + def.RefreshDuration;
            state.Charges--;
        }

        /// <summary>Debug helper: completes any running cycle immediately.</summary>
        public static void ForceRefresh(GeneratorState state, GeneratorDef def)
        {
            state.Charges = def.Capacity;
            state.RefreshEndsUtc = null;
        }
    }
}
