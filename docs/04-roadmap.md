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
| D26 | Project marker | `ProjectSettings/ProjectVersion.txt` (one line, `m_EditorVersion: 6000.3.0f1`) is committed by hand. It's the only hand-written ProjectSettings file; everything else in `ProjectSettings/` is generated by Unity | Unity Hub and Unity's repository import only recognize a folder as a project when this file exists (found during Phase 1 validation). Unity rewrites it with the exact installed version on first open, and that version gets committed |
| D27 | Glimmer in Phase 2 | A minimal Glimmer wallet is introduced in Phase 2. First discoveries pay Glimmer only (2 per item level); XP stays in Phase 4 | Selling and first-discovery rewards (Phase 2 scope) both pay Glimmer, which the roadmap had placed in Phase 4. Found while implementing Phase 2 |
| D28 | Unlockable-board rule | The validator checks the exact condition "every locked region has a Withered cell next to an Open cell", replacing the looser GDD §3.3 wording | The original wording allowed boards that can never be fully unlocked (a Sealed cell next to Open cells only). Found while implementing the validator |
| D29 | Phase 2 content | Nature and Water extended to 8 levels; new Craft chain (6 levels) with the Woodwright's Stump; the starting board gains a Withered row and two Sealed rows that hide the Stump | Phase 2 needs full chain data and a board that exercises the blight rules. IDs are unchanged, so Phase 1 saves still load |
| D30 | Freeing Withered cells | **Supersedes part of GDD §3.3 rule 2.** A Withered item is freed only by dropping a matching item onto it; a merge next to it no longer frees it. A Withered generator is still freed by a merge next to it. Sealed cells still turn Withered when a neighbour is freed. The validator rejects locked max-level items, but allows locked items whose chain has no generator on the board yet | User feedback from play-testing Phase 2: merging beside blighted items freed them too easily. Generators are the exception because a matching generator is rarely available. Chains may become obtainable in later levels |
| D31 | Small-centre start | The starting board opens only the 3 × 3 centre (Seed Pouch in the middle, 8 free places). A Withered ring surrounds it, with the Moonwell Spring next to the centre; the rest is Sealed. The 120-charge rule counts Withered generators next to an Open cell | User request: start small with one generator and expand by merging. A test with a simple automated player opens the whole board in about 90–100 actions (22–28 taps) on three seeds and frees the Moonwell Spring within the first few actions |
| D32 | Moonpetals in Phase 3 | Moonpetals are added to the wallet now. Debug overlay grants them for testing; real sources (day-7 gift now, level-ups and purchases later) | Refresh skips and Sap refills (Phase 3 scope) cost Moonpetals, which the roadmap had placed in Phase 4. Found while implementing Phase 3 |
| D33 | Level-up Sap refill | Moves to Phase 4 | It needs player levels and XP, which are Phase 4 |
| D34 | Skip cost | 1 Moonpetal per started 5 minutes of remaining refresh time, minimum 1 (a fresh Lv1 cycle costs 4) | The GDD named the mechanic without a price. Simple, proportional, and cheap enough to try in the prototype; tuned in Phase 4 |
| D35 | Daily Gift contents and ads | 7-day calendar per GDD §5 (contents listed there), one claim per local day via a new `IClock.LocalUtcOffset`. Ad rewards (refresh speed-up, +25 Sap up to 5/day) are implemented in Core behind an `IAdService` that is disabled until Phase 8; a debug "fake ads" switch tests them | Daily limits reset at local midnight (GDD §5), which needs local time. Ads are Phase 8, but their game rules are cheap to finish and test now |
| D36 | Order rewards and levels | Order value = sum of level-1 equivalents of its items (a level-k item = 2^(k−1)). Rewards: Glimmer = value, XP = value, Lifebloom = max(1, (value + 2) / 4); every 5th order adds a Mystery Acorn. XP to next level = 15 × level; each level-up refills Sap to the cap and pays 10 × level Glimmer and 2 Moonpetals; first discoveries also give XP (= item level). Orders ask for 1 item below level 3, up to 2 below level 6, up to 3 after | The GDD names these rewards without numbers. Simple formulas tied to merge cost, tuned once area costs exist (Phase 5) |
| D37 | Demand rule implementation | With two or more chains, each new order is the candidate (one rolled per chain) that leaves the least demand over the last 20 orders including itself, max 2 in a row per chain, with a re-roll guard against exceeding 55% in the window | Choosing the chain first and sizing afterwards let 1 seed in 3000 reach 56%. Balancing on the window including the new order's size keeps the worst 60-order stream at 54.1% (average 50.9%) |
| D38 | Orders the generators can cover | The generator also avoids orders that, together with the other open orders on that chain, need more than the chain's generators can still produce this cycle. Result: 0 stalls for order-driven players with real orders (3 profiles × 4 levels × 50 seeds). "Strict" players (only the oldest order) stop early in 1.05% of sessions, capped at 2% by a test | Real orders grow larger than the Python patterns (up to ~39 taps), so a 50/50 average alone still produced occasional forced stalls |
| D39 | Days per area | Checked in Phase 5, when area restoration costs exist. Phase 4 provides the Lifebloom income they will be set from | "Days per area" needs Lifebloom costs per area, which are Phase 5 content |
| D40 | Area costs and days per area | Area 1 Sunlit Glade: 10 tasks, 500 Lifebloom. Area 2 Emberroot Village: 10 tasks, 900 Lifebloom. Set from simulated Lifebloom income of a core player with real orders: Glade 1.7–2.4 days (levels 2–3), Village 6.0–6.8 days (levels 6–8), checked by a test against the GDD §5.1 targets (about 2–3 and about 6 more) | Completes D39. Lifebloom per Sap is higher early (small orders pay at least 1 Lifebloom), so costs are set per area from measured income, not from the Sap budget alone |
| D41 | Phase 5 scope cuts | Tasks are restored in order and cost Lifebloom only. Story orders (GDD §6.2) and replaying scenes from the Codex are not built yet. The area view is a graybox: one tile per task cross-fading from blighted to restored; layered area art comes in Phase 6. Areas 3–5 are not built; after Area 2 the World panel says more is coming | Keeps Phase 5 to the roadmap's deliverables. Each cut can be added later without changing saves |
| D42 | Art and audio sourcing | **User decision:** stand-ins now, real assets later. Phase 6 delivers the pipeline (icon slots in the chain assets, audio slots in `AudioService`, `docs/art/asset-spec.md`, `docs/art/placeholders.md`, *WorldTree → Art → Report Placeholders*) and polished generated stand-ins (shapes per chain, rounded UI, particles, synthesized SFX and music). Phase 6 acceptance "zero placeholders" becomes "every placeholder is tracked and replaceable without code changes" until real assets arrive | Final painted art and composed audio can't be produced in this environment, and unlicensed assets are not allowed |
| D43 | Blight dissolve | The blight cover swells and fades with a particle burst instead of a dissolve shader. A shader is written once real blight art exists, because it needs iteration in the Editor | A hand-written shader can't be tested here; a broken one renders pink |

## Phase 1: Project architecture and playable prototype 🔄

**Deliverables:** Unity project skeleton (manifest, five asmdefs, folders), Core models for board, merge, generators (first-tap refresh, Lv1 → Lv2 merging) and energy, `GameSession`, the synchronous atomic save stack with crash-recovery tests, ScriptableObject content definitions, an Editor menu that creates the content assets and the prototype scene, a graybox portrait board with drag input, a basic HUD, a debug statistics overlay, and Core tests plus .NET CI. Full scope in `05-prototype-spec.md`.

**Acceptance:** `dotnet test tools/CoreTests` passes locally and in CI, including the crash-recovery tests. In Unity the player can generate, drag, merge, and see Sap drain and regenerate. State survives an app restart, including one right after a merge.

**Status:**
- ✅ Core gameplay, save stack and content in pure C# (`Assets/WorldTree/Scripts/Core`)
- ✅ Automated tests: 59 passing via `dotnet test tools/CoreTests`, including the exhaustive process-kill test (verified to fail against a deliberately broken protocol). CI workflow added
- ✅ Unity layer written (`Data`, `Game`, `Editor`, manifest). Syntax-checked with Roslyn and type-checked against the real Core plus minimal Unity API stubs. **Not yet compiled in Unity**
- 🔄 Unity Editor validation: play-tested by the user in Unity 6.6. Unity-generated project files committed by the user. Remaining: tests don't show in the Unity Test Runner yet (they pass in .NET CI)

**⚠ Editor validation checklist (to be run by the user in Unity 6.3 LTS, steps in README):**
- [x] The project opens and packages resolve (opened in Unity 6.6, 6000.6.7f1). Resolved `manifest.json` and `packages-lock.json` committed
- [x] No compile errors (after fixing Tweener's `Start` name clash and adding the GameDatabase Resources fallback). Specifically check the asmdef references `Unity.TextMeshPro`, `Unity.InputSystem`, `UnityEngine.UI`, and the precompiled `Newtonsoft.Json.dll` in `WorldTree.Core` and `WorldTree.Tests`
- [x] Active Input Handling = Input System Package (New), and TMP Essential Resources imported
- [x] *WorldTree → Setup → Build Prototype Scene* creates the assets and `Main.unity` without errors
- [x] Play: the board, both generators (G1) and level-1 items are visible and the board fits the Game view in portrait
- [x] Tapping a generator spawns an item with an arc and lowers Sap. The HUD shows "+1 in m:ss" and the selected generator's charges and "Full refresh in m:ss"
- [x] Dragging identical items merges them with a pop. Different items swap. Dropping outside the board snaps back. Two Seed Pouch G1 merge into a full G2
- [x] Stop Play right after a merge, press Play again: the board and Sap are restored
- [x] Debug overlay: statistics update, and +10 Sap, Refresh all generators now and Reset save all work
- [x] Device Simulator (iPhone SE and a 20:9 Android): the board fits in the safe area, and touch drag lifts the item above the finger
- [ ] Test Runner → EditMode: all `WorldTree.Tests` tests pass

## Phase 2: Merge board and item progression ✅

**Deliverables:** Withered and Sealed cells with cleansing, generator UI states from GDD §4.1 (Ready / Refreshing / Waiting for refresh), full chain data (Nature, Water, Craft), the Satchel, Sell + Undo, the Grove Codex with first-discovery rewards, merge highlight on drag, item info panel, `ObjectPool` item views, tweener v1, data validator menu.

**Acceptance:** tests cover every row of GDD §3.2 and cleansing propagation. The data validator passes on all assets.

**Status:**
- ✅ Core: blight rules (cleansing merge, cleanse + reveal, one step, ordered events), layout validator, Glimmer wallet, Codex with first-discovery rewards, 4-slot Satchel, Sell + 5 s Undo, 8-level Nature/Water, Craft chain and Woodwright's Stump, new starting board, saves (Phase 1 saves still load)
- ✅ Tests: 102 passing via `dotnet test tools/CoreTests` (43 new). Cleansing tests verified to fail against a cascading-reveal mutation
- ✅ Unity layer written: blight and Sealed covers, merge hints while dragging, generator charge / countdown line and dimming while waiting, bottom bar (Sell, Store, Undo, Satchel, Codex), Satchel and Codex panels, discovery messages, drag onto Satchel to store, *WorldTree → Validate Data*, *WorldTree → Setup → Update Content Assets to Code Defaults*. Type-checked against Unity API stubs only. **Not yet run in Unity**
- ✅ Unity Editor validation: play-tested by the user (blight rules incl. D30, small-centre start D31, satchel, selling and undo, Codex, generator status, save/restore)

**⚠ Editor validation checklist:**
- [ ] After pulling: *WorldTree → Setup → Build Prototype Scene* → choose **Update**. Then *WorldTree → Validate Data* reports "All data is valid"
- [ ] Press Play, then Debug → **Reset save (new game)** to start on the new board (D31): only the 3 × 3 centre is open with the Seed Pouch in the middle, a purple Withered ring around it, dark Sealed cells outside
- [ ] Merging next to a Withered **item** doesn't free it. Dropping a matching item onto it frees it (tint disappears) and the Sealed cell above turns Withered (roadmap D30)
- [ ] Merging next to a Withered **generator** frees it
- [ ] Dropping a matching item directly onto a Withered item merges and cleanses it; a different item bounces back
- [ ] While dragging, matching items (including Withered ones) are outlined
- [ ] Generators show their charges under the frame; when empty they dim and show a countdown
- [ ] Selecting an item shows Sell +N and Store. Selling adds Glimmer and shows Undo (5 s countdown) which restores it. Generators show no Sell button
- [ ] Store, or dragging an item onto the Satchel button, moves it into the Satchel (max 4); tapping a slot in the Satchel panel puts it back near the bottom of the board
- [ ] New items show "New discovery … +N Glimmer"; the Codex panel lists discovered names and ??? for the rest
- [ ] Revealing and freeing the Woodwright's Stump (top row) lets you tap it for Craft items
- [ ] Stop and restart Play: blight state, Satchel, Glimmer and Codex are restored
- [ ] Buttons don't trigger board taps underneath; the board still fits above the bottom bar in Device Simulator

## Phase 3: Generators and energy ✅

**Deliverables:** multiple generators, charges and first-tap refresh, generator merging and upgrades (Lv1–5), drop tables, consumable generators, full energy rules (offline regen, over-cap; level-up refill moved to Phase 4, D33), Daily Gift, `IAdService` no-op hook, Moonpetal skip and refill.

**Acceptance:** fake-clock tests for regen and first-tap refresh across offline gaps, clock-rewind safety, and the board-full guard.

**Status:**
- ✅ Core: Seed Pouch, Moonwell Spring and Woodwright's Stump at levels 1–5 (GDD §4.2 table), Mystery Acorn consumable generator, Moonpetals, refresh skip, Sap refill with escalating daily cost, rewarded-ad speed-up and ad Sap (daily cap), Daily Gift, local-day resets, saves (older saves still load)
- ✅ Tests: 136 passing via `dotnet test tools/CoreTests` (24 new)
- ✅ Unity layer written: Moonpetals in the HUD, Skip and ad speed-up buttons for a selected refreshing generator, Gift panel, Sap+ panel (refill, watch ad), Mystery Acorn visuals, debug buttons (+20 Moonpetals, allow Daily Gift again, fake ads on/off). Type-checked against Unity API stubs only. **Not yet run in Unity**
- ✅ Unity Editor validation: play-tested by the user

**⚠ Editor validation checklist:**
- [ ] After pulling: *WorldTree → Setup → Build Prototype Scene* → **Update**, then *WorldTree → Validate Data* is clean
- [ ] Two Seed Pouch G1 merge into G2, and so on up to G5; each upgrade is full and shows more charges
- [ ] Gift button shows "Gift!" once per day; the panel lists 7 days; Claim grants the reward. Debug → *Allow Daily Gift again* steps through: day 3 places a Seed Pouch sprout, day 5 a Mystery Acorn (round "?"), day 7 gives 20 Moonpetals
- [ ] The Mystery Acorn shows taps left, gives 8 items, then disappears; it can be sold and never merges
- [ ] Select a refreshing generator: **Skip (N MP)** appears; with enough Moonpetals it becomes full at once
- [ ] Sap+ panel: refill gives +100 Sap; the next refill costs double
- [ ] Debug → *Fake ads: ON*: "Ad: ½ time" appears for a refreshing generator (once per cycle), and the Sap+ panel shows "Watch ad" (5 per day)
- [ ] Stop and restart Play: Moonpetals, gift progress, refill cost and ad counts are kept

## Phase 4: Orders and rewards ✅

**Deliverables:** OrderBoard and OrderGenerator with the feasibility guarantee and the demand rule (GDD §6.2), delivery by tap and by drag, rewards (Glimmer, XP, Lifebloom, chests), player levels and unlocks, reward fly-to-HUD, and balance simulation as **C# tests on Core**: the scenarios of `tools/balance/energy_sim.py` ported and their outputs compared with the Python reference.

**Acceptance:** order feasibility property tests (thousands of seeded runs). The C# scenarios match the Python reference output. Days per area fall within the GDD §5.1 targets (moved to Phase 5, D39). Order-driven players have 0 generator stalls with the real order generator. Extra taps enabled by skipped refresh waits are ≤ 5% of a core player's daily taps.

**Status:**
- ✅ Core: orders (3 slots, 4th at level 8), feasibility, demand rule (D37), coverable orders (D38), Deliver and drag-to-order delivery, swap once per 30 min, rewards and bonus acorns, XP and levels with the Sap refill on level-up (D33, D36), saves
- ✅ Balance: `Tests/Balance/EnergySimulator.cs` reproduces every Python reference scenario exactly (the reference table is generated with `python3 tools/balance/energy_sim.py --csharp`); 0 stalls for order-driven players with real orders; strict players 1.05% (cap 2%); merge-skipped waits ≤ 4.6% of taps (cap 5%)
- ✅ Property test: 3000 seeded order streams are feasible, never 3 in a row from one chain when another is available, and stay ≤ 55% per chain
- ✅ Tests: 244 passing via `dotnet test tools/CoreTests`
- ✅ Unity layer written: orders strip with cards (customer, items with ✓, rewards, Deliver, Swap), drag an item onto a card to deliver it, level / XP / Lifebloom in the top bar, rewards fly up from the strip, level-up message, debug +50 XP. Type-checked against Unity API stubs only. **Not yet run in Unity**
- ✅ Unity Editor validation: play-tested by the user (orders strip, Deliver, drag to order, swap, levels, saves). Follow-up fix: order cards used a ✓ glyph missing from the default TMP font; items on the board are now shown in green

**⚠ Editor validation checklist:**
- [ ] Orders strip shows 3 cards under the top bar; the board still fits below it in Device Simulator
- [ ] A card turns green with Deliver enabled when all its items are on the board; Deliver removes them, a reward line flies up, and a new order appears
- [ ] Dragging a matching item onto a card delivers it (strikethrough); a non-matching item bounces back with a message
- [ ] Swap replaces an order, then shows the 30-minute cooldown message
- [ ] At the start, orders only ask for Nature items; after freeing the Moonwell Spring, new orders also ask for Water
- [ ] Level, XP and Lifebloom update; Debug → +50 XP levels up, refills Sap and shows "Level up!"; at level 8 a 4th order appears
- [ ] Stop and restart Play: orders (including delivered pieces), level, XP and Lifebloom are kept

## Phase 5: Story and world restoration ✅

**Deliverables:** AreaDefinition and QuestLine, restoration tasks, layered area scene with corrupted/restored cross-fades (graybox tiles for now, D41), dialogue system and scenes for Areas 1–2, story flags, area transitions, Memory Leaf vignettes.

**Acceptance:** quest progression tests, save/restore mid-quest. Plus the days-per-area check moved from Phase 4 (D39).

**Status:**
- ✅ Core: areas with ordered restoration tasks paid in Lifebloom, area completion (flag, Memory Leaf, rewards that are never lost, completion scene, next area and intro), dialogue queue, saves (older saves start at Area 1)
- ✅ Content: Area 1 Sunlit Glade (reward: Hearthstone Oven), Area 2 Emberroot Village (first Memory Leaf, reward: Moonwell Spring Lv2), 9 original scenes; Food chain and Hearthstone Oven Lv1–5
- ✅ Balance: days per area with real orders within targets (D40)
- ✅ Tests: 260 passing via `dotnet test tools/CoreTests` (16 new)
- ✅ Unity layer written: area and dialogue assets (created by the setup menu), World panel (task tiles cross-fade, Restore button), dialogue box (Next, Skip; Memory Leaf lines in gold italics), story validation in *Validate Data* and at boot, debug +100 Lifebloom. Type-checked against Unity API stubs only. **Not yet run in Unity**
- ✅ Unity Editor validation: play-tested by the user

**⚠ Editor validation checklist:**
- [ ] After pulling: *WorldTree → Setup → Build Prototype Scene* → **Update** (creates the area and dialogue assets), then *Validate Data* is clean
- [ ] Debug → *Reset save (new game)*: Pip's intro scene appears; Next steps through it, Skip closes it
- [ ] World button opens Sunlit Glade with 10 grey-violet tiles and "Next: Clear the withered path, cost 15"
- [ ] With enough Lifebloom (Debug → +100 Lifebloom), Restore cross-fades the first tile to colour; the second task plays Elder Thalen's scene
- [ ] Restoring all 10 glade tasks shows the completion scene, places the Hearthstone Oven on the board, then Marra's village intro; new orders can ask for Food
- [ ] Restoring Emberroot Village plays the Memory Leaf scene (gold italic lines) and gives a Moonwell Spring Lv2; the World panel then says more is coming
- [ ] Stop and restart Play mid-area: restored tiles, the next task and unseen scenes are kept

## Phase 6: Visual polish, animation and sound 🔄

**Deliverables:** multi-device layout per `06-mobile-layout.md` (tiers, `BoardLayoutSolver` with reference-device tests, scene banner, context bar), UI skin, item and generator art pipeline, VFX library, blight dissolve, audio integration (music per area, SFX pitch per level), haptics (iOS and Android), accessibility settings, reduced motion. Art and audio are generated stand-ins until real assets arrive (D42).

**Acceptance:** every placeholder tracked in `docs/art/placeholders.md` and replaceable without code changes (D42); audio and haptic toggles persist.

**Status:**
- ✅ Core: `BoardLayoutSolver` with tests for all 7 reference devices and a 44–80 pt sweep; settings (music, sound, haptics, reduced motion, large text) saved, with defaults for older saves
- ✅ Tests: 271 passing via `dotnet test tools/CoreTests`
- ✅ Unity layer written: `LayoutController` applies the solver (tiers, scene banner with area progress, context bar) and fits the board camera; stand-in art (leaf / drop / crate / round / star shapes with shading, rounded 9-slice UI); real icons replace stand-ins when assigned; blight dissolve (D43); particle bursts for merges and cleansing; synthesized SFX (merge chime pitched per level, pop, bell, reward, level-up, reject, click) and ambient music per area with crossfade; haptics (iOS native plugin, Android Vibrator); Settings panel; reduced motion; large text; *WorldTree → Art → Report Placeholders*. Type-checked against Unity API stubs only. **Not yet run in Unity, and haptics need real devices**
- ⬜ Unity Editor validation (checklist below)

**⚠ Editor validation checklist:**
- [ ] Items show their stand-in shapes (Nature leaves, Water drops, Craft crates, Food rounds, Acorn star); panels and buttons are rounded
- [ ] Merging plays a chime that rises with the item level and a coloured sparkle burst; spawning pops; cleansing a blighted cell plays a bell, the cover swells and fades, with a violet burst
- [ ] Ambient music plays and changes after an area is restored; buttons click
- [ ] Settings (top right): Music, Sound effects, Vibration, Reduced motion (no particles, instant moves), Large text (bigger dialogue and info) all work and survive a restart
- [ ] Device Simulator: iPhone SE uses the compact layout, a 20:9 Android and Pro Max show the area banner, iPad caps cells at 80 pt; the board sits just above the bottom bar on all of them
- [ ] *WorldTree → Art → Report Placeholders* lists the items without icons; assigning a test sprite as an icon replaces that item's stand-in
- [ ] On a real iPhone and Android phone (Phase 7 builds): merges tick, level-ups buzz, and Vibration off silences them

## Phase 7: Saving, optimization and mobile builds ⬜

**Deliverables:** save migrations with fixture tests from real saves, device profiling of save writes against the 2 ms budget, ⚠ a device kill test and a power-off test on at least one Android and one iOS device (results recorded as observed only, no general guarantee), Addressables for area art, local notifications, Localization package, profiling pass against the budgets, Android and iOS build configs, GameCI pipeline (optional, needs a license secret).

**Acceptance:** migration fixture tests, device kill test, budgets met on a reference device.

**⚠ Editor validation:** builds, device profiling, notifications.

## Phase 8: Monetization and release preparation ⬜

**Deliverables:** Unity IAP catalog and store integration, rewarded ads (capped, opt-in), Warden's Pass, privacy and consent flow (GDPR/ATT), analytics events, store listing assets, and a release checklist.

**Acceptance:** purchase and restore flows in sandbox, ad caps enforced by tests, the game remains completable without purchases (simulator).

**⚠ Editor validation:** store sandbox testing on devices, console configuration.
