using System;
using UnityEngine;

namespace VirtualUltrasound.Core
{
    /// <summary>
    /// Identifies the type of anatomical tissue or region sampled in the volume.
    /// </summary>
    public enum TissueType
    {
        Background = 0,
        BodyTissue = 1,
        Organ1 = 2,
        Organ2Vessel = 3,
        Bone = 4,
        Fluid = 5
    }

    /// <summary>
    /// Type of virtual ultrasound probe geometry.
    /// </summary>
    public enum ProbeType
    {
        /// <summary>
        /// Linear probe producing a rectangular field of view.
        /// </summary>
        Linear,

        /// <summary>
        /// Convex/Curvilinear probe producing a sector/fan field of view.
        /// </summary>
        Curvilinear
    }

    /// <summary>
    /// Filter mode applied during Cartesian scan conversion from polar acoustic buffer.
    /// </summary>
    public enum ScanConversionFilterMode
    {
        /// <summary>
        /// Smooth bilinear interpolation between adjacent scan lines and depth samples.
        /// </summary>
        Bilinear = 0,

        /// <summary>
        /// Nearest-neighbor sampling (exposes raw ray and sample discretization).
        /// </summary>
        NearestNeighbor = 1
    }

    /// <summary>
    /// Debug inspection modes for the Phase 4 ultrasound appearance pipeline.
    /// </summary>
    public enum AppearanceDebugView
    {
        /// <summary>
        /// Complete Phase 4 B-mode ultrasound appearance (boundary + speckle scatter + attenuation + gain + compression).
        /// </summary>
        FinalUltrasound = 0,

        /// <summary>
        /// Raw anatomical scalar values (Phase 3 reference baseline).
        /// </summary>
        RawAnatomical = 1,

        /// <summary>
        /// Isolated boundary/interface gradient reflection echo response.
        /// </summary>
        BoundaryResponse = 2,

        /// <summary>
        /// Isolated distributed tissue scattering modulated by 3D coherent speckle.
        /// </summary>
        SpeckleScattering = 3
    }

    /// <summary>
    /// Configurable parameters for the B-mode ultrasound acoustic appearance model.
    /// Operates strictly in the polar acquisition domain.
    /// </summary>
    [Serializable]
    public struct UltrasoundAppearanceSettings
    {
        [Tooltip("Enable B-mode ultrasound acoustic appearance model (false reverts to raw anatomical slice).")]
        public bool Enabled;

        [Tooltip("Intermediate pipeline debug inspection view.")]
        public AppearanceDebugView DebugView;

        [Tooltip("Overall ultrasound system gain.")]
        [Range(0.1f, 5.0f)]
        public float Gain;

        [Tooltip("Strength of specular interface / boundary echoes calculated from spatial tissue gradients.")]
        [Range(0.0f, 5.0f)]
        public float BoundaryStrength;

        [Tooltip("Modulation amplitude of 3D spatially coherent speckle noise.")]
        [Range(0.0f, 1.0f)]
        public float SpeckleStrength;

        [Tooltip("Spatial frequency of 3D coherent speckle pattern in cycles per meter.")]
        [Range(50.0f, 3000.0f)]
        public float SpeckleScale;

        [Tooltip("Acoustic depth attenuation coefficient along the scanline beam.")]
        [Range(0.0f, 20.0f)]
        public float DepthAttenuation;

        [Tooltip("Logarithmic dynamic range compression ratio.")]
        [Range(1.0f, 100.0f)]
        public float CompressionRatio;

        public static UltrasoundAppearanceSettings Default => new UltrasoundAppearanceSettings
        {
            Enabled = true,
            DebugView = AppearanceDebugView.FinalUltrasound,
            Gain = 1.25f,
            BoundaryStrength = 1.8f,
            SpeckleStrength = 0.60f,
            SpeckleScale = 850.0f,
            DepthAttenuation = 4.0f,
            CompressionRatio = 20.0f
        };
    }

    /// <summary>
    /// Represents the sample result at a specific spatial coordinate within the volume.
    /// </summary>
    [Serializable]
    public struct SampleResult
    {
        /// <summary>
        /// Normalized acoustic intensity / density [0.0, 1.0].
        /// 0.0 = completely anechoic / empty background (black).
        /// 1.0 = maximum echogenicity (white).
        /// </summary>
        public float Intensity;

        /// <summary>
        /// Diffuse backscatter coefficient of the material [0.0, 1.0].
        /// </summary>
        public float Scattering;

        /// <summary>
        /// Tissue category for acoustic simulation and material differentiation.
        /// </summary>
        public TissueType Tissue;

        public SampleResult(float intensity, TissueType tissue)
        {
            Intensity = Mathf.Clamp01(intensity);
            Scattering = Mathf.Clamp01(intensity * 0.5f);
            Tissue = tissue;
        }

        public SampleResult(float intensity, float scattering, TissueType tissue)
        {
            Intensity = Mathf.Clamp01(intensity);
            Scattering = Mathf.Clamp01(scattering);
            Tissue = tissue;
        }

        public static SampleResult Empty => new SampleResult(0f, 0f, TissueType.Background);
    }

    /// <summary>
    /// Oriented bounding box representation in 3D world space.
    /// </summary>
    [Serializable]
    public struct VolumeBounds
    {
        public Vector3 Center;
        public Vector3 Extents;

        public Vector3 Min => Center - Extents;
        public Vector3 Max => Center + Extents;
        public Vector3 Size => Extents * 2f;

        public VolumeBounds(Vector3 center, Vector3 extents)
        {
            Center = center;
            Extents = extents;
        }

        public bool Contains(Vector3 worldPoint)
        {
            Vector3 min = Min;
            Vector3 max = Max;
            return worldPoint.x >= min.x && worldPoint.x <= max.x &&
                   worldPoint.y >= min.y && worldPoint.y <= max.y &&
                   worldPoint.z >= min.z && worldPoint.z <= max.z;
        }
    }
}
