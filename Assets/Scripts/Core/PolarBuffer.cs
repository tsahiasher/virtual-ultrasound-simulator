using System;
using UnityEngine;

namespace VirtualUltrasound.Core
{
    /// <summary>
    /// Pre-allocated raw acoustic buffer storing polar ray samples (ScanLines x SamplesPerScanLine).
    /// Decouples 3D volume acquisition from 2D Cartesian display rasterization.
    /// Optimized for zero per-frame heap allocations.
    /// </summary>
    public class PolarBuffer
    {
        private float[] intensities;
        private int lines;
        private int samples;

        public int Lines => lines;
        public int Samples => samples;
        public int TotalSamples => lines * samples;
        public float[] Intensities => intensities;

        public PolarBuffer(int initialLines, int initialSamples)
        {
            lines = Math.Max(1, initialLines);
            samples = Math.Max(1, initialSamples);
            intensities = new float[lines * samples];
        }

        public void Resize(int newLines, int newSamples)
        {
            newLines = Math.Max(1, newLines);
            newSamples = Math.Max(1, newSamples);

            if (lines == newLines && samples == newSamples && intensities != null)
            {
                return;
            }

            lines = newLines;
            samples = newSamples;
            int requiredCount = lines * samples;

            if (intensities == null || intensities.Length < requiredCount)
            {
                intensities = new float[requiredCount];
            }
        }

        public void SetSample(int lineIndex, int sampleIndex, float intensity)
        {
            if (lineIndex >= 0 && lineIndex < lines && sampleIndex >= 0 && sampleIndex < samples)
            {
                intensities[sampleIndex * lines + lineIndex] = intensity;
            }
        }

        public float GetSample(int lineIndex, int sampleIndex)
        {
            if (lineIndex >= 0 && lineIndex < lines && sampleIndex >= 0 && sampleIndex < samples)
            {
                return intensities[sampleIndex * lines + lineIndex];
            }
            return 0f;
        }

        public void Clear()
        {
            if (intensities != null)
            {
                Array.Clear(intensities, 0, lines * samples);
            }
        }

        /// <summary>
        /// Samples the polar buffer at normalized coordinates (u in [0..1] along lines, v in [0..1] along depth)
        /// using continuous bilinear interpolation.
        /// </summary>
        public float SampleBilinear(float normalizedLine, float normalizedDepth)
        {
            float uClamped = Mathf.Clamp01(normalizedLine);
            float vClamped = Mathf.Clamp01(normalizedDepth);

            float linePos = uClamped * (lines - 1);
            float depthPos = vClamped * (samples - 1);

            int i0 = (int)linePos;
            int j0 = (int)depthPos;
            int i1 = Math.Min(i0 + 1, lines - 1);
            int j1 = Math.Min(j0 + 1, samples - 1);

            float fu = linePos - i0;
            float fv = depthPos - j0;

            float v00 = intensities[j0 * lines + i0];
            float v10 = intensities[j0 * lines + i1];
            float v01 = intensities[j1 * lines + i0];
            float v11 = intensities[j1 * lines + i1];

            float top = v00 + (v10 - v00) * fu;
            float bottom = v01 + (v11 - v01) * fu;

            return top + (bottom - top) * fv;
        }

        /// <summary>
        /// Samples the polar buffer using nearest-neighbor discrete lookup (useful for verifying scanline discretization).
        /// </summary>
        public float SampleNearest(float normalizedLine, float normalizedDepth)
        {
            float uClamped = Mathf.Clamp01(normalizedLine);
            float vClamped = Mathf.Clamp01(normalizedDepth);

            int i = Math.Clamp((int)Math.Round(uClamped * (lines - 1)), 0, lines - 1);
            int j = Math.Clamp((int)Math.Round(vClamped * (samples - 1)), 0, samples - 1);

            return intensities[j * lines + i];
        }
    }
}
