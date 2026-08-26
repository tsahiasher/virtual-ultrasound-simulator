using UnityEngine;

namespace VirtualUltrasound.Core
{
    /// <summary>
    /// Contract for sampling anatomical/tissue intensity and properties at continuous 3D world coordinates.
    /// This abstraction allows seamless swapping between procedural math, Texture3D sampling, GPU compute,
    /// or medical voxel grids (CT/MRI) without modifying probe interaction or slice display.
    /// </summary>
    public interface IVolumeSampler
    {
        /// <summary>
        /// Samples the anatomical volume at the specified continuous 3D world coordinates.
        /// </summary>
        /// <param name="worldPosition">Continuous 3D position in world space.</param>
        /// <returns>Sample result containing intensity [0..1] and tissue classification.</returns>
        SampleResult SampleWorld(Vector3 worldPosition);
    }
}
