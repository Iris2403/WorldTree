# WORLD TREE: Phase 1 Playable Prototype Specification

> Status: **v1.0 implementation baseline (design review 4, roadmap D19).**

## 1. Purpose

Prove the **core loop** on the real architecture: *spend Sap → tap generator → drag → merge → save*. The goal is to validate feel and structure and to measure seconds per Sap, not content or art. Graybox visuals are acceptable **in this phase only**.

## 2. In scope

### 2.1 Project skeleton

- `Packages/manifest.json` as in Architecture §2.2 (TextMeshPro comes through `com.unity.ugui`)
- Five `.asmdef` files: `WorldTree.Core`, `WorldTree.Data`, `WorldTree.Game`, `WorldTree.Editor`, `WorldTree.Tests`
- `tools/CoreTests` .NET project (compiles Core and its tests directly) and a GitHub Actions workflow running it

### 2.2 Core (pure C#, unit-tested)

- IDs as stable strings. `GridPos`, `ItemDef`, `ChainDef`, `GeneratorDef`, `DropTable`, `Catalog`
- `BoardModel` with Open cells. The `CellState` enum already includes Withered and Sealed; their rules land in Phase 2
- `MergeRules`: Move, Merge, Swap, Reject and No-op as in GDD §3.2 (Open cells), including generator merging per GDD §4.3
- Generators: tap with the GDD §4.1 order of checks, first-tap refresh timer with absolute timestamps and clock clamping, nearest-empty placement (Manhattan distance, ties by row then column)
- `EnergyModel`: cap 100, +1 every 120 s with the exact timer rule (GDD §5), offline regen, over-cap `Add`, clock-rewind safety
- `IClock`, `IRandom` (seeded XorShift, state saved)
- `GameSession`: `DragItem`, `TapCell`, events for the views, `StateVersion`, session statistics
- Save stack per Architecture §6: `SaveData`, `SaveEnvelope` (sequence + SHA-256), `SaveSerializer`, `SaveMapper`, `AtomicSaveStore` over `IFileSystem`, `PhysicalFileSystem`
- `PrototypeContent`: the Phase 1 content defined in code, used by tests and as the source for the Editor setup menu

### 2.3 Content

- **Nature chain**, 6 levels: Glimmerseed → Sprout → Sapling → Blossom Shrub → Young Oak → Elder Oak
- **Water chain**, 6 levels: Dewdrop → Water Flask → Moon Pitcher → Spring Basin → Crystal Fount → Moonwell
- **Seed Pouch** generator Lv1–2 (60 charges / 20 min, then 66 / 18 min), Nature drops
- **Moonwell Spring** generator Lv1 (60 charges / 20 min), Water drops
- Starting board 7 × 9: one of each generator plus a few level-1 items, and a second Seed Pouch Lv1 so generator merging can be tried
- ScriptableObject definitions (`ItemChainDefinition`, `GeneratorDefinition`, `BoardLayoutDefinition`, `EnergyConfigDefinition`, `GameDatabase`), created from `PrototypeContent` by the Editor menu and editable afterwards

### 2.4 Game (graybox)

- Board as world-space sprites: generated placeholder sprites (rounded cells, a coloured shape per chain, generators as framed squares, a level number). No external art
- `DragController` on the Input System (`<Pointer>` actions created in code): tap vs drag threshold, lift on touch, follow, release, snap back on reject
- Minimal tweens: move/snap, merge pop, spawn arc
- `BoardCameraFitter`: the board fits inside the portrait safe area between the HUD and a bottom margin
- Basic HUD (uGUI + TextMeshPro, built in code): Sap with the next +1 timer, the selected generator's charges and "Full refresh in mm:ss", short messages ("Board is full", "Not enough Sap")
- Debug overlay (Editor and development builds): +10 Sap, refresh all generators now, reset save, and **statistics** (session time, taps, merges, Sap spent, seconds per Sap) to measure the GDD §5.1 assumption
- `SaveDriver`: one synchronous save at the end of any frame with changes, a flushed save when going to the background
- *WorldTree → Setup → Build Prototype Scene* editor menu: creates the content assets and `Main.unity` with camera and bootstrapper wired, so no hand-written scene YAML is needed

## 3. Out of scope (later phases)

Withered and Sealed rules, satchel, selling, codex, generator UI states (Phase 2) · generator upgrades beyond Lv2, consumable generators, daily gift, ads (Phase 3) · orders, other currencies, XP (Phase 4) · story, areas (Phase 5) · multi-device layout, final art, VFX, audio, haptics (Phase 6) · mobile builds, device save tests (Phase 7) · monetization (Phase 8).

## 4. Required Core tests (minimum)

| Area | Cases |
|---|---|
| Merge | Same-ID merge produces the next level in the target cell and frees the source · max-level same-ID swaps · different IDs swap · drop on empty moves · drop on self is a no-op · out-of-bounds and locked cells rejected |
| Generator | Nearest empty Open cell with the documented tie-break · board full → no Sap or charge spent · no charge → no Sap spent · no Sap → no charge spent · first tap on a full generator starts the timer, later taps don't restart it · at `refreshEndsUtc` the generator is full whatever is left · refresh completes offline · clock moved back → remaining time clamped · drop table respects weights (seeded) · state follows the item when moved or swapped |
| Generator merge | Full + depleted → full Lv2 · both depleted → full Lv2 · different types swap · generator + item swap · max level swaps |
| Energy | Regen +1 per 120 s up to the cap · offline gap regenerates to the cap only · over-cap `Add` kept, no regen while over the cap · clock moved backwards → no gain, no crash · `TrySpend` fails atomically |
| Save | Every test in Architecture §6.7 |

## 5. Acceptance criteria

1. `dotnet test tools/CoreTests` passes locally and in GitHub Actions.
2. ⚠ The project opens in Unity 6 LTS with no compile errors, and packages resolve.
3. ⚠ Running *WorldTree → Setup → Build Prototype Scene*, then pressing Play, shows a playable board.
4. ⚠ Tapping a generator spawns items and decreases Sap. Dragging identical items merges them with a pop. Dragging onto a different item swaps. Invalid drops snap back.
5. ⚠ Sap regenerates while playing and after restarting Play mode. Board state is restored after a restart, including when Play mode is stopped right after a merge.
6. ⚠ The tests pass in the Unity Test Runner (EditMode).
7. ⚠ The board fits and drag stays smooth in Device Simulator on a portrait phone (for example iPhone SE and a 20:9 Android).

Items marked ⚠ are verified by the user in the Unity Editor, using the checklist delivered with Phase 1.
