# AGENTS.md — Virtual Ultrasound Simulator Project Guide

## Overview
**Virtual Ultrasound Simulator** is an interactive 3D desktop application built with Unity and C#. It simulates ultrasound probe manipulation over 3D anatomy and generates corresponding 2D ultrasound cross-sections in real time.

## Core Rules & Workflow Directives
1. **Geometric Correctness First**: Prove geometry before adding realism or GPU optimizations.
2. **Explicit 4-Tier Coordinate Spaces**:
   $$\text{Slice UV } (u, v) \xrightarrow{T_{\text{Probe}}} \text{Probe Space } (X_P, Y_P, Z_P) \xrightarrow{T_{\text{World} \leftarrow \text{Probe}}} \text{World Space } (X_W, Y_W, Z_W) \xrightarrow{T_{\text{Volume} \leftarrow \text{World}}} \text{Volume Space}$$
3. **Decoupled Architecture**: Maintain strict separation between `ProbeController` (kinematics/input), `ProbeGeometry` (field of view/aperture), `SyntheticAnatomyVolume` (3D anatomy), `ProceduralVolumeSampler` (sampling interface), `SliceRenderer` (texture generation), and `UIController` (split-screen layout).
4. **Devlog Protocol**:
   - `docs/DECISIONS.md`: Record consequential architectural decisions as append-only table rows with the specific **why**, alternatives rejected, and proof.
   - `docs/PROGRESS.md`: Prepend dated entry with status marker (`✅ DONE` / `🟡 PARTIAL` / `⛔ NOT STARTED`), scope note, real proof, files touched, and cross-references.
   - `docs/tickets/`: Track defects, gaps, or deferred tasks (one markdown file per ticket).
5. **Git & Deployments**:
   - Never commit or push code automatically without explicit user instruction.
   - Never deploy automatically.
   - Run tests before pre-commit close-out.

## Commands & Verification

### Build & Test via CLI
```powershell
# Build standalone test and core assemblies
dotnet build tests/VirtualUltrasound.Tests/VirtualUltrasound.Tests.csproj

# Run automated unit test suite
dotnet test tests/VirtualUltrasound.Tests/VirtualUltrasound.Tests.csproj
```

### Run in Unity
1. Open project in Unity (2022.3 LTS or Unity 6 LTS).
2. Hit **Play** (the `SceneBootstrapper` automatically sets up the 3D scene and split-screen UI).

## Directory Structure
- `Assets/Scripts/Core/`: Mathematical coordinate transforms, interfaces, zero-allocation slice buffer.
- `Assets/Scripts/Volume/`: Synthetic anatomy primitives, procedural samplers, and 3D visualizers.
- `Assets/Scripts/Probe/`: Probe kinematics controller, geometry definitions, and 3D gizmos/mesh.
- `Assets/Scripts/Rendering/`: Slice rasterizer, texture update coordinator, and UI ultrasound display.
- `Assets/Scripts/Camera/`: Smooth 3D orbit/pan/zoom camera controller.
- `Assets/Scripts/UI/`: Telemetry overlay and split-screen HUD.
- `Assets/Scripts/Bootstrap/`: Self-assembling scene runner.
- `Assets/Tests/Editor/`: Unity EditMode test suite.
- `tests/VirtualUltrasound.Tests/`: Standalone .NET test suite.
- `docs/`: Project memory (`DECISIONS.md`, `PROGRESS.md`, `tickets/`).
