# Ticket: mesh-rebuild-every-frame

- **Status:** CLOSED (2026-08-29)
- **Opened:** 2026-08-29
- **Closed:** 2026-08-29
- **Resolved By:** Removed UpdatePlaneMesh from Update and subscribed to probeGeometry.OnGeometryChanged event instead.

## Symptom
The `ProbeVisualizer` component rebuilds the 3D imaging plane mesh and border line renderer every frame in its `Update()` method, regardless of whether the probe geometry has changed.

## Evidence
- `ProbeVisualizer.Update()` calls `UpdatePlaneMesh()`.
- `UpdatePlaneMesh()` clears vertex lists, recalculates boundary points, and calls `mesh.SetVertices`, `mesh.SetTriangles`, `mesh.SetUVs`, and `mesh.RecalculateNormals()`.

## Why it matters
Rebuilding the mesh and uploading vertex data to the GPU every frame is extremely inefficient and wastes CPU and GPU resources. The mesh only needs to be updated when the probe's field-of-view parameters (e.g., depth, sector angle) actually change.

## Scope / Next Steps
- Remove `UpdatePlaneMesh()` from the `Update()` loop.
- Subscribe `ProbeVisualizer` to the existing `ProbeGeometry.OnGeometryChanged` event to rebuild the mesh only when necessary.
