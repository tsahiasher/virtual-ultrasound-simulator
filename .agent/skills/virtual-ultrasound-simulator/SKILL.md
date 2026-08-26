---
name: virtual-ultrasound-simulator
description: Guide the design, implementation, debugging, testing, and review of a Unity/C# real-time virtual ultrasound simulator. Use for work on probe geometry, coordinate transforms, synthetic 3D anatomy volumes, 2D slice generation, Texture3D sampling, shaders/compute shaders, ultrasound appearance, acoustic approximations, performance, validation, or architecture in this project. Prefer geometric correctness and a simple working reference implementation before realism or GPU optimization.
---

# Virtual Ultrasound Simulator

Build the simulator incrementally. Preserve a clean separation between geometry, volume data, rendering, interaction, and ultrasound appearance.

## Core priorities

1. Prove geometry before realism.
2. Keep coordinate spaces explicit: image, probe, world, and volume.
3. Maintain a simple reference path before replacing it with GPU code.
4. Keep slice generation replaceable so CPU sampling, Texture3D sampling, fragment shaders, and compute shaders can evolve independently of probe interaction and UI.
5. Target interactive desktop performance, normally 60 FPS, but optimize only after measuring.
6. Avoid backend/network dependencies unless explicitly requested.
7. Avoid unnecessary Unity packages and frameworks.

## Architecture

Prefer focused components with responsibilities equivalent to:

- `ProbeController`: user input and probe pose only.
- `ProbeGeometry`: imaging origin, orientation, field of view, depth, and image-plane geometry.
- `VolumeData`: scalar or labeled 3D anatomy representation and metadata.
- `VolumeSampler`: map sample coordinates into the volume and return tissue/intensity data.
- `SliceRenderer`: generate the 2D slice from probe geometry and volume data.
- `UltrasoundRenderer`: optional later-stage conversion from tissue samples to ultrasound-like echo intensity.
- `SceneCameraController`: orbit/pan/zoom independently of probe controls.
- `UIController`: view layout, debug toggles, and parameters.

Do not collapse these responsibilities into one large MonoBehaviour.

## Coordinate-space rules

Treat transforms as first-class project logic.

For every sampling path, be able to describe the mapping:

`image pixel -> probe/image coordinates -> world coordinates -> volume coordinates`

Keep units documented. Prefer meters or millimeters consistently for physical quantities. Avoid implicit assumptions about Unity units.

When implementing or changing transforms:

- Add a numerical test where practical.
- Add a visual debug representation where practical.
- Verify translation and rotation independently.
- Verify points outside the volume return empty/background values.
- Verify known primitive intersections produce expected circles/ellipses.

Never compensate for a coordinate bug with ad-hoc axis swaps, sign inversions, or offsets without identifying and documenting the underlying convention.

## Development sequence

Follow this order unless the user explicitly asks otherwise.

### Stage 1: geometric reference implementation

Use synthetic anatomy made from primitives or a procedurally generated scalar volume.

Implement:

- movable/rotatable probe
- visible imaging plane
- orbiting 3D camera
- grayscale 2D cross-section
- continuous update as the probe moves
- debug axes/normal/bounds

CPU sampling is acceptable here if it makes correctness easier to validate.

### Stage 2: ultrasound-shaped acquisition geometry

Replace the conceptual rectangular plane when appropriate with probe-specific sampling geometry.

Represent probe parameters explicitly, such as:

- probe type
- aperture/width
- field of view
- imaging depth
- scan-line count
- samples per scan line

For a convex probe, generate sector/fan geometry from scan lines rather than merely masking a rectangular slice.

### Stage 3: GPU volume sampling

Move the validated sampling path to GPU only after the reference path is correct.

Prefer `Texture3D` for volumetric data. Choose fragment or compute shaders based on data flow rather than fashion.

Preserve a CPU/reference mode where practical for regression testing.

Avoid per-frame CPU readbacks and unnecessary texture uploads.

### Stage 4: plausible ultrasound appearance

Add visual ultrasound behavior incrementally rather than implementing full wave physics.

Consider, in order:

- tissue-dependent base scattering
- boundary/gradient response
- depth attenuation
- persistent spatial speckle
- gain and dynamic-range compression
- optional temporal filtering

Tie speckle primarily to spatial tissue coordinates so it remains coherent as the probe moves. Do not regenerate unrelated white noise every frame.

### Stage 5: simple acoustic propagation

Model attenuation and shadowing along each beam.

Represent tissue properties separately from display intensity. Useful properties can include:

- acoustic impedance
- attenuation coefficient
- scatter strength
- density or tissue label

Use simplified approximations first. State clearly where behavior is intentionally non-physical.

### Stage 6: real anatomical data

Only introduce CT/MRI/segmented datasets after the synthetic geometry and rendering pipeline are reliable.

Keep import and preprocessing separate from runtime simulation.

### Stage 7: training/AI extensions

Only after the simulator is usable, consider:

- target-plane scoring
- probe guidance
- pose-to-view datasets
- segmentation ground truth
- synthetic-data generation
- ML models for view classification or navigation

Do not introduce AI merely because the project is built with an AI coding agent.

## Testing and validation

Favor deterministic checks for geometry and sampling.

Create tests for:

- coordinate transforms and inverses
- known probe poses
- boundary conditions
- volume index conversion
- scan-line construction
- expected primitive intersections

Use Unity EditMode tests for pure math/data logic and PlayMode tests only when scene/runtime behavior is necessary.

When a visual result is suspicious, first build a minimal synthetic case instead of tweaking shader constants until it looks right.

## Real-time performance

Before optimizing, measure CPU frame time, GPU frame time, allocations, texture transfers, dispatch dimensions, and resolution-dependent cost.

In per-frame code:

- avoid avoidable managed allocations
- avoid repeated object discovery
- cache component references
- avoid CPU/GPU synchronization points
- avoid recreating textures/buffers when dimensions have not changed

When moving work to shaders, keep parameter ownership and coordinate conventions visible in C# rather than burying project semantics inside shader code.

## Code review rules

When reviewing changes, prioritize:

1. mathematical correctness
2. coordinate-system clarity
3. separation of responsibilities
4. testability
5. frame-time and allocation behavior
6. maintainability
7. visual polish

Call out Unity lifecycle misuse, hidden per-frame allocations, unexplained magic constants, duplicated transforms, CPU/GPU readbacks, and coupling between probe input and rendering.

## Working with the coding agent

For a new feature:

1. Inspect the existing project before proposing architecture changes.
2. State which stage of the roadmap the feature belongs to.
3. Implement the smallest end-to-end version that can be verified.
4. Add validation/debug visualization or tests.
5. Run the relevant Unity tests/build checks available in the environment.
6. Summarize changed files, validation performed, and known limitations.

If a requested feature jumps several stages ahead, implement only the prerequisites needed and preserve the staged architecture unless the user explicitly wants a throwaway experiment.
