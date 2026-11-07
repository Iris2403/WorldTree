# WORLD TREE: Development Roadmap

> Status: **Draft v0.2 (design review 1 applied), awaiting approval.** One phase at a time. Each phase ends with a working, testable result and explicit user approval before the next phase starts.

Legend: ✅ done · 🔄 in progress · ⬜ not started · ⚠ requires Unity Editor validation

## Phase 0: Concept and planning 🔄

- [x] Claude Code project skill `worldtree-game-developer`
- [x] Game Design Document
- [x] Technical Architecture
- [x] Folder and asset structure
- [x] Roadmap
- [x] Phase 1 prototype specification
- [x] Design review 1: generator merging, Withered/Sealed rules, energy budget, save reliability, Unity 6 dependencies, mobile layout (decisions below)
- [ ] **User approval of the above**

### Design review 1: decisions

| # | Topic | Decision | Documented in |
|---|---|---|---|
| D1 | Generator merging | The result is always full capacity of the new level, any pending recharge is discarded, state belongs to the item instance, consumables never merge | GDD §4.3 |
| D2 | Withered and Sealed | Only merges cleanse: the result cell and its orthogonal Withered neighbours become Open, and Sealed cells next to those become Withered. One step, no cascade. Same-ID cleansing merge directly onto a Withered item | GDD §3.2–3.3 |
| D3 | Energy | Keep cap 100 and +1 per 2 min (core player ≈ 415 Sap per day ≈ 4 sessions of 5–8 min). Generator values changed to 40 charges / 20 min at Lv1 so that Sap, not cooldowns, limits sessions. Per-area Sap budgets set | GDD §4.2, §5.1 |
| D4 | Saving | Commit-on-command with coalescing replaces the 2 s debounce. Atomic tmp → bak → json protocol with sequence + SHA-256, newest-valid recovery, critical transactions, full interruption test suite in Phase 1. Save stack moved into Core for .NET CI | Architecture §6 |
| D5 | Unity dependencies | Unity 6.3 LTS. TextMeshPro comes from `com.unity.ugui` 2.0.0 (assembly `Unity.TextMeshPro`), with no standalone TMP package. Full Phase 1 manifest specified | Architecture §2 |
| D6 | Mobile layout | Fixed portrait regions with Regular/Compact/Minimum tiers. Cells ≥ 44 pt on all reference devices (worst case 45.8 pt). Pure-C# layout solver with tests | `06-mobile-layout.md` |

## Phase 1: Project architecture and playable prototype ⬜

**Deliverables:** Unity project skeleton (manifest, asmdefs, folders), Core models for board, merge, generators (including Lv1 → Lv2 merging) and energy, `GameSession`, the full atomic save stack with interruption tests, the layout solver, a graybox portrait board scene built by an Editor menu, drag input, a debug stats overlay, and Core tests plus .NET CI. Full scope in `05-prototype-spec.md`.

**Acceptance:** the Core test suite passes in CI, and in Unity the player can generate, drag, merge, and see Sap drain and regenerate. State survives an app restart, including one right after a merge.

**⚠ Editor validation:** packages resolve and the resolved `manifest.json` / `packages-lock.json` are committed, TMP Essential Resources are imported, Active Input Handling is set to the Input System, everything compiles, the `WorldTree/Setup/Build Prototype Scene` menu builds a working scene, drag feels responsive with mouse and touch (Device Simulator), the layout checklist (`06-mobile-layout.md` §7) passes, and the EditMode tests pass in Test Runner.

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

**Deliverables:** save hardening on devices (atomic writes, recovery and interruption tests already land in Phase 1; this phase adds migration fixtures from real saves, IL2CPP background-write verification and the device kill test), Addressables for area art, local notifications, Localization package, profiling pass against the budgets, Android and iOS build configs, GameCI pipeline (optional, needs a license secret).

**Acceptance:** migration fixture tests, device kill test (Architecture §6.7), budgets met on a reference device.

**⚠ Editor validation:** builds, device profiling, notifications.

## Phase 8: Monetization and release preparation ⬜

**Deliverables:** Unity IAP catalog and store integration, rewarded ads (capped, opt-in), Warden's Pass, privacy and consent flow (GDPR/ATT), analytics events, store listing assets, and a release checklist.

**Acceptance:** purchase and restore flows in sandbox, ad caps enforced by tests, the game remains completable without purchases (simulator).

**⚠ Editor validation:** store sandbox testing on devices, console configuration.
