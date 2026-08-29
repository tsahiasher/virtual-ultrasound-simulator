# Ticket: findobjectoftype-in-update

- **Status:** CLOSED (2026-08-29)
- **Opened:** 2026-08-29
- **Closed:** 2026-08-29
- **Resolved By:** Removed FindReferences from Update in UIController, removed FindObjectOfType from UltrasoundDisplay Update, and cached ProbeGeometry in ProbeController.

## Symptom
Multiple components call `FindObjectOfType` or `GetComponent` repeatedly inside `Update()`, leading to significant per-frame overhead and memory allocations.

## Evidence
- `UIController.Update()` calls `FindReferences()` every frame if a reference is missing, which performs `FindObjectsOfType<Text>(true)`.
- `UltrasoundDisplay.Update()` calls `FindObjectOfType<SliceRenderer>()` every frame if it's missing.
- `ProbeController.HandleInput()` calls `GetComponent<ProbeGeometry>()` every frame.

## Why it matters
Unity's `FindObjectOfType` and `GetComponent` are expensive operations that traverse the scene graph. Calling them per-frame destroys performance and generates garbage collection spikes, which threatens the 60 FPS target for real-time ultrasound simulation.

## Scope / Next Steps
- Cache references in `Awake` or `Start`.
- Replace per-frame `GetComponent` with a cached field (e.g. in `ProbeController`).
- Instead of polling for missing references in `Update`, rely on event subscriptions or a one-time initialization.
