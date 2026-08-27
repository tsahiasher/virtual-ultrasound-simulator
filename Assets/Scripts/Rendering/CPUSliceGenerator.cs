using UnityEngine;
using VirtualUltrasound.Core;

namespace VirtualUltrasound.Rendering
{
    /// <summary>
    /// CPU reference implementation of the two-stage ultrasound slice generation pipeline:
    ///   Stage 1: Polar Acoustic Acquisition (ScanLines x SamplesPerScanLine 3D volume queries)
    ///   Stage 2: Cartesian Scan Conversion (PolarBuffer -> SliceBuffer / Texture2D)
    /// Provides deterministic, analytically verified volume sampling without GPU dependencies.
    /// Optimized for zero per-frame heap allocations.
    /// </summary>
    public class CPUSliceGenerator : ISliceGenerator
    {
        /// <summary>
        /// Stage 1: Samples the 3D volume strictly along discrete acoustic scan lines into a PolarBuffer.
        /// Performs exactly ScanLines x SamplesPerScanLine volume queries.
        /// </summary>
        public void AcquirePolarData(
            Vector3 probePos,
            Quaternion probeRot,
            float apertureWidth,
            float maxDepth,
            ProbeType probeType,
            float sectorAngleDeg,
            float apexRadius,
            IVolumeSampler sampler,
            PolarBuffer polarBuffer)
        {
            if (sampler == null || polarBuffer == null) return;

            int lines = polarBuffer.Lines;
            int samples = polarBuffer.Samples;

            float invLinesMinusOne = lines > 1 ? 1.0f / (lines - 1) : 0f;
            float invSamplesMinusOne = samples > 1 ? 1.0f / (samples - 1) : 0f;

            if (probeType == ProbeType.Linear)
            {
                float halfW = apertureWidth * 0.5f;

                for (int i = 0; i < lines; i++)
                {
                    float u = i * invLinesMinusOne;
                    float xP = Mathf.Lerp(-halfW, halfW, u);

                    for (int j = 0; j < samples; j++)
                    {
                        float v = j * invSamplesMinusOne;
                        float zP = v * maxDepth;

                        Vector3 pP = new Vector3(xP, 0f, zP);
                        Vector3 pW = CoordinateTransform.ProbeToWorld(pP, probePos, probeRot);

                        SampleResult result = sampler.SampleWorld(pW);
                        polarBuffer.SetSample(i, j, result.Intensity);
                    }
                }
            }
            else
            {
                float halfAngleRad = (sectorAngleDeg * 0.5f) * Mathf.Deg2Rad;

                for (int i = 0; i < lines; i++)
                {
                    float u = i * invLinesMinusOne;
                    float angleRad = Mathf.Lerp(-halfAngleRad, halfAngleRad, u);

                    float sinA = Mathf.Sin(angleRad);
                    float cosA = Mathf.Cos(angleRad);

                    for (int j = 0; j < samples; j++)
                    {
                        float v = j * invSamplesMinusOne;
                        float r = apexRadius + (v * maxDepth);

                        float xP = r * sinA;
                        float zP = (r * cosA) - apexRadius;

                        Vector3 pP = new Vector3(xP, 0f, zP);
                        Vector3 pW = CoordinateTransform.ProbeToWorld(pP, probePos, probeRot);

                        SampleResult result = sampler.SampleWorld(pW);
                        polarBuffer.SetSample(i, j, result.Intensity);
                    }
                }
            }
        }

        /// <summary>
        /// Stage 2: Converts the polar acoustic buffer into a Cartesian display image with sector masking.
        /// Performs zero 3D volume queries.
        /// </summary>
        public void ScanConvert(
            float apertureWidth,
            float maxDepth,
            ProbeType probeType,
            float sectorAngleDeg,
            float apexRadius,
            PolarBuffer polarBuffer,
            SliceBuffer outputBuffer,
            ScanConversionFilterMode filterMode = ScanConversionFilterMode.Bilinear)
        {
            if (polarBuffer == null || outputBuffer == null) return;

            int width = outputBuffer.Width;
            int height = outputBuffer.Height;

            float invWidthMinusOne = width > 1 ? 1.0f / (width - 1) : 0f;
            float invHeightMinusOne = height > 1 ? 1.0f / (height - 1) : 0f;

            if (probeType == ProbeType.Linear)
            {
                for (int y = 0; y < height; y++)
                {
                    float v = y * invHeightMinusOne;
                    int flippedY = (height - 1) - y;

                    for (int x = 0; x < width; x++)
                    {
                        float u = x * invWidthMinusOne;

                        float intensity = filterMode == ScanConversionFilterMode.Bilinear
                            ? polarBuffer.SampleBilinear(u, v)
                            : polarBuffer.SampleNearest(u, v);

                        outputBuffer.SetPixel(x, flippedY, intensity);
                    }
                }
            }
            else
            {
                float halfAngleRad = (sectorAngleDeg * 0.5f) * Mathf.Deg2Rad;
                float totalAngleRad = 2.0f * halfAngleRad;
                float invTotalAngleRad = totalAngleRad > 1e-5f ? 1.0f / totalAngleRad : 0f;
                float invMaxDepth = maxDepth > 1e-5f ? 1.0f / maxDepth : 0f;

                CoordinateTransform.GetSectorBoundingDimensions(sectorAngleDeg, apexRadius, maxDepth, out float lateralSpan, out float axialDepth);
                float effectiveLateralSpan = lateralSpan * 1.06f; // Margin factor

                for (int y = 0; y < height; y++)
                {
                    float v = y * invHeightMinusOne;
                    int flippedY = (height - 1) - y;
                    float zP = v * axialDepth;
                    float zApex = zP + apexRadius;

                    for (int x = 0; x < width; x++)
                    {
                        float u = (x * invWidthMinusOne) - 0.5f;
                        float xP = u * effectiveLateralSpan;

                        // Polar coordinates relative to apex
                        float r = Mathf.Sqrt((xP * xP) + (zApex * zApex));
                        float angleRad = Mathf.Atan2(xP, zApex);

                        // Sector mask check
                        if (Mathf.Abs(angleRad) > halfAngleRad || r < apexRadius || r > (apexRadius + maxDepth))
                        {
                            // Outside sector fan -> Black acoustic mask
                            outputBuffer.SetPixel(x, flippedY, 0.0f);
                        }
                        else
                        {
                            // Inside sector -> Map (angle, radius) to normalized polar UV [0..1]
                            float uPolar = (angleRad + halfAngleRad) * invTotalAngleRad;
                            float vPolar = (r - apexRadius) * invMaxDepth;

                            float intensity = filterMode == ScanConversionFilterMode.Bilinear
                                ? polarBuffer.SampleBilinear(uPolar, vPolar)
                                : polarBuffer.SampleNearest(uPolar, vPolar);

                            outputBuffer.SetPixel(x, flippedY, intensity);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Executes both Stage 1 (Acquisition) and Stage 2 (Scan Conversion) sequentially.
        /// </summary>
        public void GenerateSlice(
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
            ScanConversionFilterMode filterMode = ScanConversionFilterMode.Bilinear)
        {
            AcquirePolarData(probePos, probeRot, apertureWidth, maxDepth, probeType, sectorAngleDeg, apexRadius, sampler, polarBuffer);
            ScanConvert(apertureWidth, maxDepth, probeType, sectorAngleDeg, apexRadius, polarBuffer, outputBuffer, filterMode);
        }
    }
}
