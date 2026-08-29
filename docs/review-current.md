# Virtual Ultrasound Simulator - Review Report

## 1. Architecture
- **Must fix:** `ProbeVisualizer` updates its mesh every frame, even when geometry hasn't changed.
- **Should fix:** None.
- **Working well:** The codebase is well-structured and maintains a clear separation of responsibilities. `ProbeGeometry` handles parameters, `CoordinateTransform` handles pure math, `UIController` handles UI, `SliceRenderer` handles the pipeline, and `CPUSliceGenerator`/`GPUSliceGenerator` handle the actual sampling. The two-stage architecture is properly implemented.

## 2. Coordinate systems
- **Must fix:** None.
- **Should fix:** None.
- **Working well:** Clean separation. `CPUSliceGenerator` properly handles `flippedY` for Texture2D mapping, and `GPUVolumeData` explicitly handles bounding boxes for Texture3D. Handled well.

## 3. Geometry correctness
- **Must fix:** None.
- **Should fix:** None.
- **Working well:** The geometry behaves correctly. Probe transformations are appropriately handled.

## 4. Unity/C# quality
- **Must fix:** `FindObjectOfType` and `GetComponent` used repeatedly in `Update` / `LateUpdate` / `HandleInput` methods in `UIController`, `UltrasoundDisplay`, and `ProbeController`. This causes unnecessary allocations and performance hits. (High severity).
- **Should fix:** None.
- **Working well:** Appropriate use of components, plain C# math functions, and clean data passing.

## 5. Performance
- **Must fix:** The per-frame `FindObjectOfType` and Mesh rebuilding are the main performance detriments and must be fixed.
- **Should fix:** None.
- **Working well:** GPU compute is fast, CPU reference is reasonable (262k samples @ 36 FPS). GPU compute dispatch is sub-millisecond.

## 6. Testing
- **Must fix:** None.
- **Should fix:** None.
- **Working well:** Excellent coverage. NUnit test suite has 26 tests passing, validating geometry, ray direction parity, attenuation, and rendering pipelines.

## 7. Debuggability
- **Must fix:** None.
- **Should fix:** None.
- **Working well:** The project implements helpful visualizers (`ProbeVisualizer`, `AnatomyVisualizer`) and debug render modes (`FinalUltrasound`, `RawAnatomical`, `BoundaryResponse`, `SpeckleScattering`).

## 8. Roadmap discipline
- **Must fix:** None.
- **Should fix:** None.
- **Working well:** The project is strictly at Phase 4, focusing on B-Mode acoustic appearance. Realistic ultrasound physics, GPU optimization, AI, and medical datasets are intentionally delayed or well-scoped.

---

## Detailed Findings

### 1. `FindObjectOfType` and `GetComponent` in Update
**Severity:** High
**Location:** `Assets/Scripts/UI/UIController.cs`, `Assets/Scripts/Rendering/UltrasoundDisplay.cs`, `Assets/Scripts/Probe/ProbeController.cs`
**Problem:** Multiple components call `FindObjectOfType` or `GetComponent` repeatedly inside `Update()` or `HandleInput()`.
**Why it matters:** Unity's `FindObjectOfType` and `GetComponent` are expensive operations. Calling them per-frame destroys performance and generates garbage collection spikes, which threatens the 60 FPS target for real-time ultrasound simulation.
**Recommended change:** Cache references in `Awake` or `Start`. Replace per-frame `GetComponent` with a cached field (e.g. in `ProbeController`). Instead of polling for missing references in `Update`, rely on event subscriptions or a one-time initialization.
**Ticket:** `docs/tickets/findobjectoftype-in-update.md`

### 2. Mesh Rebuild Every Frame
**Severity:** High
**Location:** `Assets/Scripts/Probe/ProbeVisualizer.cs`
**Problem:** Rebuilds the 3D imaging plane mesh and border line renderer every frame in its `Update()` method, regardless of whether the probe geometry has changed.
**Why it matters:** Rebuilding the mesh and uploading vertex data to the GPU every frame is extremely inefficient and wastes CPU and GPU resources. The mesh only needs to be updated when the probe's field-of-view parameters actually change.
**Recommended change:** Remove `UpdatePlaneMesh()` from the `Update()` loop. Subscribe `ProbeVisualizer` to the existing `ProbeGeometry.OnGeometryChanged` event to rebuild the mesh only when necessary.
**Ticket:** `docs/tickets/mesh-rebuild-every-frame.md`

---

## Final assessment

### Overall assessment
The current implementation and architecture are solid, decoupled, and align well with the roadmap. The core coordinate math and pipeline stages are well-separated. However, two significant performance bottlenecks exist in the form of per-frame mesh rebuilds and expensive component lookups in `Update()`.

### What is working well
The decoupled two-stage acquisition and Cartesian scan-conversion architecture is working exceptionally well. The separation of `ProbeGeometry`, `CoordinateTransform`, and the `ISliceGenerator` implementations allows for robust testing and clean performance scaling. The automated test suite coverage is also a strong point that should be preserved.

### Top 5 actions
1. Fix per-frame `GetComponent` and `FindObjectOfType` in `UIController`, `UltrasoundDisplay`, and `ProbeController`. (Ticket: `docs/tickets/findobjectoftype-in-update.md`)
2. Fix per-frame mesh rebuild in `ProbeVisualizer` by hooking into the `ProbeGeometry.OnGeometryChanged` event. (Ticket: `docs/tickets/mesh-rebuild-every-frame.md`)

### Ready for next phase?
**Yes with minor fixes**
The architecture is ready to proceed to Phase 5 (Acoustic Propagation), but the identified per-frame performance overheads must be addressed first to maintain the strict 60 FPS target.

### Review disposition
- **Must fix:**
  - `FindObjectOfType` and `GetComponent` in Update (`docs/tickets/findobjectoftype-in-update.md`)
  - Mesh Rebuild Every Frame (`docs/tickets/mesh-rebuild-every-frame.md`)
- **Should fix:** None
- **Optional/deferred:** None

### Tickets created or updated
- `findobjectoftype-in-update.md` - Open - High - Fix `FindObjectOfType` and `GetComponent` in Update.
- `mesh-rebuild-every-frame.md` - Open - High - Fix per-frame mesh rebuild in `ProbeVisualizer`.
