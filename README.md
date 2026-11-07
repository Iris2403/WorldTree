# WORLD TREE

An original, mobile-first **Merge-2 fantasy puzzle game** built with Unity 6 and C#. Players restore a corrupted World Tree and its groves by merging items, running generators, fulfilling orders and uncovering the mystery behind the Witherblight.

> **Status: Phase 1 (playable graybox prototype) implemented, awaiting Unity Editor validation.** The engine-free gameplay core is covered by automated tests that run without Unity. The Unity layer has not yet been opened in the Editor; see the checklist in [`docs/04-roadmap.md`](docs/04-roadmap.md).

## Documentation

| Document | Contents |
|---|---|
| [Game Design Document](docs/01-game-design-document.md) | Pillars, core loop, merge/generator/energy/order rules, story, economy, UX direction |
| [Technical Architecture](docs/02-technical-architecture.md) | Layers, assemblies, core model, save system, UI decision, performance budgets, testing |
| [Project Structure](docs/03-project-structure.md) | Folder layout, naming conventions, asset specs, version control |
| [Roadmap](docs/04-roadmap.md) | Phases 1–8 with deliverables, acceptance criteria and Editor-validation checklists |
| [Prototype Spec](docs/05-prototype-spec.md) | Exact scope of the Phase 1 playable prototype |
| [Mobile Layout](docs/06-mobile-layout.md) | Portrait regions, board cell sizing, reference-device validation |

## Requirements

- **Unity 6.3 LTS** (6000.3.x, newest patch in Unity Hub), with the **Android** and/or **iOS** build support modules
- Git (Git LFS recommended once real art is added)
- Optional: .NET SDK 8+ to run the engine-independent Core tests locally

## Run the Core tests (no Unity needed)

```bash
dotnet test tools/CoreTests
```

This compiles `Assets/WorldTree/Scripts/Core` and `Assets/WorldTree/Tests` directly with .NET 8. CI runs the same command on every push (`.github/workflows/core-tests.yml`).

## Open the prototype in Unity

1. Clone the repository:
   ```bash
   git clone https://github.com/Iris2403/WorldTree.git
   ```
2. **Unity Hub → Add → Add project from disk**, pick the cloned folder, and open it with **Unity 6.3 LTS**. Make sure you're on the branch with the project (currently `claude/zealous-cray-vkfj0o`, until it's merged into `main`). The repository only contains `ProjectSettings/ProjectVersion.txt`; Unity creates the rest of `ProjectSettings/`, `Library/` and the `.meta` files on first open. If Hub says the version 6000.3.0f1 isn't installed, choose your installed Unity 6.3 LTS version instead.
   - If Hub won't add the folder, start the editor directly with `-projectPath`:
     ```bash
     "<path to Unity 6000.3.x editor>" -projectPath "<path to WorldTree>"
     ```
3. When Unity asks to enable the new Input System backends and restart, choose **Yes**. Then check *Edit → Project Settings → Player → Active Input Handling* = **Input System Package (New)**.
4. *Window → TextMeshPro → Import TMP Essential Resources*.
5. **WorldTree → Setup → Build Prototype Scene**. This creates the content assets under `Assets/WorldTree/Data/` and `Assets/WorldTree/Scenes/Main.unity`, then opens the scene.
6. Press **Play**. Click or touch a framed generator (G1) to grow items, and drag matching items together. The **Debug** button (bottom right) shows seconds-per-Sap statistics and debug actions.
7. Run the tests in *Window → General → Test Runner → EditMode*.
8. Commit what Unity generated: all `.meta` files, `ProjectSettings/`, `Packages/packages-lock.json`, `Assets/TextMesh Pro/`, and the new assets under `Assets/WorldTree/Data/` and `Assets/WorldTree/Scenes/`.

Save files live in `Application.persistentDataPath/save/`. *WorldTree → Debug → Delete Save Files* removes them.

## Development workflow

Development is driven through GitHub and Claude Code, following the project skill in `.claude/skills/worldtree-game-developer/`. Work is delivered phase by phase. Each phase lists what has to be verified in the Unity Editor before it counts as complete.

## Intellectual property

All characters, story, names, art and audio are original to WORLD TREE or used under a documented licence (`Assets/WorldTree/Art/CREDITS.md`). The game uses genre conventions but doesn't copy content from other games.
