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
    /// Represents the sample result at a specific spatial coordinate within the volume.
    /// </summary>
    [Serializable]
    public struct SampleResult
    {
        /// <summary>
        /// Normalized acoustic intensity / echogenicity [0.0, 1.0].
        /// 0.0 = completely anechoic / empty background (black).
        /// 1.0 = maximum echogenicity (white).
        /// </summary>
        public float Intensity;

        /// <summary>
        /// Tissue category for acoustic simulation and material differentiation.
        /// </summary>
        public TissueType Tissue;

        public SampleResult(float intensity, TissueType tissue)
        {
            Intensity = Mathf.Clamp01(intensity);
            Tissue = tissue;
        }

        public static SampleResult Empty => new SampleResult(0f, TissueType.Background);
    }

    /// <summary>
    /// Oriented bounding box representation in 3D world space.
    /// </summary>
    [Serializable]
    public struct VolumeBounds
    {
        public Vector3 Center;
        public Vector3 Extents;

        public VolumeBounds(Vector3 center, Vector3 extents)
        {
            Center = center;
            Extents = extents;
        }

        public bool Contains(Vector3 worldPoint)
        {
            Vector3 min = Center - Extents;
            Vector3 max = Center + Extents;
            return worldPoint.x >= min.x && worldPoint.x <= max.x &&
                   worldPoint.y >= min.y && worldPoint.y <= max.y &&
                   worldPoint.z >= min.z && worldPoint.z <= max.z;
        }
    }
}
