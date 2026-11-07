# WORLD TREE: Development Roadmap

> Status: **Planning frozen (design review 4).** The GDD and architecture are the implementation baseline. No further design-review cycles. A decision is reopened only when implementation exposes a concrete contradiction or blocker, and the change is recorded here as a new decision.

Legend: ✅ done · 🔄 in progress · ⬜ not started · ⚠ requires Unity Editor validation

## Phase 0: Concept and planning ✅

- [x] Claude Code project skill `worldtree-game-developer`
- [x] Game Design Document
- [x] Technical Architecture
- [x] Folder and asset structure
- [x] Roadmap
- [x] Phase 1 prototype specification
- [x] Design review 1: generator merging, Withered/Sealed rules, energy budget, save reliability, Unity 6 dependencies, mobile layout (decisions below)
- [x] Design review 2: deterministic 7-day energy simulation, generator refresh rule and starting capacity, save concurrency guarantees, cooldown-skip measurement plan (decisions below)
- [x] Design review 3: tap strategies in the simulator, qualified capacity guarantee and order demand rule, durability scope, slow-save stress test, refresh-cycle UI terminology (decisions below)
- [x] Design review 4: simplification pass, planning frozen (decisions below)
- [x] **User approval** (design review 4)

### Design review 1: decisions

| # | Topic | Decision | Documented in |
|---|---|---|---|
| D1 | Generator merging | The result is always full capacity of the new level, any running refresh timer is discarded, state belongs to the item instance, consumables never merge | GDD §4.3 |
| D2 | Withered and Sealed | Only merges cleanse: the result cell and its orthogonal Withered neighbours become Open, and Sealed cells next to those become Withered. One step, no cascade. Same-ID cleansing merge directly onto a Withered item | GDD §3.2–3.3 |
| D3 | Energy | Keep cap 100 and +1 per 2 min (core player ≈ 415 Sap per day ≈ 4 sessions of 5–8 min). Generator values changed to 40 charges / 20 min at Lv1 so that Sap, not cooldowns, limits sessions. Per-area Sap budgets set | GDD §4.2, §5.1 |
| D4 | Saving | Commit-on-command with coalescing replaces the 2 s debounce. Atomic tmp → bak → json protocol with sequence + SHA-256, newest-valid recovery, critical transactions, full interruption test suite in Phase 1. Save stack moved into Core for .NET CI | Architecture §6 |
| D5 | Unity dependencies | Unity 6.3 LTS. TextMeshPro comes from `com.unity.ugui` 2.0.0 (assembly `Unity.TextMeshPro`), with no standalone TMP package. Full Phase 1 manifest specified | Architecture §2 |
| D6 | Mobile layout | Fixed portrait regions with Regular/Compact/Minimum tiers. Cells ≥ 44 pt on all reference devices (worst case 45.8 pt). Pure-C# layout solver with tests | `06-mobile-layout.md` |

### Design review 2: decisions

| # | Topic | Decision | Documented in |
|---|---|---|---|
| D7 | Energy simulation | A deterministic 7-day simulator (`tools/balance/energy_sim.py`) replaces hand estimates. Core player (4 sessions): **412 Sap per day** from regeneration, 10.3 h per day at the cap. Light 206, engaged 477, 24/7 reference exactly 720. Cap 100 and +1 per 2 min unchanged | GDD §5.1, `docs/balance/energy-simulation.md` |
| D8 | Generator refresh rule | The refresh timer starts at the first tap on a full generator, and the generator returns to full when it ends, whatever is left. This replaces "refill only when empty", which stranded leftover charges between sessions | GDD §4.1 |
| D9 | Starting generator capacity | Lv1 capacity 60 (table 60/66/72/80/90). Guaranteed generators must hold ≥ 120 charges at all times (validator rule). The simulation shows zero generator stalls for balanced and order-driven players in every profile (qualified in D13–D14); v0.2 stalled every core session | GDD §4.2 |
| D10 | Cooldown-skip risk | Accepted for now: merging can't create Sap, and the skipped wait is ≤ one refresh timer. The Phase 4 simulator measures it, with a 5% threshold and a predefined fallback rule | GDD §4.3, Phase 4 below |
| D11 | Save concurrency | Guarantees G1–G6: one serialized write queue, monotonic 64-bit sequence that survives restarts, stale-snapshot guard, latest-wins coalescing, crash safety at every file operation, purchase confirmation only after durability. Exhaustive crash and threaded stress tests in Phase 1 | Architecture §6.3–6.7 |

### Design review 3: decisions

| # | Topic | Decision | Documented in |
|---|---|---|---|
| D12 | Simulator tap strategies | The "balanced = worst case" claim was wrong and is removed. The simulator now runs **balanced, focused, order-driven and strict-order** players against a deterministic 3-slot order queue, under four order-demand patterns, and reports Sap spent, stalls, stranded Sap, session length, orders per day and off-order taps | `tools/balance/energy_sim.py`, `docs/balance/energy-simulation.md` |
| D13 | 120-charge guarantee | Qualified: it covers taps spread across generators, not 100 taps on one generator (a Lv1 generator gives 60 per cycle) or sessions starting above the cap | GDD §4.2 |
| D14 | Single-chain stalls | Intended when a player taps one generator by choice (focused players stall every session but couldn't progress orders anyway). Never forced by orders: new **order demand rule**, max share per chain = capacity ÷ 108 rounded down to 5% (55% at Lv1), max 2 consecutive orders per chain. Simulated: 0 stalls for order-driven and strict players; 60% wasn't enough | GDD §4.4, §6.2 |
| D15 | Save durability wording | "Committed" (safe against process termination, guaranteed and tested) is separated from power-loss durability (best effort, file-system dependent, not claimed). `WhenDurable` renamed `WhenCommitted`. Phase 7 adds device power-loss verification and an optional directory-sync hook | Architecture §6.3, §6.5.1 |
| D16 | Slow-save stress test | Added to Phase 1 acceptance: no overtaking, no stale replacement, latest snapshot eventually persisted, single writer, bounded flush | Architecture §6.7, prototype spec §5 |
| D17 | Generator UI terminology | A "refresh cycle": the first tap starts a fixed timer, after which the generator is full whatever is left. States Ready / Refreshing / Waiting for refresh. Wording "refresh", never "recharge" or "empty" | GDD §4.1 |

### Design review 4: simplification and freeze

| # | Topic | Decision | Rationale |
|---|---|---|---|
| D18 | Save system | **Supersedes D4, D11, D15, D16.** Synchronous saves on the main thread, at most one write per frame. Disk flush only when going to the background. Kept: atomic tmp → bak → json protocol, SHA-256, sequence numbers, newest-valid recovery, crash-recovery tests. Removed: background worker, write queue, coalescing slot, stale-snapshot machinery, `WhenCommitted`, threaded and slow-save stress tests, critical-transactions table | The only guarantee made is process-crash safety, which doesn't need a disk flush, so the background thread (added to hide flush cost) and all its concurrency machinery weren't needed. A single thread writing in order can't overtake itself. Revisit only if device profiling (Phase 7) shows saves over 2 ms |
| D19 | Phase 1 scope | Playable graybox and core loop only: board, generators, drag, merge, Sap, basic HUD, debug statistics, simple portrait fit, save and recovery tests | Phase 1 exists to validate feel and measure seconds per Sap. Multi-device layout, UI polish and full generator-state presentation move to later phases |
| D20 | Planning freeze | Current GDD and architecture are the baseline. No new review cycles | The remaining questions are empirical and can only be answered by the prototype |
| D21 | Assemblies | Five: Core, Data, Game (merges Presentation, Platform and App), Editor, Tests | After the save code moved into Core, Platform held only a clock and a path. Engine-free Core and the .NET test path are unchanged |
| D22 | Phase 4 simulation | Port the Python scenarios to C# tests and compare outputs before retiring the Python script, which stays as the reference until then. Merge-cooldown exploit measured by one number: extra taps enabled by skipped refresh waits | Avoids binding production code to a throwaway tool, and measures the one number that can matter |
| D23 | Order demand | Configurable max 55% per chain and max 2 consecutive orders per chain while there are two generators. No capacity-based formula yet | Simplest rule that the simulation showed removes forced stalls. Generalize when a third generator exists |
| D24 | Extras removed | Optional native directory-sync hook removed. One quarantined corrupt save instead of three | No demonstrated need. Re-add only if device testing shows one |
| D25 | Mobile layout | `docs/06-mobile-layout.md` stays the target for Phase 6. Phase 1 only fits the board into the portrait safe area | Part of D19 |

## Phase 1: Project architecture and playable prototype 🔄

**Deliverables:** Unity project skeleton (manifest, five asmdefs, folders), Core models for board, merge, generators (first-tap refresh, Lv1 → Lv2 merging) and energy, `GameSession`, the synchronous atomic save stack with crash-recovery tests, ScriptableObject content definitions, an Editor menu that creates the content assets and the prototype scene, a graybox portrait board with drag input, a basic HUD, a debug statistics overlay, and Core tests plus .NET CI. Full scope in `05-prototype-spec.md`.

**Acceptance:** `dotnet test tools/CoreTests` passes locally and in CI, including the crash-recovery tests. In Unity the player can generate, drag, merge, and see Sap drain and regenerate. State survives an app restart, including one right after a merge.

**Status:**
- ✅ Core gameplay, save stack and content in pure C# (`Assets/WorldTree/Scripts/Core`)
- ✅ Automated tests: 59 passing via `dotnet test tools/CoreTests`, including the exhaustive process-kill test (verified to fail against a deliberately broken protocol). CI workflow added
- ✅ Unity layer written (`Data`, `Game`, `Editor`, manifest). Syntax-checked with Roslyn and type-checked against the real Core plus minimal Unity API stubs. **Not yet compiled in Unity**
- ⬜ Unity Editor validation (checklist below)

**⚠ Editor validation checklist (to be run by the user in Unity 6.3 LTS, steps in README):**
- [ ] The project opens and packages resolve. Commit the resolved `manifest.json` and `packages-lock.json`
- [ ] No compile errors. Specifically check the asmdef references `Unity.TextMeshPro`, `Unity.InputSystem`, `UnityEngine.UI`, and the precompiled `Newtonsoft.Json.dll` in `WorldTree.Core` and `WorldTree.Tests`
- [ ] Active Input Handling = Input System Package (New), and TMP Essential Resources imported
- [ ] *WorldTree → Setup → Build Prototype Scene* creates the assets and `Main.unity` without errors
- [ ] Play: the board, both generators (G1) and level-1 items are visible and the board fits the Game view in portrait
- [ ] Tapping a generator spawns an item with an arc and lowers Sap. The HUD shows "+1 in m:ss" and the selected generator's charges and "Full refresh in m:ss"
- [ ] Dragging identical items merges them with a pop. Different items swap. Dropping outside the board snaps back. Two Seed Pouch G1 merge into a full G2
- [ ] Stop Play right after a merge, press Play again: the board and Sap are restored
- [ ] Debug overlay: statistics update, and +10 Sap, Refresh all generators now and Reset save all work
- [ ] Device Simulator (iPhone SE and a 20:9 Android): the board fits in the safe area, and touch drag lifts the item above the finger
- [ ] Test Runner → EditMode: all `WorldTree.Tests` tests pass

## Phase 2: Merge board and item progression ⬜

**Deliverables:** Withered and Sealed cells with cleansing, generator UI states from GDD §4.1 (Ready / Refreshing / Waiting for refresh), full chain data (Nature, Water, Craft), the Satchel, Sell + Undo, the Grove Codex with first-discovery rewards, merge highlight on drag, item info panel, `ObjectPool` item views, tweener v1, data validator menu.

**Acceptance:** tests cover every row of GDD §3.2 and cleansing propagation. The data validator passes on all assets.

**⚠ Editor validation:** merge and cleanse animations, codex UI, pooled views don't leak.

## Phase 3: Generators and energy ⬜

**Deliverables:** multiple generators, charges and first-tap refresh, generator merging and upgrades (Lv1–5), drop tables, consumable generators, full energy rules (offline regen, over-cap, level-up refill), Daily Gift, `IAdService` no-op hook, Moonpetal skip and refill.

**Acceptance:** fake-clock tests for regen and first-tap refresh across offline gaps, clock-rewind safety, and the board-full guard.

**⚠ Editor validation:** generator ready and cooldown visuals, HUD timers.

## Phase 4: Orders and rewards ⬜

**Deliverables:** OrderBoard and OrderGenerator with the feasibility guarantee and the demand rule (GDD §6.2: configurable 55% per chain, max 2 consecutive), delivery by tap and by drag, rewards (Glimmer, XP, Lifebloom, chests), player levels and unlocks, reward fly-to-HUD, and balance simulation as **C# tests on Core**: the scenarios of `tools/balance/energy_sim.py` ported and their outputs compared with the Python reference. The Python script is retired only after the comparison passes.

**Acceptance:** order feasibility property tests (thousands of seeded runs). The C# scenarios match the Python reference output. Days per area fall within the GDD §5.1 targets. Order-driven players have 0 generator stalls with the real order generator. Extra taps enabled by skipped refresh waits are ≤ 5% of a core player's daily taps (otherwise apply the GDD §4.3 remedy).

**⚠ Editor validation:** order panel UX, reward flow feel.

## Phase 5: Story and world restoration ⬜

**Deliverables:** AreaDefinition and QuestLine, restoration tasks, layered area scene with corrupted/restored cross-fades, dialogue system and scenes for Areas 1–2, story flags, area transitions, Memory Leaf vignettes.

**Acceptance:** quest progression tests, save/restore mid-quest.

**⚠ Editor validation:** area art transitions, dialogue presentation.

## Phase 6: Visual polish, animation and sound ⬜

**Deliverables:** multi-device layout per `06-mobile-layout.md` (tiers, `BoardLayoutSolver` with reference-device tests, scene banner, context bar), final UI skin, item and generator art pipeline (replacing all placeholders), VFX library, blight-dissolve shader, audio integration (music per area, SFX pitch per level), haptics (iOS and Android), accessibility settings, reduced motion.

**Acceptance:** zero placeholder assets remaining (tracked list), and audio and haptic toggles persist.

**⚠ Editor validation:** everything visual and audible, plus haptics on physical devices.

## Phase 7: Saving, optimization and mobile builds ⬜

**Deliverables:** save migrations with fixture tests from real saves, device profiling of save writes against the 2 ms budget, ⚠ a device kill test and a power-off test on at least one Android and one iOS device (results recorded as observed only, no general guarantee), Addressables for area art, local notifications, Localization package, profiling pass against the budgets, Android and iOS build configs, GameCI pipeline (optional, needs a license secret).

**Acceptance:** migration fixture tests, device kill test, budgets met on a reference device.

**⚠ Editor validation:** builds, device profiling, notifications.

## Phase 8: Monetization and release preparation ⬜

**Deliverables:** Unity IAP catalog and store integration, rewarded ads (capped, opt-in), Warden's Pass, privacy and consent flow (GDPR/ATT), analytics events, store listing assets, and a release checklist.

**Acceptance:** purchase and restore flows in sandbox, ad caps enforced by tests, the game remains completable without purchases (simulator).

**⚠ Editor validation:** store sandbox testing on devices, console configuration.
