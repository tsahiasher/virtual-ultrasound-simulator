using System;
using UnityEngine;

namespace VirtualUltrasound.Volume
{
    /// <summary>
    /// Exact mathematical primitive definitions and containment/SDF evaluations.
    /// </summary>
    public static class PrimitiveShapes
    {
        /// <summary>
        /// Evaluates whether a point is inside an ellipsoid centered at 'center' with semi-axes radii (rx, ry, rz).
        /// Value <= 1.0 means inside the ellipsoid.
        /// </summary>
        public static bool IsInsideEllipsoid(Vector3 point, Vector3 center, Vector3 radii, Quaternion rotation)
        {
            Vector3 local = Quaternion.Inverse(rotation) * (point - center);
            if (radii.x <= 0f || radii.y <= 0f || radii.z <= 0f) return false;

            float nx = local.x / radii.x;
            float ny = local.y / radii.y;
            float nz = local.z / radii.z;

            return (nx * nx + ny * ny + nz * nz) <= 1.0f;
        }

        /// <summary>
        /// Normalized algebraic distance to ellipsoid boundary (< 1 inside, = 1 on surface, > 1 outside).
        /// </summary>
        public static float EllipsoidNormalizedDistance(Vector3 point, Vector3 center, Vector3 radii, Quaternion rotation)
        {
            Vector3 local = Quaternion.Inverse(rotation) * (point - center);
            if (radii.x <= 0f || radii.y <= 0f || radii.z <= 0f) return float.MaxValue;

            float nx = local.x / radii.x;
            float ny = local.y / radii.y;
            float nz = local.z / radii.z;

            return Mathf.Sqrt(nx * nx + ny * ny + nz * nz);
        }

        /// <summary>
        /// Evaluates whether a point is inside a sphere centered at 'center' with radius 'radius'.
        /// </summary>
        public static bool IsInsideSphere(Vector3 point, Vector3 center, float radius)
        {
            return (point - center).sqrMagnitude <= (radius * radius);
        }

        /// <summary>
        /// Distance from point to sphere center minus radius (< 0 inside, > 0 outside).
        /// </summary>
        public static float SphereSDF(Vector3 point, Vector3 center, float radius)
        {
            return Vector3.Distance(point, center) - radius;
        }

        /// <summary>
        /// Evaluates whether a point is inside a finite cylinder defined by start point, end point, and radius.
        /// </summary>
        public static bool IsInsideCylinder(Vector3 point, Vector3 start, Vector3 end, float radius)
        {
            Vector3 axis = end - start;
            float lengthSq = axis.sqrMagnitude;
            if (lengthSq < 1e-6f) return IsInsideSphere(point, start, radius);

            Vector3 ptVec = point - start;
            float t = Vector3.Dot(ptVec, axis) / lengthSq;
            if (t < 0f || t > 1f) return false;

            Vector3 projection = start + t * axis;
            return (point - projection).sqrMagnitude <= (radius * radius);
        }
    }
}
