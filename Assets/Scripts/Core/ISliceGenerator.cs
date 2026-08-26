using UnityEngine;

namespace VirtualUltrasound.Core
{
    /// <summary>
    /// Contract for generating 2D ultrasound slice data from probe pose and volume data.
    /// Can be implemented on CPU (reference) or GPU (compute shader / fragment shader).
    /// </summary>
    public interface ISliceGenerator
    {
        /// <summary>
        /// Generates a 2D slice into the target SliceBuffer based on probe parameters and volume sampler.
        /// </summary>
        /// <param name="probePos">World-space position of the probe apex.</param>
        /// <param name="probeRot">World-space rotation of the probe.</param>
        /// <param name="apertureWidth">Aperture width in meters.</param>
        /// <param name="maxDepth">Imaging depth in meters.</param>
        /// <param name="probeType">Linear or Curvilinear probe mode.</param>
        /// <param name="sampler">Volume sampler providing 3D tissue/intensity queries.</param>
        /// <param name="outputBuffer">Pre-allocated target buffer to write pixels into.</param>
        void GenerateSlice(
            Vector3 probePos,
            Quaternion probeRot,
            float apertureWidth,
            float maxDepth,
            ProbeType probeType,
            IVolumeSampler sampler,
            SliceBuffer outputBuffer);
    }
}
