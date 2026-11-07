# WORLD TREE: Technical Architecture

> Status: **Draft v0.1, awaiting approval.**

## 1. Goals

- **Testable:** every gameplay rule runs and is tested without Unity, scenes or rendering.
- **Data-driven:** designers tune items, generators, orders and areas through ScriptableObjects without code changes.
- **Modular:** systems talk through narrow interfaces and can be developed one phase at a time.
- **Mobile-performant:** 60 fps on mid-range devices, no garbage-collector spikes during drag and merge, fast cold start.
- **Save-safe:** saves survive app kills, schema changes and asset renames.

## 2. Platform and packages

| Package | Why | Phase |
|---|---|---|
| Unity **6 LTS** (6000.x, newest LTS available in Unity Hub) | Long-term support, URP 2D maturity | 1 |
| `com.unity.render-pipelines.universal` (2D Renderer) | 2D lights for the corruption/restoration mood, Shader Graph for the blight-dissolve effect | 1 |
| `com.unity.inputsystem` | Unified touch and mouse through `Pointer` actions. Required by the brief | 1 |
| `com.unity.ugui` (includes TextMeshPro in Unity 6) | HUD and menus (see §7) | 1 |
| `com.unity.2d.sprite` | Sprite Editor, SpriteAtlas | 1 |
| `com.unity.test-framework` | EditMode and PlayMode tests | 1 |
| `com.unity.nuget.newtonsoft-json` | Save serialization (dictionaries, polymorphism, versioning). Official Unity package | 1 |
| `com.unity.mobile.notifications` | "Sap full" and "generator ready" local notifications | 7 |
| `com.unity.addressables` | Load area art on demand to keep memory and startup small | 7 |
| `com.unity.localization` | String tables (keys are used from Phase 1) | 7 |
| `com.unity.purchasing` | IAP | 8 |
| Unity LevelPlay (`com.unity.services.levelplay`) or equivalent | Rewarded ads behind `IAdService` | 8 |

Not used on purpose: third-party DI containers, tween libraries (an in-house tweener is about 300 lines), and paid Asset Store plugins. `2d.pixel-perfect` is **not** used, because the art direction is painterly, not pixel art.

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
| `WorldTree.Core` | — | **No** (`noEngineReferences: true`) | All rules and state. Also compiled by `tools/CoreTests` with .NET |
| `WorldTree.Data` | Core | Yes | ScriptableObjects, validation (`OnValidate`), `GameDatabase` → `CoreCatalog` |
| `WorldTree.Platform` | Core | Yes | `ISaveStore`, `IHaptics`, `IAdService`, `IClock` implementations |
| `WorldTree.Presentation` | Core, Data, Unity.InputSystem, Unity.TextMeshPro | Yes | MonoBehaviours, views, UI |
| `WorldTree.App` | All of the above | Yes | `Bootstrapper`, scene loading |
| `WorldTree.Editor` | Data, Core | Editor only | Data validators, scene/prefab builder menus, balance simulator window |
| `WorldTree.Tests.EditMode` | Core, Data, Platform | Editor only | NUnit |
| `WorldTree.Tests.PlayMode` | All | Test | Input, scene smoke tests |

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
interface ISaveStore { bool TryLoad(out SaveData d); void Save(SaveData d); }
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
        (pooled ItemViews)          VFX + SFX + haptic          marks dirty
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

## 6. Save system

- **Format:** JSON (Newtonsoft) of `SaveData` DTOs defined in Core: plain classes, lists instead of engine types, with `int Version`.
- **Contents:** board cells, generator states (charges, recharge-end UTC), Sap and last regen UTC, wallet, XP and level, codex, orders, area and quest progress, story flags, RNG state, settings, daily gift state.
- **Storage:** `Application.persistentDataPath/save.json`. Writes are **atomic**: write `save.tmp` → flush → replace `save.json`, keeping `save.bak` as the previous good copy. On load: try main → backup → new game, and never crash.
- **When to save:** debounced 2 s after any state change, plus forced on `OnApplicationPause(true)` and `OnApplicationQuit`.
- **Migrations:** `SaveMigrator` chains `vN → vN+1` steps. Each migration has a fixture test.
- **Integrity:** a checksum field detects corruption (not anti-cheat). Cloud save is a post-launch extension behind `ISaveStore`.

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
| `SafeAreaFitter`, `CameraFitter` | Notch- and aspect-safe layout, from 16:9 up to 21:9 phones and 4:3 tablets |
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
| **Core unit tests** | NUnit through Unity Test Framework (EditMode) **and** `tools/CoreTests` (.NET, runs in CI without Unity) | All rules: merge resolution, cleansing, spawn placement, energy regeneration with fake clock, order feasibility, rewards, save round-trip and migrations |
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

- Package resolution of `Packages/manifest.json` versions
- `.asmdef` references (TextMeshPro and Input System assembly names) compile in the Editor
- Scenes, prefabs, sorting layers, URP 2D Renderer asset, SpriteAtlas setup, and the Input Actions asset import
- Drag feel, animation timing, particles, haptics, safe-area layout on real devices
- Mobile builds (Android and iOS) and their performance budgets
