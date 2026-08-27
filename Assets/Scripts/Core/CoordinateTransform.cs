using UnityEngine;

namespace VirtualUltrasound.Core
{
    /// <summary>
    /// Pure mathematical coordinate transform utilities for the ultrasound simulation pipeline.
    /// Explicitly maps:
    ///   Scanline/Depth Polar (α, r) -> Probe Space (x_P, y_P, z_P) -> World Space (x_W, y_W, z_W) -> Volume Space (x_V, y_V, z_V)
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
        /// Converts polar scanline angle and radial depth into 3D Probe Space coordinates.
        /// angleRad: Azimuthal ray angle relative to central beam axis (angle = 0 at center line).
        /// radius: Distance from the virtual apex center of curvature (radius >= apexRadius).
        /// apexRadius: Curvature radius of the convex probe face (R_c).
        /// </summary>
        public static Vector3 PolarToProbeSpace(float angleRad, float radius, float apexRadius)
        {
            float xP = radius * Mathf.Sin(angleRad);
            float yP = 0f;
            float zP = radius * Mathf.Cos(angleRad) - apexRadius;
            return new Vector3(xP, yP, zP);
        }

        /// <summary>
        /// Converts a 3D Probe Space point into polar ray coordinates (angle and radius relative to the virtual apex).
        /// </summary>
        public static void ProbeSpaceToPolar(Vector3 pointInProbeSpace, float apexRadius, out float angleRad, out float radius)
        {
            float zApex = pointInProbeSpace.z + apexRadius;
            radius = Mathf.Sqrt((pointInProbeSpace.x * pointInProbeSpace.x) + (zApex * zApex));
            angleRad = Mathf.Atan2(pointInProbeSpace.x, zApex);
        }

        /// <summary>
        /// Checks whether a 3D point in Probe Space falls within the valid curvilinear ultrasound sector fan.
        /// </summary>
        public static bool IsInsideCurvilinearSector(Vector3 pointInProbeSpace, float sectorAngleDeg, float apexRadius, float maxDepth)
        {
            if (pointInProbeSpace.z < 0f || pointInProbeSpace.z > maxDepth)
            {
                return false;
            }

            ProbeSpaceToPolar(pointInProbeSpace, apexRadius, out float angleRad, out float radius);

            float halfAngleRad = (sectorAngleDeg * 0.5f) * Mathf.Deg2Rad;
            if (Mathf.Abs(angleRad) > halfAngleRad)
            {
                return false;
            }

            if (radius < apexRadius || radius > (apexRadius + maxDepth))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Converts normalized UV coordinates [0..1] on a curvilinear/convex probe into Probe Space 3D coordinates.
        /// sectorAngleDeg: total field of view angle (e.g. 65 deg).
        /// apexRadius: radius of curvature of convex probe head (e.g. 0.04m).
        /// </summary>
        public static Vector3 UVToCurvilinearProbeSpace(Vector2 uv, float sectorAngleDeg, float apexRadius, float maxDepth)
        {
            float halfAngleRad = (sectorAngleDeg * 0.5f) * Mathf.Deg2Rad;
            float currentAngleRad = Mathf.Lerp(-halfAngleRad, halfAngleRad, uv.x);
            float r = apexRadius + (uv.y * maxDepth);
            return PolarToProbeSpace(currentAngleRad, r, apexRadius);
        }

        /// <summary>
        /// Calculates the full lateral width and axial bounding dimensions of a curvilinear probe's sector fan.
        /// </summary>
        public static void GetSectorBoundingDimensions(
            float sectorAngleDeg,
            float apexRadius,
            float maxDepth,
            out float maxLateralWidth,
            out float totalAxialDepth)
        {
            float halfAngleRad = (sectorAngleDeg * 0.5f) * Mathf.Deg2Rad;
            float maxRadius = apexRadius + maxDepth;
            maxLateralWidth = 2f * maxRadius * Mathf.Sin(halfAngleRad);
            totalAxialDepth = maxDepth;
        }

        /// <summary>
        /// Maps a 2D display pixel (x in [0..width-1], y in [0..height-1]) on a scan-converted Cartesian grid
        /// into Probe Space, checking whether it falls within the authentic ultrasound sector fan.
        /// row y=0 is probe face at top, row y=height-1 is deep tissue.
        /// </summary>
        public static bool DisplayPixelToProbeSpace(
            int pixelX,
            int pixelY,
            int imageWidth,
            int imageHeight,
            float sectorAngleDeg,
            float apexRadius,
            float maxDepth,
            out Vector3 pointInProbeSpace)
        {
            GetSectorBoundingDimensions(sectorAngleDeg, apexRadius, maxDepth, out float lateralSpan, out float axialDepth);

            // Add slight margin so sector arc edges don't touch display borders
            float marginFactor = 1.06f;
            float effectiveLateralSpan = lateralSpan * marginFactor;

            // Normalized coordinates across image: u in [-0.5, +0.5], v in [0, 1]
            float u = imageWidth > 1 ? ((float)pixelX / (imageWidth - 1)) - 0.5f : 0f;
            float v = imageHeight > 1 ? (float)pixelY / (imageHeight - 1) : 0f;

            float xP = u * effectiveLateralSpan;
            float zP = v * axialDepth;

            pointInProbeSpace = new Vector3(xP, 0f, zP);

            return IsInsideCurvilinearSector(pointInProbeSpace, sectorAngleDeg, apexRadius, maxDepth);
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
