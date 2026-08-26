using System;
using UnityEngine;
using VirtualUltrasound.Core;

namespace VirtualUltrasound.Probe
{
    /// <summary>
    /// Defines the geometric parameters and coordinate transformation logic of the virtual ultrasound probe.
    /// Manages aperture width, imaging depth, field of view, and scan line resolution.
    /// </summary>
    public class ProbeGeometry : MonoBehaviour
    {
        [Header("Probe Configuration")]
        [SerializeField] private ProbeType probeType = ProbeType.Linear;
        [Tooltip("Aperture width across the probe face in meters (e.g. 0.05m = 50mm).")]
        [SerializeField] private float apertureWidth = 0.050f;
        [Tooltip("Maximum imaging depth into tissue in meters (e.g. 0.12m = 120mm).")]
        [SerializeField] private float maxDepth = 0.120f;

        [Header("Curvilinear Specific")]
        [Tooltip("Sector angle in degrees for convex/curvilinear probe.")]
        [Range(20f, 120f)]
        [SerializeField] private float sectorAngleDegrees = 65f;
        [Tooltip("Radius of curvature of the convex probe face in meters.")]
        [SerializeField] private float convexRadius = 0.040f;

        [Header("Sampling Resolution")]
        [Tooltip("Number of lateral scan lines (slice image width).")]
        [Range(32, 512)]
        [SerializeField] private int scanLines = 128;
        [Tooltip("Number of axial samples per scan line (slice image height).")]
        [Range(32, 512)]
        [SerializeField] private int samplesPerScanLine = 128;

        public ProbeType Type
        {
            get => probeType;
            set => probeType = value;
        }

        public float ApertureWidth
        {
            get => apertureWidth;
            set => apertureWidth = Mathf.Max(0.005f, value);
        }

        public float MaxDepth
        {
            get => maxDepth;
            set => maxDepth = Mathf.Max(0.010f, value);
        }

        public float SectorAngleDegrees
        {
            get => sectorAngleDegrees;
            set => sectorAngleDegrees = Mathf.Clamp(value, 10f, 160f);
        }

        public float ConvexRadius
        {
            get => convexRadius;
            set => convexRadius = Mathf.Max(0.005f, value);
        }

        public int ScanLines
        {
            get => scanLines;
            set => scanLines = Mathf.Clamp(value, 16, 1024);
        }

        public int SamplesPerScanLine
        {
            get => samplesPerScanLine;
            set => samplesPerScanLine = Mathf.Clamp(value, 16, 1024);
        }

        /// <summary>
        /// Gets the world-space origin of the transducer contact face.
        /// </summary>
        public Vector3 Origin => transform.position;

        /// <summary>
        /// Gets the world-space orientation of the probe.
        /// </summary>
        public Quaternion Orientation => transform.rotation;

        /// <summary>
        /// Gets the normal direction vector to the imaging plane in world space (elevation axis).
        /// </summary>
        public Vector3 ImagingPlaneNormal => transform.up; // Probe Y axis is normal to X-Z imaging plane

        /// <summary>
        /// Gets the beam propagation direction vector in world space (axial depth axis).
        /// </summary>
        public Vector3 BeamDirection => transform.forward; // Probe Z axis is depth into anatomy

        /// <summary>
        /// Gets the lateral axis across the probe face in world space.
        /// </summary>
        public Vector3 LateralDirection => transform.right; // Probe X axis is lateral width

        /// <summary>
        /// Maps normalized slice UV coordinates (u in [0..1], v in [0..1]) into continuous 3D world space.
        /// </summary>
        public Vector3 UVToWorldPosition(Vector2 uv)
        {
            Vector3 pP = UVToProbeSpace(uv);
            return CoordinateTransform.ProbeToWorld(pP, transform.position, transform.rotation);
        }

        /// <summary>
        /// Maps normalized slice UV coordinates into Probe Space (local 3D coordinates relative to probe apex).
        /// </summary>
        public Vector3 UVToProbeSpace(Vector2 uv)
        {
            if (probeType == ProbeType.Linear)
            {
                return CoordinateTransform.UVToLinearProbeSpace(uv, apertureWidth, maxDepth);
            }
            else
            {
                return CoordinateTransform.UVToCurvilinearProbeSpace(uv, sectorAngleDegrees, convexRadius, maxDepth);
            }
        }

        /// <summary>
        /// Computes the 4 boundary corners of the imaging plane in Probe Space.
        /// Order: Top-Left (u=0, v=0), Top-Right (u=1, v=0), Bottom-Right (u=1, v=1), Bottom-Left (u=0, v=1).
        /// </summary>
        public void GetPlaneCornersProbeSpace(out Vector3 topLeft, out Vector3 topRight, out Vector3 bottomRight, out Vector3 bottomLeft)
        {
            topLeft = UVToProbeSpace(new Vector2(0f, 0f));
            topRight = UVToProbeSpace(new Vector2(1f, 0f));
            bottomRight = UVToProbeSpace(new Vector2(1f, 1f));
            bottomLeft = UVToProbeSpace(new Vector2(0f, 1f));
        }

        /// <summary>
        /// Computes the 4 boundary corners of the imaging plane in World Space.
        /// </summary>
        public void GetPlaneCornersWorldSpace(out Vector3 topLeft, out Vector3 topRight, out Vector3 bottomRight, out Vector3 bottomLeft)
        {
            GetPlaneCornersProbeSpace(out Vector3 tlP, out Vector3 trP, out Vector3 brP, out Vector3 blP);
            Vector3 pos = transform.position;
            Quaternion rot = transform.rotation;
            topLeft = CoordinateTransform.ProbeToWorld(tlP, pos, rot);
            topRight = CoordinateTransform.ProbeToWorld(trP, pos, rot);
            bottomRight = CoordinateTransform.ProbeToWorld(brP, pos, rot);
            bottomLeft = CoordinateTransform.ProbeToWorld(blP, pos, rot);
        }
    }
}
