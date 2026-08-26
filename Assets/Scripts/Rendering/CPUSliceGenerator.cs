using UnityEngine;
using VirtualUltrasound.Core;

namespace VirtualUltrasound.Rendering
{
    /// <summary>
    /// CPU reference implementation of 2D ultrasound slice rasterization.
    /// Provides deterministic, analytically verified volume sampling without GPU dependencies.
    /// Optimized for zero per-frame heap allocations.
    /// </summary>
    public class CPUSliceGenerator : ISliceGenerator
    {
        public void GenerateSlice(
            Vector3 probePos,
            Quaternion probeRot,
            float apertureWidth,
            float maxDepth,
            ProbeType probeType,
            IVolumeSampler sampler,
            SliceBuffer outputBuffer)
        {
            if (sampler == null || outputBuffer == null) return;

            int width = outputBuffer.Width;
            int height = outputBuffer.Height;

            // Pre-cache coordinate mapping factors
            float invWidthMinusOne = width > 1 ? 1.0f / (width - 1) : 0f;
            float invHeightMinusOne = height > 1 ? 1.0f / (height - 1) : 0f;

            for (int y = 0; y < height; y++)
            {
                float v = y * invHeightMinusOne; // Depth coordinate [0..1]

                for (int x = 0; x < width; x++)
                {
                    float u = x * invWidthMinusOne; // Lateral coordinate [0..1]
                    Vector2 uv = new Vector2(u, v);

                    // 1. Image UV -> Probe Space
                    Vector3 pP;
                    if (probeType == ProbeType.Linear)
                    {
                        pP = CoordinateTransform.UVToLinearProbeSpace(uv, apertureWidth, maxDepth);
                    }
                    else
                    {
                        pP = CoordinateTransform.UVToCurvilinearProbeSpace(uv, 65f, 0.04f, maxDepth);
                    }

                    // 2. Probe Space -> World Space
                    Vector3 pW = CoordinateTransform.ProbeToWorld(pP, probePos, probeRot);

                    // 3. Sample 3D Volume
                    SampleResult sample = sampler.SampleWorld(pW);

                    // 4. Write pixel (v=0 is probe face at top of ultrasound image, so we map y accordingly)
                    // In ultrasound display standard: row 0 is top (probe face), row (height-1) is deep tissue.
                    // Texture2D coordinate (0,0) is bottom-left. To display probe face at top of RawImage,
                    // we flip the vertical index:
                    int flippedY = (height - 1) - y;
                    outputBuffer.SetPixel(x, flippedY, sample.Intensity);
                }
            }
        }
    }
}
