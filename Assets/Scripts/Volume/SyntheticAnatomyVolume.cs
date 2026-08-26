using System;
using UnityEngine;
using VirtualUltrasound.Core;

namespace VirtualUltrasound.Volume
{
    /// <summary>
    /// Procedural 3D synthetic anatomical volume component composed of analytical primitives.
    /// Provides ground-truth intensity distributions with known geometrical properties for verification.
    /// </summary>
    public class SyntheticAnatomyVolume : MonoBehaviour, IVolumeData
    {
        [Header("Body Region (Ellipsoid)")]
        [Tooltip("Center of the body region in local coordinates.")]
        [SerializeField] private Vector3 bodyCenter = Vector3.zero;
        [Tooltip("Semi-axes radii (X, Y, Z) in meters.")]
        [SerializeField] private Vector3 bodyRadii = new Vector3(0.12f, 0.16f, 0.10f);
        [Tooltip("Acoustic intensity of surrounding parenchymal body tissue.")]
        [Range(0f, 1f)]
        [SerializeField] private float bodyIntensity = 0.25f;

        [Header("Organ 1 - Hyperechoic Structure (Sphere)")]
        [SerializeField] private Vector3 organ1Center = new Vector3(0.035f, 0.02f, 0.015f);
        [SerializeField] private float organ1Radius = 0.035f;
        [Range(0f, 1f)]
        [SerializeField] private float organ1Intensity = 0.65f;

        [Header("Organ 2 - Anechoic Cyst / Fluid Cavity (Sphere)")]
        [SerializeField] private Vector3 organ2Center = new Vector3(-0.04f, -0.015f, -0.01f);
        [SerializeField] private float organ2Radius = 0.025f;
        [Range(0f, 1f)]
        [SerializeField] private float organ2Intensity = 0.04f;

        [Header("Vessel / Tubular Structure (Cylinder)")]
        [SerializeField] private Vector3 vesselStart = new Vector3(-0.08f, -0.06f, 0.03f);
        [SerializeField] private Vector3 vesselEnd = new Vector3(0.08f, 0.06f, -0.03f);
        [SerializeField] private float vesselOuterRadius = 0.018f;
        [SerializeField] private float vesselWallThickness = 0.003f;
        [Range(0f, 1f)]
        [SerializeField] private float vesselWallIntensity = 0.88f;
        [Range(0f, 1f)]
        [SerializeField] private float vesselLumenIntensity = 0.05f;

        public string VolumeName => "Procedural Synthetic Anatomy Volume";

        public VolumeBounds WorldBounds
        {
            get
            {
                Vector3 center = transform.position + bodyCenter;
                Vector3 extents = Vector3.Scale(bodyRadii * 1.15f, transform.lossyScale);
                return new VolumeBounds(center, extents);
            }
        }

        public Vector3 BodyCenter => transform.TransformPoint(bodyCenter);
        public Vector3 BodyRadii => Vector3.Scale(bodyRadii, transform.lossyScale);
        public float BodyIntensity => bodyIntensity;

        public Vector3 Organ1Center => transform.TransformPoint(organ1Center);
        public float Organ1Radius => organ1Radius * transform.lossyScale.x;
        public float Organ1Intensity => organ1Intensity;

        public Vector3 Organ2Center => transform.TransformPoint(organ2Center);
        public float Organ2Radius => organ2Radius * transform.lossyScale.x;
        public float Organ2Intensity => organ2Intensity;

        public Vector3 VesselStart => transform.TransformPoint(vesselStart);
        public Vector3 VesselEnd => transform.TransformPoint(vesselEnd);
        public float VesselOuterRadius => vesselOuterRadius * transform.lossyScale.x;
        public float VesselWallThickness => vesselWallThickness * transform.lossyScale.x;

        public bool IsInBounds(Vector3 worldPos)
        {
            return WorldBounds.Contains(worldPos);
        }

        /// <summary>
        /// Evaluates sample result at continuous world coordinate using exact analytical equations.
        /// </summary>
        public SampleResult EvaluateSample(Vector3 worldPos)
        {
            // 1. First check if outside the main body region
            Vector3 worldBodyCenter = BodyCenter;
            Vector3 worldBodyRadii = BodyRadii;
            Quaternion worldBodyRot = transform.rotation;

            if (!PrimitiveShapes.IsInsideEllipsoid(worldPos, worldBodyCenter, worldBodyRadii, worldBodyRot))
            {
                return SampleResult.Empty;
            }

            // 2. Check inner high-priority structures:
            // Organ 2 (Anechoic cyst / fluid cavity)
            Vector3 worldOrgan2Center = Organ2Center;
            float worldOrgan2Radius = Organ2Radius;
            if (PrimitiveShapes.IsInsideSphere(worldPos, worldOrgan2Center, worldOrgan2Radius))
            {
                return new SampleResult(organ2Intensity, TissueType.Fluid);
            }

            // Vessel (Tubular structure with echogenic wall & anechoic lumen)
            Vector3 worldVesselStart = VesselStart;
            Vector3 worldVesselEnd = VesselEnd;
            float worldVesselOuterRadius = VesselOuterRadius;
            if (PrimitiveShapes.IsInsideCylinder(worldPos, worldVesselStart, worldVesselEnd, worldVesselOuterRadius))
            {
                float lumenRadius = Mathf.Max(0.001f, worldVesselOuterRadius - VesselWallThickness);
                if (PrimitiveShapes.IsInsideCylinder(worldPos, worldVesselStart, worldVesselEnd, lumenRadius))
                {
                    return new SampleResult(vesselLumenIntensity, TissueType.Fluid);
                }
                return new SampleResult(vesselWallIntensity, TissueType.Bone); // highly reflective wall
            }

            // Organ 1 (Hyperechoic spherical lesion / organ)
            Vector3 worldOrgan1Center = Organ1Center;
            float worldOrgan1Radius = Organ1Radius;
            if (PrimitiveShapes.IsInsideSphere(worldPos, worldOrgan1Center, worldOrgan1Radius))
            {
                return new SampleResult(organ1Intensity, TissueType.Organ1);
            }

            // 3. Surrounding body tissue
            return new SampleResult(bodyIntensity, TissueType.BodyTissue);
        }
    }
}
