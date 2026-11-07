# WORLD TREE

An original, mobile-first **Merge-2 fantasy puzzle game** built with Unity 6 and C#. Players restore a corrupted World Tree and its groves by merging items, running generators, fulfilling orders and uncovering the mystery behind the Witherblight.

> **Status: Phase 0 (concept and planning), awaiting design approval.** There is no Unity project in the repository yet. Phase 1 adds the project skeleton and a playable prototype.

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

## Setup (available once Phase 1 lands)

1. Clone the repository:
   ```bash
   git clone https://github.com/Iris2403/WorldTree.git
   ```
2. In **Unity Hub → Add → Add project from disk**, select the cloned folder and open it with Unity 6 LTS. The first import generates `Library/`, `.meta` files and `ProjectSettings/`. Commit the generated `.meta` and `ProjectSettings/` files.
3. One-time project settings: *Window → TextMeshPro → Import TMP Essential Resources*, and *Edit → Project Settings → Player → Active Input Handling* = **Input System Package (New)**. Commit the resulting files, plus the `Packages/packages-lock.json` Unity generates.
4. Run **WorldTree → Setup → Build Prototype Scene**, open `Assets/WorldTree/Scenes/Main.unity` and press **Play**.
5. Run tests in **Window → General → Test Runner** (EditMode). Core tests can also run without Unity:
   ```bash
   dotnet test tools/CoreTests
   ```

## Development workflow

Development is driven through GitHub and Claude Code, following the project skill in `.claude/skills/worldtree-game-developer/`. Work is delivered phase by phase. Each phase lists what has to be verified in the Unity Editor before it counts as complete.

## Intellectual property

All characters, story, names, art and audio are original to WORLD TREE or used under a documented licence (`Assets/WorldTree/Art/CREDITS.md`). The game uses genre conventions but doesn't copy content from other games.
