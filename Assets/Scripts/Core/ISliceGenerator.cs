using UnityEngine;

namespace VirtualUltrasound.Core
{
    /// <summary>
    /// Contract for generating 2D ultrasound slice data from probe pose and volume data.
    /// Strictly decoupled into:
    ///   Stage 1: Polar Acoustic Acquisition (ScanLines x SamplesPerScanLine volume queries)
    ///   Stage 2: Cartesian Scan Conversion (PolarBuffer -> SliceBuffer / Texture2D)
    /// </summary>
    public interface ISliceGenerator
    {
        /// <summary>
        /// Stage 1: Samples the 3D volume strictly along discrete acoustic rays into a PolarBuffer.
        /// Performs exactly ScanLines x SamplesPerScanLine volume queries.
        /// </summary>
        void AcquirePolarData(
            Vector3 probePos,
            Quaternion probeRot,
            float apertureWidth,
            float maxDepth,
            ProbeType probeType,
            float sectorAngleDeg,
            float apexRadius,
            IVolumeSampler sampler,
            PolarBuffer polarBuffer);

        /// <summary>
        /// Stage 2: Converts the polar acoustic buffer into a Cartesian display image with sector masking.
        /// Performs zero 3D volume queries.
        /// </summary>
        void ScanConvert(
            float apertureWidth,
            float maxDepth,
            ProbeType probeType,
            float sectorAngleDeg,
            float apexRadius,
            PolarBuffer polarBuffer,
            SliceBuffer outputBuffer,
            ScanConversionFilterMode filterMode = ScanConversionFilterMode.Bilinear);

        /// <summary>
        /// Executes both Stage 1 and Stage 2 sequentially.
        /// </summary>
        void GenerateSlice(
            Vector3 probePos,
            Quaternion probeRot,
            float apertureWidth,
            float maxDepth,
            ProbeType probeType,
            float sectorAngleDeg,
            float apexRadius,
            IVolumeSampler sampler,
            PolarBuffer polarBuffer,
            SliceBuffer outputBuffer,
            ScanConversionFilterMode filterMode = ScanConversionFilterMode.Bilinear);
    }
}
