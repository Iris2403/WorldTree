# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

WORLD TREE is an original mobile-first Merge-2 fantasy puzzle game (Unity 6 LTS, C#). **Load the `worldtree-game-developer` skill (`.claude/skills/worldtree-game-developer/SKILL.md`) for any work here.** It holds the architecture rules, environment constraints, testing requirements and phase-gating workflow.

Design and technical sources of truth live in `docs/` (GDD, architecture, structure, roadmap, prototype spec, mobile layout). Current phase and status: `docs/04-roadmap.md`.

## Key constraints

- No Unity Editor is available in cloud sessions. Don't run Editor or slash commands, and don't hand-write `.meta`, scene, prefab or `ProjectSettings` files. Mark unverified work as "⚠ Requires Unity Editor validation".
- `WorldTree.Core` is engine-free pure C# (`noEngineReferences`). All gameplay rules live there and are tested both in Unity (EditMode) and through `dotnet test tools/CoreTests`.
- Work one phase at a time, and get user approval before starting the next.

## Commands (from Phase 1)

```bash
dotnet test tools/CoreTests                                   # all Core tests
dotnet test tools/CoreTests --filter "FullyQualifiedName~Merge"  # a subset / single test
```
