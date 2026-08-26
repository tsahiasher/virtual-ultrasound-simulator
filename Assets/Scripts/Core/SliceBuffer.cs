using System;
using UnityEngine;

namespace VirtualUltrasound.Core
{
    /// <summary>
    /// Reusable memory buffer holding 2D slice pixel data (intensities and Color32 pixels).
    /// Avoids per-frame GC allocations during continuous probe movement and rendering.
    /// </summary>
    public sealed class SliceBuffer
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int TotalPixels => Width * Height;

        /// <summary>
        /// Raw normalized scalar intensities [0..1] for each pixel.
        /// </summary>
        public float[] Intensities { get; private set; }

        /// <summary>
        /// 32-bit RGBA pixel array ready for direct upload to Texture2D.SetPixels32().
        /// </summary>
        public Color32[] Pixels { get; private set; }

        public SliceBuffer(int width, int height)
        {
            Resize(width, height);
        }

        public void Resize(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException("Slice dimensions must be positive.");

            if (Width == width && Height == height && Pixels != null)
                return;

            Width = width;
            Height = height;
            int total = width * height;
            Intensities = new float[total];
            Pixels = new Color32[total];
        }

        /// <summary>
        /// Clears the entire buffer to black (background/empty).
        /// </summary>
        public void Clear()
        {
            Array.Clear(Intensities, 0, Intensities.Length);
            Color32 black = new Color32(0, 0, 0, 255);
            for (int i = 0; i < Pixels.Length; i++)
            {
                Pixels[i] = black;
            }
        }

        /// <summary>
        /// Sets a pixel at (x, y) with a normalized grayscale intensity.
        /// </summary>
        public void SetPixel(int x, int y, float intensity)
        {
            int index = y * Width + x;
            float clamped = Mathf.Clamp01(intensity);
            Intensities[index] = clamped;

            byte gray = (byte)(clamped * 255f + 0.5f);
            Pixels[index] = new Color32(gray, gray, gray, 255);
        }
    }
}
