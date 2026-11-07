# WORLD TREE: Technical Architecture

> Status: **v1.0 implementation baseline (frozen after design review 4).** Reopen a decision only if implementation exposes a concrete contradiction or blocker (see `04-roadmap.md`).

## 1. Goals

- **Testable:** every gameplay rule runs and is tested without Unity, scenes or rendering.
- **Data-driven:** designers tune items, generators, orders and areas through ScriptableObjects without code changes.
- **Modular:** systems talk through narrow interfaces and can be developed one phase at a time.
- **Mobile-performant:** 60 fps on mid-range devices, no garbage-collector spikes during drag and merge, fast cold start.
- **Save-safe:** saves survive app kills, schema changes and asset renames.

## 2. Platform and packages

### 2.1 Editor version

**Target: Unity 6.3 LTS (`6000.3.x`), the newest patch release in Unity Hub.** Unity 6.0 LTS (`6000.0`) was released in October 2024 with a two-year standard support window, so in late 2026 Unity 6.3 is the LTS to build a new project on. The code avoids 6.3-only APIs, so a 6000.0 LTS project also works if needed. ⚠ *Confirm the current LTS in Unity Hub before creating the project.*

### 2.2 Phase 1 `Packages/manifest.json`

```json
{
  "dependencies": {
    "com.unity.2d.sprite": "1.0.0",
    "com.unity.inputsystem": "1.14.2",
    "com.unity.nuget.newtonsoft-json": "3.2.1",
    "com.unity.render-pipelines.universal": "17.3.0",
    "com.unity.test-framework": "1.6.0",
    "com.unity.ugui": "2.0.0",
    "com.unity.modules.androidjni": "1.0.0",
    "com.unity.modules.animation": "1.0.0",
    "com.unity.modules.audio": "1.0.0",
    "com.unity.modules.imgui": "1.0.0",
    "com.unity.modules.jsonserialize": "1.0.0",
    "com.unity.modules.particlesystem": "1.0.0",
    "com.unity.modules.ui": "1.0.0",
    "com.unity.modules.uielements": "1.0.0"
  }
}
```

| Entry | Why |
|---|---|
| `com.unity.2d.sprite` | Sprite Editor, SpriteAtlas |
| `com.unity.inputsystem` | Unified touch and mouse through `Pointer` actions. Required by the brief |
| `com.unity.nuget.newtonsoft-json` | Save serialization (dictionaries, versioned DTOs). Official Unity-maintained package. Also used by the Core save code (see §6.6) |
| `com.unity.render-pipelines.universal` | URP 2D Renderer: 2D lights for the corruption/restoration mood. Shader Graph comes in as a dependency (blight-dissolve effect) |
| `com.unity.test-framework` | EditMode and PlayMode tests (pulls in `com.unity.ext.nunit`) |
| `com.unity.ugui` | uGUI **and TextMeshPro** (see §2.3) |
| `com.unity.modules.*` | Built-in modules the project uses **directly**: `androidjni` (Android haptics bridge), `animation`, `audio`, `particlesystem`, `ui`, `imgui` and `uielements` (editor tooling), `jsonserialize` (`JsonUtility` in editor tools). Modules not listed are disabled, which keeps builds small. Modules that packages need are enabled automatically through their own dependencies |

**Version policy:** URP, uGUI and Test Framework are **core packages locked to the Editor version**. If the installed 6000.3 patch bundles a different version, Unity uses its own and rewrites the manifest. Input System and Newtonsoft are registry packages: the listed versions are the ones Unity's own 6.3-era templates and changelogs reference. ⚠ *After the first open, commit the `manifest.json` and `packages-lock.json` that Unity actually resolved. Those become the source of truth.*

**Optional, not in the manifest:** an IDE integration package (`com.unity.ide.rider` or `com.unity.ide.visualstudio`). Each developer adds the one for their IDE. It doesn't affect builds.

**Later phases, added only when that phase starts:**

| Package | Why | Phase |
|---|---|---|
| `com.unity.mobile.notifications` | "Sap full" and "generator ready" local notifications | 7 |
| `com.unity.addressables` | Load area art on demand to keep memory and startup small | 7 |
| `com.unity.localization` | String tables (keys are used from Phase 1) | 7 |
| `com.unity.purchasing` | IAP | 8 |
| Unity LevelPlay (`com.unity.services.levelplay`) or equivalent | Rewarded ads behind `IAdService` | 8 |

Not used on purpose: third-party DI containers, tween libraries (an in-house tweener is about 300 lines), paid Asset Store plugins, the `com.unity.feature.2d` bundle (it pulls in many unused packages), and `2d.pixel-perfect` (the art direction is painterly, not pixel art).

### 2.3 TextMeshPro in Unity 6

- Since Unity 2023.2, TextMeshPro ships **inside `com.unity.ugui` 2.0.0**. The standalone `com.unity.textmeshpro` package is deprecated and **must not** be added to the manifest. Adding it causes duplicate-type conflicts.
- Namespace: `TMPro` (unchanged). Assembly definition reference: **`Unity.TextMeshPro`** (the name kept by uGUI 2.0; Unity fixed a 6000.0 bug where it was missing from the asmdef picker in 6000.0.2f1).
- **TMP Essential Resources** (default SDF shader, settings, fallback font) must be imported once through *Window → TextMeshPro → Import TMP Essential Resources*. The imported `Assets/TextMesh Pro/` folder is committed.
- ⚠ *Confirm in the Editor: an asmdef referencing `Unity.TextMeshPro` compiles, and `TextMeshProUGUI` renders with the essential resources imported.*

## 3. Layered architecture

```
┌────────────────────────────────────────────────────────────┐
│ Game         Bootstrapper (composition root), board/item   │
│              views, drag input, HUD, tweens, save driver,  │
│              system clock (later: haptics, ads, IAP)       │
├────────────────────────────────────────────────────────────┤
│ Data         ScriptableObject definitions → Core defs      │
├────────────────────────────────────────────────────────────┤
│ Core (pure C#, noEngineReferences)                         │
│ Board · Merge · Items · Generators · Energy · Orders ·     │
│ Progression · Economy · Story · Save · GameSession         │
└────────────────────────────────────────────────────────────┘
```

### 3.1 Assemblies (`.asmdef`)

| Assembly | References | Engine | Notes |
|---|---|---|---|
| `WorldTree.Core` | Precompiled `Newtonsoft.Json.dll` only (`overrideReferences: true`) | **No** (`noEngineReferences: true`) | All rules and state, and the save store. Also compiled by `tools/CoreTests` with .NET (Newtonsoft through NuGet) |
| `WorldTree.Data` | Core | Yes | ScriptableObjects, `GameDatabase` → Core catalog |
| `WorldTree.Game` | Core, Data, `Unity.InputSystem`, `Unity.TextMeshPro` | Yes | Bootstrapper, MonoBehaviours, views, input, HUD, save driver, `SystemClock`. Platform services (haptics, notifications, ads, IAP) are added here when their phase starts, and split into their own assembly only if they grow large |
| `WorldTree.Editor` | Core, Data, Game | Editor only | Setup menus (content assets, prototype scene), data validators |
| `WorldTree.Tests` | Core, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, precompiled `nunit.framework.dll` | Editor only, `defineConstraints: UNITY_INCLUDE_TESTS` | Engine-free NUnit tests of Core. The same files are compiled by `tools/CoreTests`, so they run with and without Unity |

Five assemblies (design review 4 merged `Platform`, `App` and `Presentation` into `Game`, and dropped the separate PlayMode test assembly until there are PlayMode tests). The boundaries enforce the dependency rule at compile time. Core physically *can't* reference `UnityEngine`.

## 4. Core domain model

```csharp
// Identity: stable string IDs, never enum ordinals or asset names.
readonly struct ItemId    { string Value; }        // "item.nature.03"
readonly struct ChainId   { string Value; }        // "chain.nature"
readonly struct GridPos   { int X; int Y; }

// Immutable definitions (built from ScriptableObjects at boot)
sealed class ItemDef      { ItemId Id; ChainId Chain; int Level; ItemId? Next; int SellValue;
                            GeneratorDef Generator /*null unless generator*/; ItemFlags Flags; }
sealed class ChainDef     { ChainId Id; IReadOnlyList<ItemDef> Levels; }
sealed class GeneratorDef { int Charges; TimeSpan Recharge; int SapCost; DropTable Drops;
                            int? ConsumableTaps; }
sealed class DropTable    { IReadOnlyList<(ItemId item, int weight)> Entries; ItemId Roll(IRandom r); }
interface   ICatalog      { ItemDef GetItem(ItemId id); ChainDef GetChain(ChainId id); ... }
```

### 4.1 Models (mutable state, raise events)

| Model | Responsibility | Key API |
|---|---|---|
| `BoardModel` | Grid of `Cell { CellState State; ItemInstance Item; }` | `TryPlace`, `Move(from,to) → MoveResult`, `FindNearestEmpty(origin)`, events `ItemPlaced/Moved/Merged/Removed/CellStateChanged` |
| `MergeRules` (stateless) | Decides Move / Merge / Swap / Reject, and cleansing Withered neighbours | `Resolve(board, from, to, catalog) → MoveResult` |
| `GeneratorService` | Charges, first-tap refresh timers, tapping, spawn placement | `TryActivate(pos) → ActivationResult`, `GetState(pos)` |
| `EnergyModel` | Sap amount, regeneration from timestamps, over-cap rules | `Current`, `TrySpend(n)`, `Add(n)`, `NextTickAt`, `Refresh()` |
| `Wallet` | Glimmer, Lifebloom, Moonpetals | `TrySpend(currency, n)`, `Add`, event `Changed` |
| `ProgressionModel` | XP, level, unlock rewards | `AddXp → LevelUpResult` |
| `OrderBoard` + `OrderGenerator` | Active orders, feasibility, delivery | `CanDeliver(order, board)`, `Deliver`, `Refill` |
| `Codex` | Discovered items | `Discover(itemId) → bool isNew` |
| `AreaProgress` / `QuestLine` | Restoration tasks, story flags | `TryCompleteTask(taskId)` |
| `GameSession` | **Facade / command entry point** that coordinates models in single transactions | `DragItem(from,to)`, `TapCell(pos)`, `DeliverOrder(id)`, `Sell(pos)`, … |

Every model operation is **transactional**: it validates first and then mutates, so a failure leaves state unchanged. Example: a generator tap checks for an empty cell *before* spending Sap.

### 4.2 Cross-cutting interfaces

```csharp
interface IClock  { DateTime UtcNow { get; } }        // SystemClock (Game), FakeClock (tests)
interface IRandom { int Next(int maxExclusive); }     // seeded XorShift in Core; state saved for determinism
interface IFileSystem { /* exists, read, write (optional disk flush), move, delete: the only file access the save store uses */ }
                                                      // PhysicalFileSystem (System.IO, Core), in-memory + fault-injecting (tests)
// AtomicSaveStore (Core) writes and loads saves, see §6
interface IHaptics { void Play(HapticKind kind); }    // Light / Medium / Success / Warning
interface IAdService { bool IsRewardedReady { get; } void ShowRewarded(Action<bool> done); } // NoOp until Phase 8
```

### 4.3 Events and data flow

```
Pointer input ──► DragController ──► GameSession.DragItem(from,to)
                                           │ (validates, mutates models)
                                           ▼
                              BoardModel.ItemMerged event
                                           │
               ┌───────────────────────────┼───────────────────────────┐
               ▼                           ▼                           ▼
        BoardView animates          FeedbackService:            SaveDriver writes
        (pooled ItemViews)          VFX + SFX + haptic          at end of frame (§6.3)
```

Views are **projections** of model state. A view can always be fully rebuilt from the model (`BoardView.RebuildAll()`), which makes load, undo and debugging trivial.

## 5. Data layer (ScriptableObjects)

| Asset type | Contents |
|---|---|
| `ItemChainDefinition` | Chain ID, display name key, colour accent, ordered list of `ItemLevelEntry { id, nameKey, sprite, sellValue, flags }` |
| `GeneratorDefinition` | Item ID of the generator, per-level capacity, refresh duration, Sap cost, drop tables |
| `BoardLayoutDefinition` | Width, height, per-cell initial state and item |
| `OrderPoolDefinition` | Procedural order rules per area (chains, level bands, reward formula) |
| `AreaDefinition` | Board layout, quest line (tasks, costs, story scenes), unlocks, art references |
| `EnergyConfig`, `EconomyConfig`, `ProgressionConfig` | Global tuning numbers |
| `DialogueScene` | Lines (speaker, expression, localization key) |
| `GameDatabase` | Root asset that references everything and builds `ICatalog` at boot |

- **Validation:** `OnValidate` plus a `WorldTree/Validate Data` editor menu checks for duplicate IDs, broken `Next` links, unreachable chains, empty drop tables and orders that can't be fulfilled. CI can call the same validator in batch mode later.
- IDs are typed explicitly in the inspector, never derived from asset names.

## 6. Save system (simplified in design review 4)

Mobile operating systems can kill the app at any moment, without `OnApplicationQuit`. The save system is built so that a **process crash or kill never corrupts or loses the save**, while staying simple: one thread, synchronous writes, no queue.

### 6.1 Principles

1. **Always consistent.** Every save is a complete snapshot of the game state taken *between* commands. It never contains half a transaction.
2. **Written promptly.** Any frame in which state changed ends with one synchronous save. A kill can lose at most the commands from the frame in progress.
3. **The last good save survives a process crash.** No step of the write protocol leaves the device without a valid save if the app is killed or crashes.
4. **No power-loss claim.** Sudden power loss or an OS crash is best effort (§6.5).

### 6.2 Format

- **Payload:** JSON (Newtonsoft) of `SaveData` DTOs defined in Core: plain classes, lists instead of engine types, timestamps as UTC ticks.
- **Contents:** board cells (state, item ID, generator charges and `refreshEndsUtc`), Sap and next regen tick, RNG state, session statistics. Later phases add wallet, XP, codex, orders, quests, settings and processed purchase IDs.
- **Envelope:** `{ formatVersion, sequence, savedAtUtc, sha256, payload }`. `sequence` increases by 1 with every write and continues from the highest sequence found on disk at startup, so the loader can always tell which file is newest. `sha256` (from `System.Security.Cryptography`, no extra package) detects truncated or corrupted files. It's an integrity check, not anti-cheat.
- All time-based state uses **absolute UTC timestamps**, so a rollback to an older save never distorts timers.

### 6.3 When saves happen

- Each successful `GameSession` command (drag, tap, debug action…) increments `StateVersion`.
- `SaveDriver` (Game) checks once per frame, in `LateUpdate`: if `StateVersion` changed since the last save, it writes one save **synchronously on the main thread**, without a disk flush. Several commands in one frame produce one write.
- **Going to the background** (`OnApplicationPause(true)`, `OnApplicationFocus(false)` on Android, `OnApplicationQuit`): write the latest state **with a disk flush** (`FileStream.Flush(flushToDisk: true)`). If nothing changed since the last save but that save wasn't flushed, it's rewritten with a flush.
- **Why no flush on ordinary saves:** finished writes and renames survive the *process* dying, because the operating system keeps them. A disk flush only helps against power loss (which isn't guaranteed) and costs most of the write time on mobile storage.
- **Write failure** (for example, disk full): the previous save stays untouched, the error is logged, and the next frame tries again. A non-blocking warning appears after 3 consecutive failures.
- **Budget:** a typical save (10–30 KB) should take ≤ 2 ms without a flush. ⚠ Measured on a device in Phase 7. Only if it exceeds the budget is a background writer considered, as a new decision.
- **Purchases (Phase 8):** grant the purchase and its transaction ID as one command, save, and confirm to the store only after the save call returns successfully. If the app dies first, the store re-delivers the purchase, and the saved transaction ID prevents a double grant.

### 6.4 Atomic write protocol

0. Delete any leftover `save.tmp` (it can only come from an interrupted earlier write, which the loader already considered at startup).
1. Serialize the envelope and write it to `save.tmp` (with a disk flush only when going to the background).
2. If `save.json` exists: delete `save.bak`, then rename `save.json` → `save.bak`.
3. Rename `save.tmp` → `save.json`.

`File.Replace` is deliberately **not** used: its behaviour on IL2CPP and Android file systems isn't guaranteed. The loader below makes every intermediate state recoverable.

| Process killed after… | Files on disk | Loader picks |
|---|---|---|
| During step 1 (partial tmp) | `json` (old), `bak`, broken `tmp` | `json` (old). The broken `tmp` fails its checksum |
| Step 1 | `json` (old), `bak`, `tmp` (new) | `tmp` (new, highest sequence) |
| Deleting `bak` in step 2 | `json` (old), `tmp` (new) | `tmp` (new) |
| Renaming `json` → `bak` | `bak` (old), `tmp` (new) | `tmp` (new) |
| Step 3 | `json` (new), `bak` (old) | `json` (new) |

### 6.5 Durability scope

| Failure | Guarantee |
|---|---|
| **Process termination** (killed by the OS or user, crash, `OnApplicationQuit` never called) | **Guaranteed:** the next launch loads exactly the last completed save, or the one being written if it reached a recoverable stage. Never corrupted, never empty. Proven by the crash-recovery tests (§6.7) |
| **Sudden power loss or OS/kernel crash** | **Best effort, not guaranteed.** Expected outcome: a recent valid save, possibly older than the last completed one (up to the last background flush, or `save.bak`). Renames are directory metadata, which .NET can't flush explicitly, and behaviour depends on the device's file system. No power-loss guarantee is claimed anywhere |

⚠ Phase 7 runs a device kill test and a power-off test on at least one Android and one iOS device, and records the results as "observed on these devices" only.

### 6.6 Load and recovery

1. Read `save.json`, `save.tmp` and `save.bak` if present. For each: parse the envelope, verify `sha256`, check that `formatVersion` is known.
2. Choose the valid file with the **highest `sequence`**. The next write continues from the highest sequence among all readable files.
3. If the chosen file isn't `save.json`, write it again immediately through the protocol, so the file layout is normal again.
4. Invalid files: the most recent one is kept as `save.corrupt.json` for support (one file, overwritten), never silently deleted.
5. No files → new game. Files existed but none was valid → new game, plus a one-time notice that the save couldn't be restored.

**Placement:** the save stack lives in **`WorldTree.Core.Save`** (`SaveData`, `SaveEnvelope`, `SaveSerializer`, `SaveMapper`, `AtomicSaveStore`, `IFileSystem`, `PhysicalFileSystem`), so every recovery test runs in .NET CI. The Game assembly supplies the directory (`Application.persistentDataPath`) and the lifecycle callbacks.

### 6.7 Crash-recovery tests (Phase 1)

| Test | Expectation |
|---|---|
| **Exhaustive interruption test:** run two consecutive writes (A then B) and, for every *k*, kill the "process" after the *k*-th file operation (each delete, partial write, write, rename) using a fault-injecting in-memory file system | The next load returns exactly A or exactly B, never a corrupted or empty state, and never A once B's write completed |
| Interrupt, recover, write again (for every *k* above) | The next write succeeds and has a higher sequence than every file on disk |
| `save.json` truncated, empty, garbage, or wrong checksum | Falls back to the newest valid file and keeps the bad one as `save.corrupt.json` |
| All three files invalid | New game, the "couldn't restore" flag is set |
| Only `save.tmp` present | Loads it and normalizes the file layout |
| `save.bak` has a higher sequence than `save.json` | Loads `bak` |
| `IOException` on write | The previous save is unchanged and still loads |
| Sequence continues across restarts | The first write after loading has sequence = highest on disk + 1 |
| Full round trip `GameSession` → disk → `GameSession` | Equal state, including RNG state and timers |

### 6.8 Migrations and future extensions

- `SaveMigrator` (added with the first schema change) chains `vN → vN+1` steps on the payload JSON before deserializing. Each migration gets a fixture test with a real saved file from the previous version.
- Cloud save is a post-launch extension.

## 7. Presentation

### 7.1 UI framework decision

- **Board:** world-space `SpriteRenderer`s under an orthographic camera that fits the board to the safe-area width. Reasons: sorting and batching through SpriteAtlas, particles and 2D lights render naturally alongside items, and Shader Graph effects (blight dissolve) are simple.
- **HUD, menus, dialogue:** **uGUI + TextMeshPro**, Canvas in *Screen Space – Camera*. Reasons: mature runtime animation, masking, and both world and screen interplay (reward items fly from the board to HUD counters), plus broad team familiarity.
- **UI Toolkit** was evaluated and not chosen for runtime. Its runtime world-space support, particle interplay and shader-driven effects are still weaker for this premium, animation-heavy style. It may still be used for **editor tooling** (data validator, balance simulator).

### 7.2 Key components

| Component | Role |
|---|---|
| `BoardView` / `CellView` / `ItemView` | Renders the board. `ItemView`s come from `ObjectPool<ItemView>` |
| `DragController` | Input System `Point` + `Press` actions. Handles the tap vs drag threshold, lifts the item to a drag layer, converts screen → grid, and calls `GameSession` on release |
| `Tweener` | Allocation-free tween runner (struct tweens in a pooled array, easing functions, `Update`-driven) |
| `FeedbackService` | Maps game events to VFX (pooled `ParticleSystem`s), SFX (`AudioSource` pool, pitch per level) and haptics |
| `BoardCameraFitter` | Phase 1: fits the 7 × 9 board into the portrait safe area between the HUD and the bottom margin. The multi-tier layout from [`06-mobile-layout.md`](06-mobile-layout.md) (`BoardLayoutSolver`, reference-device tests) is deferred to Phase 6 |
| `HudView`, `OrderPanelView`, `CodexView`, `DialogueView` | UI screens bound to model events |

### 7.3 Input specifics

- Two Input System actions, `Point` (`<Pointer>/position`) and `Press` (`<Pointer>/press`), created in code by `DragController`, so no hand-written `.inputactions` asset is needed. They work identically for touch and mouse. An actions asset can replace them when rebinding or more actions are needed.
- Multi-touch is ignored during a drag (the first pointer wins). Drag is cancelled cleanly on app pause.

## 8. Performance budgets (mid-range device, for example a 2021 Android with 4 GB RAM)

| Metric | Budget |
|---|---|
| Frame time | ≤ 16.6 ms (60 fps), 30 fps floor on low-end |
| GC allocation during drag and merge | 0 B per frame |
| Draw calls (board + HUD) | ≤ 80 (one atlas per domain) |
| Texture memory | ≤ 250 MB, areas loaded and unloaded through Addressables (Phase 7) |
| Cold start to interactive board | ≤ 5 s |
| Save write | ≤ 2 ms on the main thread for a typical save without a disk flush (§6.3) |

## 9. Testing strategy

| Level | Tooling | Covers |
|---|---|---|
| **Core unit tests** | NUnit through Unity Test Framework (EditMode) **and** `tools/CoreTests` (.NET, runs in CI without Unity) | All rules: merge resolution, cleansing and revealing, generator merging, spawn placement, energy regeneration with fake clock, order feasibility, rewards, save round-trip, crash recovery (§6.7), migrations |
| **Data validation tests** | EditMode | Every `GameDatabase` asset passes the validator |
| **PlayMode smoke tests** (later, with their own assembly) | Unity Test Framework | Boot → board visible, simulated drag merges two items, save → reload equality |
| **Balance simulation** | C# tests on Core (Phase 4) | The Python scenarios ported to C# and compared with the Python reference output, then days per area |
| **Manual device QA** | Checklist per phase | Feel, layout, haptics, performance |

**CI (GitHub Actions):** `dotnet test tools/CoreTests` on every push. Unity EditMode and PlayMode tests and builds can be added through GameCI once the user provides a Unity license secret (Phase 7).

## 10. Error handling and logging

- Core throws for programmer errors (invalid IDs, out-of-range positions) and returns result types (`MoveResult.Rejected(reason)`) for player-facing invalid actions.
- Presentation catches unexpected exceptions at command boundaries, logs them and keeps the session alive.
- `Log` wrapper with categories. Verbose logging is stripped from release builds by a scripting define.

## 11. Things that require Unity Editor validation

These can't be verified in a headless environment and must be checked by the user in Unity 6:

- Package resolution of `Packages/manifest.json` versions (commit the resolved `manifest.json` and `packages-lock.json`)
- `.asmdef` references compile in the Editor: `Unity.TextMeshPro`, `Unity.InputSystem`, the precompiled `Newtonsoft.Json.dll` reference in `WorldTree.Core`, and the test assembly
- TMP Essential Resources imported and committed
- *Player Settings → Active Input Handling* set to **Input System Package (New)**
- Scenes, prefabs, sorting layers, URP 2D Renderer asset, SpriteAtlas setup, and the Input Actions asset import
- Drag feel, animation timing, particles, haptics, safe-area layout in Device Simulator ([`06-mobile-layout.md`](06-mobile-layout.md) §7) and on real devices
- Background save writes under IL2CPP, and the device kill test (§6.7)
- Mobile builds (Android and iOS) and their performance budgets
