using UnityEngine;

namespace VirtualUltrasound.Core
{
    /// <summary>
    /// Pure mathematical coordinate transform utilities for the ultrasound simulation pipeline.
    /// Explicitly maps:
    ///   Image UV (u, v) -> Probe Space (x_P, y_P, z_P) -> World Space (x_W, y_W, z_W) -> Volume Space (x_V, y_V, z_V)
    /// </summary>
    public static class CoordinateTransform
    {
        /// <summary>
        /// Converts pixel integer coordinates (i, j) to normalized slice UV coordinates in [0, 1].
        /// u = 0 (left edge of probe face), u = 1 (right edge of probe face).
        /// v = 0 (probe face/origin, depth = 0), v = 1 (deepest imaging boundary, depth = D).
        /// </summary>
        public static Vector2 PixelToUV(int pixelX, int pixelY, int width, int height)
        {
            float u = width > 1 ? (float)pixelX / (width - 1) : 0.5f;
            float v = height > 1 ? (float)pixelY / (height - 1) : 0.5f;
            return new Vector2(u, v);
        }

        /// <summary>
        /// Converts normalized UV coordinates [0..1] on a linear probe into Probe Space 3D coordinates.
        /// Probe Space Convention:
        ///   X_P: Lateral axis across probe aperture [-ApertureWidth/2, +ApertureWidth/2].
        ///   Y_P: Elevation axis normal to the imaging plane (Y_P = 0 on plane).
        ///   Z_P: Beam propagation / axial depth axis into tissue [0, Depth].
        /// </summary>
        public static Vector3 UVToLinearProbeSpace(Vector2 uv, float apertureWidth, float maxDepth)
        {
            float xP = (uv.x - 0.5f) * apertureWidth;
            float yP = 0f;
            float zP = uv.y * maxDepth;
            return new Vector3(xP, yP, zP);
        }

        /// <summary>
        /// Converts normalized UV coordinates [0..1] on a curvilinear/convex probe into Probe Space 3D coordinates.
        /// sectorAngleDeg: total field of view angle (e.g. 60 deg).
        /// apexRadius: radius of curvature of convex probe head.
        /// </summary>
        public static Vector3 UVToCurvilinearProbeSpace(Vector2 uv, float sectorAngleDeg, float apexRadius, float maxDepth)
        {
            float halfAngleRad = (sectorAngleDeg * 0.5f) * Mathf.Deg2Rad;
            float currentAngleRad = Mathf.Lerp(-halfAngleRad, halfAngleRad, uv.x);
            float r = apexRadius + uv.y * maxDepth;

            // In probe space: lateral is X, depth is Z
            float xP = r * Mathf.Sin(currentAngleRad);
            float yP = 0f;
            float zP = r * Mathf.Cos(currentAngleRad) - apexRadius;
            return new Vector3(xP, yP, zP);
        }

        /// <summary>
        /// Transforms a point from Probe Space to World Space given probe position and orientation.
        /// p_W = probePos + probeRot * p_P
        /// </summary>
        public static Vector3 ProbeToWorld(Vector3 pointInProbeSpace, Vector3 probePosition, Quaternion probeRotation)
        {
            return probePosition + (probeRotation * pointInProbeSpace);
        }

        /// <summary>
        /// Transforms a point from World Space back into Probe Space.
        /// p_P = inv(probeRot) * (p_W - probePos)
        /// </summary>
        public static Vector3 WorldToProbe(Vector3 pointInWorldSpace, Vector3 probePosition, Quaternion probeRotation)
        {
            return Quaternion.Inverse(probeRotation) * (pointInWorldSpace - probePosition);
        }

        /// <summary>
        /// Transforms a world space point into local volume space relative to volume center and rotation.
        /// </summary>
        public static Vector3 WorldToVolume(Vector3 worldPoint, Vector3 volumeCenter, Quaternion volumeRotation)
        {
            return Quaternion.Inverse(volumeRotation) * (worldPoint - volumeCenter);
        }

        /// <summary>
        /// Full chain: maps normalized slice UV directly to 3D World Space for a linear probe.
        /// </summary>
        public static Vector3 LinearUVToWorld(Vector2 uv, float apertureWidth, float maxDepth, Vector3 probePos, Quaternion probeRot)
        {
            Vector3 pP = UVToLinearProbeSpace(uv, apertureWidth, maxDepth);
            return ProbeToWorld(pP, probePos, probeRot);
        }
    }
}
