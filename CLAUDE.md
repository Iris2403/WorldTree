# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

WorldTree is a planned Unity game project. The repository currently has no Unity project yet, only a placeholder `README.md`. No build, lint, or test commands exist yet.

Intended direction, from the project owner's setup notes:
- A **2D** game: sprites, sprite atlases, and pixel-perfect rendering.
- UI built with Unity UI (uGUI and/or UI Toolkit).
- Live-game features and in-app purchases.
- Development is meant to use the official Unity agent plugin (`Unity-Technologies/unity-agent-plugin`) for Unity CLI, package management, sprite, UI, and IAP workflows.

## Environment notes

- Cloud sessions for this repo have no Unity Editor installed, so you can't open, build, or play-test the project here. Changes to scenes, prefabs, and assets have to be made as text (YAML `.unity`/`.prefab`/`.asset`, plus `.meta` files) or C# scripts, and checked in the Editor on a local machine.
- When the Unity project is created, keep the standard layout at the repo root (`Assets/`, `Packages/`, `ProjectSettings/`) and add a Unity `.gitignore` (ignore `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `UserSettings/`). Commit every `.meta` file along with its asset.

Update this file with real build, test, and architecture details once the Unity project exists.
