using System;
using UnityEngine;
using VirtualUltrasound.Core;

namespace VirtualUltrasound.Rendering
{
    /// <summary>
    /// CPU reference implementation of the two-stage ultrasound slice generation pipeline:
    ///   Stage 1: Polar Acoustic Acquisition (ScanLines x SamplesPerScanLine 3D volume queries)
    ///            Optional B-mode acoustic appearance (Boundary gradient, 3D coherent speckle, attenuation, gain, compression)
    ///   Stage 2: Cartesian Scan Conversion (PolarBuffer -> SliceBuffer / Texture2D)
    /// Provides deterministic, analytically verified volume sampling without GPU dependencies.
    /// </summary>
    public class CPUSliceGenerator : ISliceGenerator
    {
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
            UltrasoundAppearanceSettings defaultSettings = new UltrasoundAppearanceSettings
            {
                Enabled = false,
                DebugView = AppearanceDebugView.RawAnatomical
            };
            AcquirePolarData(probePos, probeRot, apertureWidth, maxDepth, probeType, sectorAngleDeg, apexRadius, sampler, polarBuffer, defaultSettings);
        }

        public void AcquirePolarData(
            Vector3 probePos,
            Quaternion probeRot,
            float apertureWidth,
            float maxDepth,
            ProbeType probeType,
            float sectorAngleDeg,
            float apexRadius,
            IVolumeSampler sampler,
            PolarBuffer polarBuffer,
            UltrasoundAppearanceSettings appearance)
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
                        float zAxial = v * maxDepth;

                        Vector3 pP = new Vector3(xP, 0f, zAxial);
                        Vector3 pW = CoordinateTransform.ProbeToWorld(pP, probePos, probeRot);

                        SampleResult result = sampler.SampleWorld(pW);
                        float signal = FormSignal(result, pW, zAxial, sampler, appearance);
                        polarBuffer.SetSample(i, j, signal);
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
                        float zAxial = v * maxDepth;
                        float r = apexRadius + zAxial;

                        float xP = r * sinA;
                        float zP = (r * cosA) - apexRadius;

                        Vector3 pP = new Vector3(xP, 0f, zP);
                        Vector3 pW = CoordinateTransform.ProbeToWorld(pP, probePos, probeRot);

                        SampleResult result = sampler.SampleWorld(pW);
                        float signal = FormSignal(result, pW, zAxial, sampler, appearance);
                        polarBuffer.SetSample(i, j, signal);
                    }
                }
            }
        }

        private float FormSignal(SampleResult sample, Vector3 pW, float zAxial, IVolumeSampler sampler, UltrasoundAppearanceSettings app)
        {
            if (!app.Enabled || app.DebugView == AppearanceDebugView.RawAnatomical)
            {
                return sample.Intensity;
            }

            // 1. 3D Spatial Gradient (Boundary/Interface detector)
            float h = 0.002f; // 2mm sample step
            float dx = (sampler.SampleWorld(pW + new Vector3(h, 0f, 0f)).Intensity - sampler.SampleWorld(pW - new Vector3(h, 0f, 0f)).Intensity) / (2f * h);
            float dy = (sampler.SampleWorld(pW + new Vector3(0f, h, 0f)).Intensity - sampler.SampleWorld(pW - new Vector3(0f, h, 0f)).Intensity) / (2f * h);
            float dz = (sampler.SampleWorld(pW + new Vector3(0f, 0f, h)).Intensity - sampler.SampleWorld(pW - new Vector3(0f, 0f, h)).Intensity) / (2f * h);
            float gradMagnitude = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
            float boundaryEcho = gradMagnitude * 0.015f * app.BoundaryStrength;

            // 2. 3D Spatially Coherent Speckle Noise & Tissue Scattering
            float noise = CoherentNoise3D(pW, app.SpeckleScale);
            float speckleScatter = sample.Scattering * Mathf.Max(0.0f, 1.0f + (app.SpeckleStrength * noise));

            // Debug view inspection bypasses
            if (app.DebugView == AppearanceDebugView.BoundaryResponse) return Mathf.Clamp01(boundaryEcho * app.Gain);
            if (app.DebugView == AppearanceDebugView.SpeckleScattering) return Mathf.Clamp01(speckleScatter * app.Gain);

            // 3. Combined Raw Echo
            float rawEcho = boundaryEcho + speckleScatter;

            // 4. Depth Attenuation along axial acoustic depth
            float attenuationFactor = MathF.Exp(-app.DepthAttenuation * zAxial);
            float attenuatedEcho = rawEcho * attenuationFactor;

            // 5. System Gain
            float gainedEcho = attenuatedEcho * app.Gain;

            // 6. Dynamic Range Compression (Logarithmic response)
            float comp = Mathf.Max(1.0f, app.CompressionRatio);
            float compressed = MathF.Log(1.0f + (comp * gainedEcho)) / MathF.Log(1.0f + comp);

            return Mathf.Clamp01(compressed);
        }

        // 3D Deterministic Spatial Noise Function
        public static float Hash31(Vector3 p)
        {
            float px = p.x * 0.1031f;
            float py = p.y * 0.1031f;
            float pz = p.z * 0.1031f;
            px = px - MathF.Floor(px);
            py = py - MathF.Floor(py);
            pz = pz - MathF.Floor(pz);

            float d = px * (pz + 31.32f) + py * (py + 31.32f) + pz * (px + 31.32f);
            px += d; py += d; pz += d;
            float res = (px + py) * pz;
            return res - MathF.Floor(res);
        }

        public static float CoherentNoise3D(Vector3 pos, float scale)
        {
            Vector3 p = pos * scale;
            Vector3 i = new Vector3(MathF.Floor(p.x), MathF.Floor(p.y), MathF.Floor(p.z));
            Vector3 f = new Vector3(p.x - i.x, p.y - i.y, p.z - i.z);
            Vector3 u = new Vector3(f.x * f.x * (3f - 2f * f.x), f.y * f.y * (3f - 2f * f.y), f.z * f.z * (3f - 2f * f.z));

            float n000 = Hash31(i + new Vector3(0f, 0f, 0f));
            float n100 = Hash31(i + new Vector3(1f, 0f, 0f));
            float n010 = Hash31(i + new Vector3(0f, 1f, 0f));
            float n110 = Hash31(i + new Vector3(1f, 1f, 0f));
            float n001 = Hash31(i + new Vector3(0f, 0f, 1f));
            float n101 = Hash31(i + new Vector3(1f, 0f, 1f));
            float n011 = Hash31(i + new Vector3(0f, 1f, 1f));
            float n111 = Hash31(i + new Vector3(1f, 1f, 1f));

            float nx00 = Mathf.Lerp(n000, n100, u.x);
            float nx10 = Mathf.Lerp(n010, n110, u.x);
            float nx01 = Mathf.Lerp(n001, n101, u.x);
            float nx11 = Mathf.Lerp(n011, n111, u.x);

            float nxy0 = Mathf.Lerp(nx00, nx10, u.y);
            float nxy1 = Mathf.Lerp(nx01, nx11, u.y);

            float val = Mathf.Lerp(nxy0, nxy1, u.z);
            return (val * 2.0f) - 1.0f;
        }

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
            UltrasoundAppearanceSettings appearance,
            ScanConversionFilterMode filterMode = ScanConversionFilterMode.Bilinear)
        {
            AcquirePolarData(probePos, probeRot, apertureWidth, maxDepth, probeType, sectorAngleDeg, apexRadius, sampler, polarBuffer, appearance);
            ScanConvert(apertureWidth, maxDepth, probeType, sectorAngleDeg, apexRadius, polarBuffer, outputBuffer, filterMode);
        }
    }
}
