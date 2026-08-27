using System;
using UnityEngine;
using VirtualUltrasound.Core;

namespace VirtualUltrasound.Probe
{
    /// <summary>
    /// Defines the geometric parameters and coordinate transformation logic of the virtual ultrasound probe.
    /// Manages aperture width, imaging depth, sector FOV angle, apex curvature radius, and acquisition scan line resolution.
    /// </summary>
    public class ProbeGeometry : MonoBehaviour
    {
        [Header("Probe Configuration")]
        [SerializeField] private ProbeType probeType = ProbeType.Curvilinear;
        [Tooltip("Aperture width across the probe face in meters for linear probe (e.g. 0.05m = 50mm).")]
        [SerializeField] private float apertureWidth = 0.050f;
        [Tooltip("Maximum imaging depth into tissue in meters (e.g. 0.12m = 120mm).")]
        [SerializeField] private float maxDepth = 0.120f;

        [Header("Curvilinear Specific")]
        [Tooltip("Sector angle in degrees for convex/curvilinear probe (e.g. 65 degrees).")]
        [Range(20f, 120f)]
        [SerializeField] private float sectorAngleDegrees = 65f;
        [Tooltip("Radius of curvature of the convex probe face in meters (e.g. 0.04m = 40mm).")]
        [SerializeField] private float convexRadius = 0.040f;

        [Header("Acquisition Resolution (Stage 1 Sampling)")]
        [Tooltip("Number of lateral scan lines (controls volume acquisition rays count).")]
        [Range(8, 1024)]
        [SerializeField] private int scanLines = 128;
        [Tooltip("Number of axial depth samples per scan line (controls volume samples per ray).")]
        [Range(8, 1024)]
        [SerializeField] private int samplesPerScanLine = 128;

        public event Action OnGeometryChanged;

        public ProbeType Type
        {
            get => probeType;
            set { if (probeType != value) { probeType = value; NotifyChanged(); } }
        }

        public float ApertureWidth
        {
            get => apertureWidth;
            set { float v = Mathf.Max(0.005f, value); if (apertureWidth != v) { apertureWidth = v; NotifyChanged(); } }
        }

        public float MaxDepth
        {
            get => maxDepth;
            set { float v = Mathf.Max(0.010f, value); if (maxDepth != v) { maxDepth = v; NotifyChanged(); } }
        }

        public float SectorAngleDegrees
        {
            get => sectorAngleDegrees;
            set { float v = Mathf.Clamp(value, 10f, 160f); if (sectorAngleDegrees != v) { sectorAngleDegrees = v; NotifyChanged(); } }
        }

        public float ConvexRadius
        {
            get => convexRadius;
            set { float v = Mathf.Max(0.005f, value); if (convexRadius != v) { convexRadius = v; NotifyChanged(); } }
        }

        public int ScanLines
        {
            get => scanLines;
            set { int v = Mathf.Clamp(value, 4, 1024); if (scanLines != v) { scanLines = v; NotifyChanged(); } }
        }

        public int SamplesPerScanLine
        {
            get => samplesPerScanLine;
            set { int v = Mathf.Clamp(value, 4, 1024); if (samplesPerScanLine != v) { samplesPerScanLine = v; NotifyChanged(); } }
        }

        private void OnValidate()
        {
            scanLines = Mathf.Clamp(scanLines, 4, 1024);
            samplesPerScanLine = Mathf.Clamp(samplesPerScanLine, 4, 1024);
            sectorAngleDegrees = Mathf.Clamp(sectorAngleDegrees, 10f, 160f);
            apertureWidth = Mathf.Max(0.005f, apertureWidth);
            maxDepth = Mathf.Max(0.010f, maxDepth);
            convexRadius = Mathf.Max(0.005f, convexRadius);

            if (Application.isPlaying)
            {
                NotifyChanged();
            }
        }

        private void NotifyChanged()
        {
            OnGeometryChanged?.Invoke();
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
        /// Gets the central beam propagation direction vector in world space (axial depth axis).
        /// </summary>
        public Vector3 BeamDirection => transform.forward; // Probe Z axis is depth into anatomy

        /// <summary>
        /// Gets the lateral axis across the probe face in world space.
        /// </summary>
        public Vector3 LateralDirection => transform.right; // Probe X axis is lateral width

        /// <summary>
        /// Calculates the directional ray in 3D Probe Space for a given scanline index.
        /// </summary>
        public void GetScanLineRayProbeSpace(int lineIndex, out Vector3 rayOrigin, out Vector3 rayDirection)
        {
            if (scanLines <= 1)
            {
                rayOrigin = Vector3.zero;
                rayDirection = Vector3.forward;
                return;
            }

            float t = (float)lineIndex / (scanLines - 1);

            if (probeType == ProbeType.Linear)
            {
                float xP = (t - 0.5f) * apertureWidth;
                rayOrigin = new Vector3(xP, 0f, 0f);
                rayDirection = Vector3.forward;
            }
            else
            {
                float halfAngleRad = (sectorAngleDegrees * 0.5f) * Mathf.Deg2Rad;
                float angleRad = Mathf.Lerp(-halfAngleRad, halfAngleRad, t);

                // Origin is on the curved transducer face (radius = convexRadius)
                rayOrigin = CoordinateTransform.PolarToProbeSpace(angleRad, convexRadius, convexRadius);

                // Ray direction radiates outwards from virtual apex
                rayDirection = new Vector3(Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad)).normalized;
            }
        }

        /// <summary>
        /// Calculates the directional ray in 3D World Space for a given scanline index.
        /// </summary>
        public void GetScanLineRayWorldSpace(int lineIndex, out Vector3 worldOrigin, out Vector3 worldDirection)
        {
            GetScanLineRayProbeSpace(lineIndex, out Vector3 originP, out Vector3 dirP);
            worldOrigin = CoordinateTransform.ProbeToWorld(originP, transform.position, transform.rotation);
            worldDirection = (transform.rotation * dirP).normalized;
        }

        /// <summary>
        /// Maps normalized slice UV coordinates (u in [0..1], v in [0..1]) into continuous 3D world space.
        /// </summary>
        public Vector3 UVToWorldPosition(Vector2 uv)
        {
            Vector3 pP = UVToProbeSpace(uv);
            return CoordinateTransform.ProbeToWorld(pP, transform.position, transform.rotation);
        }

        /// <summary>
        /// Maps normalized slice UV coordinates into Probe Space (local 3D coordinates relative to probe contact face).
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
        /// Generates the arc points defining the top transducer face and bottom deep boundary of the sector in Probe Space.
        /// </summary>
        public void GetSectorBoundaryPoints(int arcResolution, out Vector3[] topArc, out Vector3[] bottomArc)
        {
            arcResolution = Mathf.Max(2, arcResolution);
            topArc = new Vector3[arcResolution];
            bottomArc = new Vector3[arcResolution];

            if (probeType == ProbeType.Linear)
            {
                float halfW = apertureWidth * 0.5f;
                for (int i = 0; i < arcResolution; i++)
                {
                    float t = (float)i / (arcResolution - 1);
                    float x = Mathf.Lerp(-halfW, halfW, t);
                    topArc[i] = new Vector3(x, 0f, 0f);
                    bottomArc[i] = new Vector3(x, 0f, maxDepth);
                }
            }
            else
            {
                float halfAngleRad = (sectorAngleDegrees * 0.5f) * Mathf.Deg2Rad;
                float rTop = convexRadius;
                float rBottom = convexRadius + maxDepth;

                for (int i = 0; i < arcResolution; i++)
                {
                    float t = (float)i / (arcResolution - 1);
                    float angleRad = Mathf.Lerp(-halfAngleRad, halfAngleRad, t);

                    topArc[i] = CoordinateTransform.PolarToProbeSpace(angleRad, rTop, convexRadius);
                    bottomArc[i] = CoordinateTransform.PolarToProbeSpace(angleRad, rBottom, convexRadius);
                }
            }
        }
    }
}
