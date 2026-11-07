# WORLD TREE: Development Roadmap

> Status: **Draft v0.1, awaiting approval.** One phase at a time. Each phase ends with a working, testable result and explicit user approval before the next phase starts.

Legend: ✅ done · 🔄 in progress · ⬜ not started · ⚠ requires Unity Editor validation

## Phase 0: Concept and planning 🔄

- [x] Claude Code project skill `worldtree-game-developer`
- [x] Game Design Document
- [x] Technical Architecture
- [x] Folder and asset structure
- [x] Roadmap
- [x] Phase 1 prototype specification
- [ ] **User approval of the above**

## Phase 1: Project architecture and playable prototype ⬜

**Deliverables:** Unity project skeleton (manifest, asmdefs, folders), Core models for board, merge, a single generator and energy, `GameSession`, a minimal save, a graybox board scene built by an Editor menu, drag input, and Core tests plus .NET CI. Full scope in `05-prototype-spec.md`.

**Acceptance:** the Core test suite passes in CI, and in Unity the player can generate, drag, merge, and see Sap drain and regenerate. State survives an app restart.

**⚠ Editor validation:** packages resolve, everything compiles, the `WorldTree/Setup/Build Prototype Scene` menu builds a working scene, drag feels responsive with mouse and touch (Device Simulator), and the EditMode tests pass in Test Runner.

## Phase 2: Merge board and item progression ⬜

**Deliverables:** Withered and Sealed cells with cleansing, full chain data (Nature, Water, Craft), the Satchel, Sell + Undo, the Grove Codex with first-discovery rewards, merge highlight on drag, item info panel, `ObjectPool` item views, tweener v1, data validator menu.

**Acceptance:** tests cover every row of GDD §3.2 and cleansing propagation. The data validator passes on all assets.

**⚠ Editor validation:** merge and cleanse animations, codex UI, pooled views don't leak.

## Phase 3: Generators and energy ⬜

**Deliverables:** multiple generators, charges and recharge, generator merging and upgrades (Lv1–5), drop tables, consumable generators, full energy rules (offline regen, over-cap, level-up refill), Daily Gift, `IAdService` no-op hook, Moonpetal skip and refill.

**Acceptance:** fake-clock tests for regen and recharge across offline gaps, clock-rewind safety, and the board-full guard.

**⚠ Editor validation:** generator ready and cooldown visuals, HUD timers.

## Phase 4: Orders and rewards ⬜

**Deliverables:** OrderBoard and OrderGenerator with the feasibility guarantee, delivery by tap and by drag, rewards (Glimmer, XP, Lifebloom, chests), player levels and unlocks, reward fly-to-HUD, and a **balance simulator** editor window.

**Acceptance:** order feasibility property tests (thousands of seeded runs), and the simulator reports days-per-area estimates.

**⚠ Editor validation:** order panel UX, reward flow feel.

## Phase 5: Story and world restoration ⬜

**Deliverables:** AreaDefinition and QuestLine, restoration tasks, layered area scene with corrupted/restored cross-fades, dialogue system and scenes for Areas 1–2, story flags, area transitions, Memory Leaf vignettes.

**Acceptance:** quest progression tests, save/restore mid-quest.

**⚠ Editor validation:** area art transitions, dialogue presentation.

## Phase 6: Visual polish, animation and sound ⬜

**Deliverables:** final UI skin, item and generator art pipeline (replacing all placeholders), VFX library, blight-dissolve shader, audio integration (music per area, SFX pitch per level), haptics (iOS and Android), accessibility settings, reduced motion.

**Acceptance:** zero placeholder assets remaining (tracked list), and audio and haptic toggles persist.

**⚠ Editor validation:** everything visual and audible, plus haptics on physical devices.

## Phase 7: Saving, optimization and mobile builds ⬜

**Deliverables:** save hardening (atomic writes, backup, migrations, checksum), Addressables for area art, local notifications, Localization package, profiling pass against the budgets, Android and iOS build configs, GameCI pipeline (optional, needs a license secret).

**Acceptance:** migration fixture tests, kill-during-save test, budgets met on a reference device.

**⚠ Editor validation:** builds, device profiling, notifications.

## Phase 8: Monetization and release preparation ⬜

**Deliverables:** Unity IAP catalog and store integration, rewarded ads (capped, opt-in), Warden's Pass, privacy and consent flow (GDPR/ATT), analytics events, store listing assets, and a release checklist.

**Acceptance:** purchase and restore flows in sandbox, ad caps enforced by tests, the game remains completable without purchases (simulator).

**⚠ Editor validation:** store sandbox testing on devices, console configuration.
