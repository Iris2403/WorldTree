# WORLD TREE: Phase 1 Playable Prototype Specification

> Status: **Draft v0.2 (design review 1 applied), awaiting approval.**

## 1. Purpose

Prove the **core micro loop** on the real architecture: *spend Sap → tap generator → drag → merge → save*. The goal is to validate feel and structure, not content or art. Graybox visuals are acceptable **in this phase only**.

## 2. In scope

### 2.1 Project skeleton

- `Packages/manifest.json` exactly as in Architecture §2.2 (TextMeshPro through `com.unity.ugui`, no standalone TMP package)
- All `.asmdef` files and the folder structure (Project Structure §1)
- Unity `.gitignore`
- `tools/CoreTests` .NET project and a GitHub Actions workflow running it

### 2.2 Core (pure C#, fully unit-tested)

- `ItemId`, `ChainId`, `GridPos`, `ItemDef`, `ChainDef`, `GeneratorDef`, `DropTable`, `ICatalog` and an in-memory `Catalog`
- `BoardModel` (open cells only in this phase; the cell-state enum already includes Withered and Sealed for Phase 2)
- `MergeRules`: Move, Merge, Swap and Reject, exactly as in GDD §3.2 (minus order delivery)
- `GeneratorService`: tap with the GDD §4.1 order of checks, cycle-model charges and recharge with absolute timestamps and clock clamping, nearest-empty placement (Manhattan distance, ties by row then column), generator merging per GDD §4.3 (Lv1 → Lv2 in this phase)
- `EnergyModel`: cap 100, +1 every 120 s, offline regen, over-cap via `Add`, clock-rewind safety
- `IClock`, `IRandom` (seeded XorShift), test fakes
- `GameSession` facade: `DragItem`, `TapCell`, plus the events views need, and the `StateVersion` counter
- Save stack per Architecture §6: `SaveData` v1 DTOs, `SaveEnvelope` (sequence + SHA-256), `SaveSerializer`, `AtomicSaveStore` over `IFileSystem`, recovery loader, `SaveScheduler` (commit-on-command, coalescing, lifecycle flush, retry), `PhysicalFileSystem`, and a `FaultInjectingFileSystem` for tests
- `BoardLayoutSolver` (`WorldTree.Core.Layout`): the cell-size and tier algorithm from `06-mobile-layout.md` §3

### 2.3 Content (ScriptableObjects, created by an Editor setup menu)

- **Nature chain**, 6 levels: Glimmerseed → Sprout → Sapling → Blossom Shrub → Young Oak → Elder Oak
- **Water chain**, 6 levels: Dewdrop → Water Flask → Moon Pitcher → Spring Basin → Crystal Fount → Moonwell
- **Seed Pouch** generator (Lv1–2): Nature drops, values from GDD §4.2 (Lv1: 40 charges, 20 min recharge; Lv2: 48 charges, 18 min). A debug toggle shortens recharge to 30 s
- **Moonwell Spring** generator (Lv1): Water drops, same Lv1 values
- Two extra Seed Pouch Lv1 items on the starting board, so generator merging can be tested
- Starting board 7×9: two generators plus a few level-1 items

### 2.4 Presentation (graybox)

- Board rendered with world-space sprites: rounded-square cells, items drawn as a coloured shape per chain plus a level number (generated placeholder sprites, no external art)
- `DragController` on the Input System: tap vs drag threshold, lift, follow, release, snap-back on reject
- Minimal tweens: move/snap (ease-out), merge pop, spawn arc
- Portrait layout via `LayoutController` + `BoardLayoutSolver`: HUD bar, an empty orders-strip placeholder, board, and context bar placeholder, sized per `06-mobile-layout.md` (finger-lift drag offset included)
- HUD: Sap counter, next +1 timer, generator charge and cooldown indicator
- Debug overlay (development builds only): add Sap, reset save, fast recharge toggle, and a **stats panel** (Sap spent, taps, merges per minute) to measure the GDD §5.1 "3–5 s per Sap" assumption
- `WorldTree/Setup/Build Prototype Scene` editor menu that creates `Main.unity` with camera, board and HUD wired, so no hand-written scene YAML is needed

### 2.5 Platform and App

- `SystemClock`, save directory from `Application.persistentDataPath`
- Lifecycle forwarding (`OnApplicationPause`, `OnApplicationFocus`, `OnApplicationQuit`) to `SaveScheduler.Flush()`

## 3. Out of scope (later phases)

Withered and Sealed cells, satchel, selling, codex (Phase 2) · generator upgrades beyond Lv2, daily gift, ads (Phase 3) · orders, currencies other than Sap, XP (Phase 4) · story, areas (Phase 5) · final art, VFX, audio, haptics (Phase 6) · mobile builds (Phase 7) · monetization (Phase 8).

## 4. Required Core tests (minimum)

| Area | Cases |
|---|---|
| Merge | Same-ID merge produces the next level in the target cell and frees the source · max-level same-ID swaps · different IDs swap · drop on empty moves · drop on self is a no-op · out-of-bounds rejected |
| Generator | Spawns into the nearest empty Open cell with the documented tie-break · board full → no Sap or charge spent · no charge → no Sap spent · no Sap → no charge spent · charges reach 0 → recharge → full capacity at exactly `rechargeEndsUtc` (fake clock) · recharge completes while offline · clock moved back → remaining time clamped to the full duration · drop table respects weights (seeded distribution test) · generator state follows the item when moved or swapped |
| Generator merge | Every row of GDD §4.3 that applies to Phase 1: full + depleted → full Lv2 · both depleted → full Lv2 · partial + partial → full Lv2 · different types swap · generator + item swap · result in the target cell |
| Energy | Regen +1 per 120 s up to the cap · offline gap of 5 h regenerates to the cap only · over-cap `Add` is kept, with no regen while over the cap · clock moved backwards → no gain, no crash · `TrySpend` fails atomically when insufficient |
| Save | Every test in Architecture §6.7 except the Phase 7 device kill test and the Phase 8 purchase test |
| Layout | `BoardLayoutSolver` returns the tier and cell size for every reference device in `06-mobile-layout.md` §4 (±0.1 pt) · cell never below 44 or above 80 |

## 5. Acceptance criteria

1. `dotnet test tools/CoreTests` passes locally and in GitHub Actions.
2. ⚠ The project opens in Unity 6 LTS with no compile errors, and packages resolve.
3. ⚠ Running *WorldTree → Setup → Build Prototype Scene* then pressing Play shows a playable board.
4. ⚠ Tapping a generator spawns items and decreases Sap. Dragging identical items merges them with a pop. Dragging onto a different item swaps. Invalid drops snap back.
5. ⚠ Sap regenerates while playing and after restarting Play mode, and board state is restored after restart, including when Play mode is stopped seconds after a merge.
6. ⚠ The EditMode tests pass in the Unity Test Runner.
7. ⚠ Drag stays smooth in Device Simulator (for example, iPhone and mid-range Android profiles), with no visible hitches.
8. ⚠ The layout checklist in `06-mobile-layout.md` §7 passes for the board, HUD and placeholder regions.

Items marked ⚠ are verified by the user in the Unity Editor. Claude will provide a step-by-step validation checklist with the Phase 1 delivery.
