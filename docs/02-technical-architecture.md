# WORLD TREE: Technical Architecture

> Status: **Draft v0.2 (design review 1 applied), awaiting approval.**

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
│ App          Bootstrapper (composition root), scene flow   │
├──────────────────────────────┬─────────────────────────────┤
│ Presentation                 │ Platform                    │
│ Board/Item views, drag input,│ File save store, haptics,   │
│ HUD/UI, tweens, VFX, audio,  │ notifications, ads, IAP,    │
│ pooling                      │ device/time services        │
├──────────────────────────────┴─────────────────────────────┤
│ Data         ScriptableObject definitions → Core defs      │
├────────────────────────────────────────────────────────────┤
│ Core (pure C#, noEngineReferences)                         │
│ Board · Merge · Items · Generators · Energy · Orders ·     │
│ Progression · Economy · Story · Save DTOs · GameSession    │
└────────────────────────────────────────────────────────────┘
```

### 3.1 Assemblies (`.asmdef`)

| Assembly | References | Engine | Notes |
|---|---|---|---|
| `WorldTree.Core` | Precompiled `Newtonsoft.Json.dll` only (`overrideReferences: true`) | **No** (`noEngineReferences: true`) | All rules and state, the save store and the layout solver. Also compiled by `tools/CoreTests` with .NET (Newtonsoft through NuGet) |
| `WorldTree.Data` | Core | Yes | ScriptableObjects, validation (`OnValidate`), `GameDatabase` → `CoreCatalog` |
| `WorldTree.Platform` | Core | Yes | `SystemClock`, save directory path, `IHaptics`, `IAdService` implementations |
| `WorldTree.Presentation` | Core, Data, `Unity.InputSystem`, `Unity.TextMeshPro`, `Unity.RenderPipelines.Universal.Runtime` (only if code touches `Light2D`) | Yes | MonoBehaviours, views, UI |
| `WorldTree.App` | All of the above | Yes | `Bootstrapper`, scene loading |
| `WorldTree.Editor` | Data, Core | Editor only | Data validators, scene/prefab builder menus, balance simulator window |
| `WorldTree.Tests.EditMode` | Core, Data, Platform, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, precompiled `nunit.framework.dll` | Editor only, `defineConstraints: UNITY_INCLUDE_TESTS` | NUnit |
| `WorldTree.Tests.PlayMode` | All, `UnityEngine.TestRunner`, precompiled `nunit.framework.dll` | Test, `defineConstraints: UNITY_INCLUDE_TESTS` | Input, scene smoke tests |

Assembly boundaries enforce the dependency rule at compile time. Core physically *can't* reference `UnityEngine`.

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
| `GeneratorService` | Charges, recharge, tapping, spawn placement | `TryActivate(pos) → ActivationResult`, `GetState(pos)` |
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
interface IClock  { DateTime UtcNow { get; } }        // SystemClock (Platform), FakeClock (tests)
interface IRandom { int Next(int maxExclusive); }     // seeded XorShift in Core; state saved for determinism
interface IFileSystem { /* read, write+flush, rename, delete, exists: the only file access the save store uses */ }
                                                      // PhysicalFileSystem (System.IO, Core), FaultInjectingFileSystem (tests)
interface ISaveStore { LoadResult Load(); void Write(SaveEnvelope e); } // AtomicSaveStore (Core), see §6
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
        BoardView animates          FeedbackService:            SaveScheduler
        (pooled ItemViews)          VFX + SFX + haptic          commits (§6.3)
```

Views are **projections** of model state. A view can always be fully rebuilt from the model (`BoardView.RebuildAll()`), which makes load, undo and debugging trivial.

## 5. Data layer (ScriptableObjects)

| Asset type | Contents |
|---|---|
| `ItemChainDefinition` | Chain ID, display name key, colour accent, ordered list of `ItemLevelEntry { id, nameKey, sprite, sellValue, flags }` |
| `GeneratorDefinition` | Item ID of the generator, per-level charges, recharge, Sap cost, drop tables |
| `BoardLayoutDefinition` | Width, height, per-cell initial state and item |
| `OrderPoolDefinition` | Procedural order rules per area (chains, level bands, reward formula) |
| `AreaDefinition` | Board layout, quest line (tasks, costs, story scenes), unlocks, art references |
| `EnergyConfig`, `EconomyConfig`, `ProgressionConfig` | Global tuning numbers |
| `DialogueScene` | Lines (speaker, expression, localization key) |
| `GameDatabase` | Root asset that references everything and builds `ICatalog` at boot |

- **Validation:** `OnValidate` plus a `WorldTree/Validate Data` editor menu checks for duplicate IDs, broken `Next` links, unreachable chains, empty drop tables and orders that can't be fulfilled. CI can call the same validator in batch mode later.
- IDs are typed explicitly in the inspector, never derived from asset names.

## 6. Save system (revised in design review 1)

Mobile operating systems can kill the app at any moment, without `OnApplicationQuit`: low memory, a swipe from the app switcher, a crash, a battery pull. The v0.1 plan (a 2-second debounce) could lose up to 2 seconds of play, including rewards. It is replaced by the design below.

### 6.1 Principles

1. **Always consistent.** Every save is a complete snapshot of the whole game state taken *between* commands. A save never contains half a transaction (for example, Sap spent but no item spawned).
2. **Durable as soon as possible.** Every state-changing command is written right away, with no fixed delay. A kill can lose at most the commands made during the one write that was in progress (typically under 50 ms of play).
3. **Value is never lost or duplicated.** Real-money purchases are confirmed to the store only after the save that contains them is on disk (§6.4).
4. **The last good save is never destroyed.** No step of the write protocol leaves the device without a valid save.

### 6.2 Format

- **Payload:** JSON (Newtonsoft) of `SaveData` DTOs defined in Core: plain classes, lists instead of engine types.
- **Contents:** board cells, generator states (charges, `rechargeEndsUtc`), Sap and last regeneration UTC, wallet, XP and level, codex, orders, area and quest progress, story flags, RNG state, settings, daily gift state, and processed purchase transaction IDs (Phase 8).
- **Envelope:** `{ schemaVersion, sequence, savedAtUtc, sha256, payload }`. `sequence` increases by 1 on every write, so the loader can always tell which file is newest. `sha256` (from `System.Security.Cryptography`, no extra package) detects truncated or corrupted files. It's an integrity check, not anti-cheat.
- All time-based state uses **absolute UTC timestamps**, so a rollback to an older save never distorts timers.

### 6.3 When saves happen: commit-on-command

- Each successful `GameSession` command (drag, tap, deliver, sell, claim…) increments a `StateVersion`.
- `SaveScheduler`:
  - If no write is in progress: take a **snapshot** on the main thread (a DTO copy of the state, target < 1 ms), then serialize, hash and write it on a **background thread**.
  - If a write is in progress: mark *pending*. When the write finishes, snapshot the **latest** state and write once more. Many rapid merges therefore produce at most one follow-up write, not one per merge.
  - Nothing is written when `StateVersion` hasn't changed.
- **Lifecycle flush:** on `OnApplicationPause(true)`, `OnApplicationFocus(false)` (Android) and `OnApplicationQuit`, wait for any in-progress write, then write the latest state **synchronously**. iOS and Android give apps several seconds when backgrounding, far more than a save takes.
- **Write failure** (for example, disk full): the previous save stays untouched. The scheduler retries with backoff (1 s, 2 s, 4 s … max 30 s) and shows a non-blocking warning after 3 failures.
- The background thread touches only the immutable snapshot, never live models, so no locking is needed. `Task.Run` works under IL2CPP on iOS and Android.

### 6.4 Critical transactions

Rewards are always committed through the normal path above. These commands are additionally marked **critical**:

| Transaction | Extra guarantee |
|---|---|
| IAP grant (Phase 8) | The grant and its transaction ID are saved, and the store purchase is confirmed (`ConfirmPendingPurchase`) only **after** that write completes. If the app dies first, the store re-delivers the purchase on next launch, and the saved transaction ID prevents a double grant |
| Rewarded-ad grant, Moonpetal spend | The UI shows the reward immediately, but the scheduler moves the write ahead of any lifecycle wait. A failed write raises the warning at once instead of after 3 retries |
| Order delivery, restoration task, level-up, daily gift | Same as above |

### 6.5 Atomic write protocol

1. Serialize the envelope, write it to `save.tmp`, `FileStream.Flush(flushToDisk: true)`, close.
2. If `save.json` exists: delete `save.bak`, then rename `save.json` → `save.bak`.
3. Rename `save.tmp` → `save.json`.

`File.Replace` is deliberately **not** used: its behaviour on IL2CPP and Android file systems isn't guaranteed. The loader below makes every intermediate state recoverable.

| Killed after… | Files on disk | Loader picks |
|---|---|---|
| During step 1 (partial tmp) | `json` (old), `bak`, broken `tmp` | `json` (old). The broken `tmp` fails its checksum |
| Step 1 | `json` (old), `bak`, `tmp` (new) | `tmp` (new, highest sequence) |
| Deleting `bak` in step 2 | `json` (old), `tmp` (new) | `tmp` (new) |
| Renaming `json` → `bak` | `bak` (old), `tmp` (new) | `tmp` (new) |
| Step 3 | `json` (new), `bak` (old) | `json` (new) |

### 6.6 Load and recovery

1. Read `save.json`, `save.tmp` and `save.bak` if present. For each: parse the envelope, verify `sha256`, check that `schemaVersion` is known or can be migrated.
2. Choose the valid file with the **highest `sequence`**. If it isn't `save.json`, immediately rewrite it through the protocol so the file layout is normal again.
3. Invalid files are renamed `save.corrupt-<utc>.json` (the newest 3 are kept) for support, never silently deleted.
4. No files → new game. Files existed but none was valid → new game, plus a one-time notice that the save couldn't be restored (the quarantined copies allow manual recovery).

**Placement (changed from v0.1):** the whole save stack lives in **`WorldTree.Core.Save`**, so that every interruption test runs in .NET CI: `SaveData` DTOs, `SaveEnvelope`, `SaveSerializer`, `SaveMigrator`, `AtomicSaveStore` over `IFileSystem`, `SaveScheduler` (background work behind `IBackgroundWorker`, so tests run synchronously), and `PhysicalFileSystem` (plain `System.IO`, engine-free). Platform supplies only the directory (`Application.persistentDataPath`), and App forwards the lifecycle callbacks.

### 6.7 Interruption and recovery tests (Phase 1)

| Test | Expectation |
|---|---|
| `FaultInjectingFileSystem` kills the write at **each** step in §6.5 (including a partial `tmp`) | The next load returns exactly the old state or exactly the new state, never a corrupted or empty one |
| `save.json` truncated, zero bytes, garbage, or wrong checksum | Falls back to the newest valid file and quarantines the bad one |
| All three files invalid | New game, notice flag set, files quarantined |
| Only `save.tmp` present | Loads it and normalizes the file layout |
| `save.bak` has a higher sequence than `save.json` | Loads `bak` |
| `IOException` on write (disk full) | The old save is unchanged, a retry is scheduled, the warning appears after 3 failures |
| Scheduler: N commands during an in-progress write | Exactly one follow-up write, containing the latest state |
| Scheduler: lifecycle flush during an in-progress write | Waits, then writes the latest state |
| Scheduler: no state change | No write |
| Full round-trip `GameSession` → disk → `GameSession` | Equal state, including RNG state and timers |
| Duplicate purchase transaction ID (Phase 8) | Granted once |
| ⚠ Device kill test (Phase 7, manual): force-stop the app 20 times during rapid merging | Every relaunch loads a consistent state |

### 6.8 Migrations and future extensions

- `SaveMigrator` chains `vN → vN+1` steps on the payload JSON before deserializing. Each migration has a fixture test with a real saved file from the previous version.
- Cloud save is a post-launch extension behind `ISaveStore`.

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
| `LayoutController` | Reads `Screen.safeArea` and DPI, runs `BoardLayoutSolver` (Core, pure math, unit-tested), positions the uGUI regions, and fits the board camera into the board rect. Spec: [`06-mobile-layout.md`](06-mobile-layout.md) |
| `HudView`, `OrderPanelView`, `CodexView`, `DialogueView` | UI screens bound to model events |

### 7.3 Input specifics

- One `WorldTreeInput.inputactions` asset with a `Gameplay` map: `Point` (`<Pointer>/position`) and `Press` (`<Pointer>/press`). This works identically for touch and mouse.
- Multi-touch is ignored during a drag (the first pointer wins). Drag is cancelled cleanly on app pause.

## 8. Performance budgets (mid-range device, for example a 2021 Android with 4 GB RAM)

| Metric | Budget |
|---|---|
| Frame time | ≤ 16.6 ms (60 fps), 30 fps floor on low-end |
| GC allocation during drag and merge | 0 B per frame |
| Draw calls (board + HUD) | ≤ 80 (one atlas per domain) |
| Texture memory | ≤ 250 MB, areas loaded and unloaded through Addressables (Phase 7) |
| Cold start to interactive board | ≤ 5 s |
| Save write | ≤ 10 ms on the main thread for a typical save (background thread if exceeded) |

## 9. Testing strategy

| Level | Tooling | Covers |
|---|---|---|
| **Core unit tests** | NUnit through Unity Test Framework (EditMode) **and** `tools/CoreTests` (.NET, runs in CI without Unity) | All rules: merge resolution, cleansing and revealing, generator merging, spawn placement, energy regeneration with fake clock, order feasibility, rewards, save round-trip, interruption and recovery (§6.7), migrations, layout solver for every reference device |
| **Data validation tests** | EditMode | Every `GameDatabase` asset passes the validator |
| **PlayMode smoke tests** | Unity Test Framework | Boot → board visible, simulated drag merges two items, save → reload equality |
| **Balance simulation** | Editor window + Core | Simulated player sessions estimate days per area (Phase 4) |
| **Manual device QA** | Checklist per phase | Feel, layout, haptics, performance |

**CI (GitHub Actions):** `dotnet test tools/CoreTests` on every push. Unity EditMode and PlayMode tests and builds can be added through GameCI once the user provides a Unity license secret (Phase 7).

## 10. Error handling and logging

- Core throws for programmer errors (invalid IDs, out-of-range positions) and returns result types (`MoveResult.Rejected(reason)`) for player-facing invalid actions.
- Presentation catches unexpected exceptions at command boundaries, logs them and keeps the session alive.
- `Log` wrapper with categories. Verbose logging is stripped from release builds by a scripting define.

## 11. Things that require Unity Editor validation

These can't be verified in a headless environment and must be checked by the user in Unity 6:

- Package resolution of `Packages/manifest.json` versions (commit the resolved `manifest.json` and `packages-lock.json`)
- `.asmdef` references compile in the Editor: `Unity.TextMeshPro`, `Unity.InputSystem`, the precompiled `Newtonsoft.Json.dll` reference in `WorldTree.Core`, and the test assemblies
- TMP Essential Resources imported and committed
- *Player Settings → Active Input Handling* set to **Input System Package (New)**
- Scenes, prefabs, sorting layers, URP 2D Renderer asset, SpriteAtlas setup, and the Input Actions asset import
- Drag feel, animation timing, particles, haptics, safe-area layout in Device Simulator ([`06-mobile-layout.md`](06-mobile-layout.md) §7) and on real devices
- Background save writes under IL2CPP, and the device kill test (§6.7)
- Mobile builds (Android and iOS) and their performance budgets
