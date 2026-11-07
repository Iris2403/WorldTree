# WORLD TREE: Phase 1 Playable Prototype Specification

> Status: **Draft v0.1, awaiting approval.**

## 1. Purpose

Prove the **core micro loop** on the real architecture: *spend Sap → tap generator → drag → merge → save*. The goal is to validate feel and structure, not content or art. Graybox visuals are acceptable **in this phase only**.

## 2. In scope

### 2.1 Project skeleton

- `Packages/manifest.json` with the Phase 1 packages (Architecture §2)
- All `.asmdef` files and the folder structure (Project Structure §1)
- Unity `.gitignore`
- `tools/CoreTests` .NET project and a GitHub Actions workflow running it

### 2.2 Core (pure C#, fully unit-tested)

- `ItemId`, `ChainId`, `GridPos`, `ItemDef`, `ChainDef`, `GeneratorDef`, `DropTable`, `ICatalog` and an in-memory `Catalog`
- `BoardModel` (open cells only in this phase; the cell-state enum already includes Withered and Sealed for Phase 2)
- `MergeRules`: Move, Merge, Swap and Reject, exactly as in GDD §3.2 (minus order delivery)
- `GeneratorService`: tap, charges, recharge, nearest-empty placement, board-full guard before spending Sap
- `EnergyModel`: cap 100, +1 every 120 s, offline regen, over-cap via `Add`, clock-rewind safety
- `IClock`, `IRandom` (seeded XorShift), test fakes
- `GameSession` facade: `DragItem`, `TapCell`, plus the events views need
- `SaveData` v1 DTO and mapping to and from models

### 2.3 Content (ScriptableObjects, created by an Editor setup menu)

- **Nature chain**, 6 levels: Glimmerseed → Sprout → Sapling → Blossom Shrub → Young Oak → Elder Oak
- **Water chain**, 6 levels: Dewdrop → Water Flask → Moon Pitcher → Spring Basin → Crystal Fount → Moonwell
- **Seed Pouch** generator (Lv1–2): Nature drops, 12 charges, 15 min recharge (shortened to 30 s by a debug toggle)
- **Moonwell Spring** generator (Lv1): Water drops
- Starting board 7×9: two generators plus a few level-1 items

### 2.4 Presentation (graybox)

- Board rendered with world-space sprites: rounded-square cells, items drawn as a coloured shape per chain plus a level number (generated placeholder sprites, no external art)
- `DragController` on the Input System: tap vs drag threshold, lift, follow, release, snap-back on reject
- Minimal tweens: move/snap (ease-out), merge pop, spawn arc
- HUD: Sap counter, next +1 timer, generator charge and cooldown indicator
- Debug overlay (development builds only): add Sap, reset save, fast recharge toggle
- `WorldTree/Setup/Build Prototype Scene` editor menu that creates `Main.unity` with camera, board and HUD wired, so no hand-written scene YAML is needed

### 2.5 Platform

- `SystemClock`, `JsonFileSaveStore` (atomic write + backup), save on pause/quit plus debounced save

## 3. Out of scope (later phases)

Withered and Sealed cells, satchel, selling, codex (Phase 2) · generator upgrades beyond Lv2, daily gift, ads (Phase 3) · orders, currencies other than Sap, XP (Phase 4) · story, areas (Phase 5) · final art, VFX, audio, haptics (Phase 6) · mobile builds (Phase 7) · monetization (Phase 8).

## 4. Required Core tests (minimum)

| Area | Cases |
|---|---|
| Merge | Same-ID merge produces the next level in the target cell and frees the source · max-level same-ID swaps · different IDs swap · drop on empty moves · drop on self is a no-op · out-of-bounds rejected |
| Generator | Tap spends 1 Sap and spawns into the nearest empty cell (BFS order deterministic) · board full → no Sap spent, `BoardFull` result · charges deplete → recharge → available again at the exact time (fake clock) · drop table respects weights (seeded distribution test) · two Lv1 generators merge into Lv2 |
| Energy | Regen +1 per 120 s up to the cap · offline gap of 5 h regenerates to the cap only · over-cap `Add` is kept, with no regen while over the cap · clock moved backwards → no gain, no crash · `TrySpend` fails atomically when insufficient |
| Save | Round-trip `GameSession` → `SaveData` → JSON → `SaveData` → `GameSession` gives an equal state · missing or corrupt file → backup → new game |

## 5. Acceptance criteria

1. `dotnet test tools/CoreTests` passes locally and in GitHub Actions.
2. ⚠ The project opens in Unity 6 LTS with no compile errors, and packages resolve.
3. ⚠ Running *WorldTree → Setup → Build Prototype Scene* then pressing Play shows a playable board.
4. ⚠ Tapping a generator spawns items and decreases Sap. Dragging identical items merges them with a pop. Dragging onto a different item swaps. Invalid drops snap back.
5. ⚠ Sap regenerates while playing and after restarting Play mode, and board state is restored after restart.
6. ⚠ The EditMode tests pass in the Unity Test Runner.
7. ⚠ Drag stays smooth in Device Simulator (for example, iPhone and mid-range Android profiles), with no visible hitches.

Items marked ⚠ are verified by the user in the Unity Editor. Claude will provide a step-by-step validation checklist with the Phase 1 delivery.
