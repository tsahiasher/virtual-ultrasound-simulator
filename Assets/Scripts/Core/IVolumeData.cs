using UnityEngine;

namespace VirtualUltrasound.Core
{
    /// <summary>
    /// Contract for 3D anatomical volume data representation and spatial bounds.
    /// </summary>
    public interface IVolumeData
    {
        /// <summary>
        /// Gets the spatial bounding box of the anatomical volume in world space.
        /// </summary>
        VolumeBounds WorldBounds { get; }

        /// <summary>
        /// Gets the human-readable name or descriptor of this anatomical volume.
        /// </summary>
        string VolumeName { get; }

        /// <summary>
        /// Checks if a world position lies within the gross bounding region of this volume.
        /// </summary>
        /// <param name="worldPos">Position in world space.</param>
        /// <returns>True if within bounding volume.</returns>
        bool IsInBounds(Vector3 worldPos);
    }
}
