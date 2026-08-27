# Project Progress Log

**2026-08-27 — Phase 2: Decoupled Polar Acquisition & Scan-Conversion Architecture (offline, code + tests). ✅ DONE**
Refactored slice generation pipeline into two decoupled stages: Stage 1 Polar Acoustic Acquisition (`ScanLines` × `SamplesPerScanLine` volume queries stored in `PolarBuffer`) and Stage 2 Cartesian Scan Conversion (`SliceWidth` × `SliceHeight` display rasterization with Bilinear/Nearest interpolation and acoustic sector masking). Added dynamic Inspector dimension update detection during Play mode via `OnValidate()` / `OnGeometryChanged`. Expanded automated test suite with 14 passing deterministic tests.
- **Proof:** `dotnet test tests/VirtualUltrasound.Tests/VirtualUltrasound.Tests.csproj` -> 14 passed, 0 failed (Duration: 34ms); CPU benchmark recorded (32x32: 3.1ms, 128x128: 4.2ms, 256x512: 11.6ms).
- **Files Touched:** `Assets/Scripts/Core/PolarBuffer.cs`, `Assets/Scripts/Core/VolumeTypes.cs`, `Assets/Scripts/Core/ISliceGenerator.cs`, `Assets/Scripts/Probe/ProbeGeometry.cs`, `Assets/Scripts/Rendering/CPUSliceGenerator.cs`, `Assets/Scripts/Rendering/SliceRenderer.cs`, `Assets/Scripts/UI/UIController.cs`, `Assets/Tests/Editor/SliceGenerationTests.cs`, `tests/VirtualUltrasound.Tests/StandaloneTests.cs`, `tests/VirtualUltrasound.Tests/UnityEngineShim.cs`, `tests/VirtualUltrasound.Tests/VirtualUltrasound.Tests.csproj`, `docs/DECISIONS.md`, `docs/PROGRESS.md`, `README.md`.
- **Cross-references:** Implements Phase 2 Decoupled Acquisition Architecture; Decision #5.
Implemented authentic convex ultrasound probe geometry with a fan/sector field of view. Added scanline polar-to-probe coordinate transforms, analytical polar scan conversion with acoustic black masking outside the sector fan, 3D procedural curved sector fan mesh with line perimeter tracing in `ProbeVisualizer`, interactive runtime probe optics tuning (`T` toggle probe, `+/-` depth, `[ / ]` FOV angle), and expanded unit test suite with 10 deterministic tests covering polar round-trip, sector boundaries, acoustic masking, and rigid body transformations.
- **Proof:** `dotnet test tests/VirtualUltrasound.Tests/VirtualUltrasound.Tests.csproj` -> 10 passed, 0 failed (Duration: 23ms); 0 per-frame managed heap allocations in slice loop.
- **Files Touched:** `Assets/Scripts/Core/CoordinateTransform.cs`, `Assets/Scripts/Core/ISliceGenerator.cs`, `Assets/Scripts/Probe/ProbeGeometry.cs`, `Assets/Scripts/Probe/ProbeController.cs`, `Assets/Scripts/Probe/ProbeVisualizer.cs`, `Assets/Scripts/Rendering/CPUSliceGenerator.cs`, `Assets/Scripts/Rendering/SliceRenderer.cs`, `Assets/Scripts/UI/UIController.cs`, `Assets/Scripts/Bootstrap/SceneBootstrapper.cs`, `Assets/Tests/Editor/CoordinateTransformTests.cs`, `Assets/Tests/Editor/SliceGenerationTests.cs`, `tests/VirtualUltrasound.Tests/StandaloneTests.cs`, `tests/VirtualUltrasound.Tests/UnityEngineShim.cs`, `docs/DECISIONS.md`, `docs/PROGRESS.md`, `README.md`.
- **Cross-references:** Implements Phase 2 Roadmap; Decision #4.

**2026-08-26 — Split-screen UI layout polish, camera framing, and transparent materials (offline, code + tests). ✅ DONE**
Enhanced dual-camera background clearer for split-screen layout, resolved transparent 3D materials with white texture backing in `ProbeVisualizer` and `AnatomyVisualizer`, implemented automatic telemetry text binding in `UIController`, added auto-binding in `UltrasoundDisplay`, and optimized default camera framing distance.
- **Proof:** `dotnet test tests/VirtualUltrasound.Tests/VirtualUltrasound.Tests.csproj` -> 7 passed, 0 failed (Duration: 12ms); live 2D ultrasound cross-sections confirmed working in Unity Game view.
- **Files Touched:** `Assets/Scripts/Bootstrap/SceneBootstrapper.cs`, `Assets/Scripts/Camera/SceneCameraController.cs`, `Assets/Scripts/Editor/SceneSetupMenu.cs`, `Assets/Scripts/Probe/ProbeVisualizer.cs`, `Assets/Scripts/Rendering/UltrasoundDisplay.cs`, `Assets/Scripts/UI/UIController.cs`, `Assets/Scripts/Volume/AnatomyVisualizer.cs`, `docs/PROGRESS.md`.

**2026-08-26 — Unity Editor integration and scene bootstrapper enhancements (offline, code + tests). ✅ DONE**
Added 1-click Editor scene setup menu (`Virtual Ultrasound > Setup Simulator Scene in Editor`), runtime auto-initialization hook (`RuntimeInitializeOnLoadMethod`), resolved physics module references and namespace imports in visualizers, and included Unity metadata and project settings.
- **Proof:** `dotnet test tests/VirtualUltrasound.Tests/VirtualUltrasound.Tests.csproj` -> 7 passed, 0 failed (Duration: 15ms); clean compilation in Unity Editor.
- **Files Touched:** `Assets/Scripts/Bootstrap/SceneBootstrapper.cs`, `Assets/Scripts/Editor/SceneSetupMenu.cs`, `Assets/Scripts/Probe/ProbeVisualizer.cs`, `Assets/Scripts/Volume/AnatomyVisualizer.cs`, `Packages/manifest.json`, `ProjectSettings/*`, `.gitignore`, `docs/PROGRESS.md`.

**2026-08-26 — Virtual Ultrasound Simulator Milestone 1 shipped (offline, code + tests + docs). ✅ DONE**
Implemented the foundational architecture and first milestone for the real-time Virtual Ultrasound Simulator in Unity / C#. Established a modular component architecture separating probe kinematics, geometry, volume representation, slice sampling, 3D rendering, and split-screen UI. Created procedural synthetic anatomy (ellipsoid body, spherical organ, anechoic cyst, vessel cylinder), rigid probe controller with 6-DOF controls and preset views, zero-allocation CPU slice generator, 3D orbit camera, split-screen UI HUD with live ultrasound display and telemetry, and self-assembling `SceneBootstrapper`. Verified deterministic geometric correctness through a 7-test standalone NUnit suite with 100% pass rate.
- **Proof:** `dotnet test tests/VirtualUltrasound.Tests/VirtualUltrasound.Tests.csproj` -> 7 passed, 0 failed, 0 skipped (Duration: 21ms).
- **Files Touched:**
  - `Assets/Scripts/Core/` (`VolumeTypes.cs`, `IVolumeData.cs`, `IVolumeSampler.cs`, `ISliceGenerator.cs`, `CoordinateTransform.cs`, `SliceBuffer.cs`)
  - `Assets/Scripts/Volume/` (`PrimitiveShapes.cs`, `SyntheticAnatomyVolume.cs`, `ProceduralVolumeSampler.cs`, `AnatomyVisualizer.cs`)
  - `Assets/Scripts/Probe/` (`ProbeGeometry.cs`, `ProbeController.cs`, `ProbeVisualizer.cs`)
  - `Assets/Scripts/Rendering/` (`CPUSliceGenerator.cs`, `SliceRenderer.cs`, `UltrasoundDisplay.cs`)
  - `Assets/Scripts/Camera/` (`SceneCameraController.cs`)
  - `Assets/Scripts/UI/` (`UIController.cs`)
  - `Assets/Scripts/Bootstrap/` (`SceneBootstrapper.cs`)
  - `Assets/Tests/` (`VirtualUltrasound.Tests.asmdef`, `Editor/CoordinateTransformTests.cs`, `Editor/SyntheticVolumeTests.cs`, `Editor/SliceGenerationTests.cs`)
  - `tests/VirtualUltrasound.Tests/` (`VirtualUltrasound.Tests.csproj`, `UnityEngineShim.cs`, `StandaloneTests.cs`)
  - `docs/DECISIONS.md`, `docs/PROGRESS.md`, `README.md`
- **Cross-references:** Implements Milestone 1 spec; Decisions #1, #2, #3.
