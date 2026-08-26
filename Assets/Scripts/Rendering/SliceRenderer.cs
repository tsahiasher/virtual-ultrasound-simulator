using System;
using System.Diagnostics;
using UnityEngine;
using VirtualUltrasound.Core;
using VirtualUltrasound.Probe;
using VirtualUltrasound.Volume;

namespace VirtualUltrasound.Rendering
{
    /// <summary>
    /// Coordinates slice generation between probe pose and volume sampler, managing the Texture2D output.
    /// </summary>
    public class SliceRenderer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ProbeGeometry probeGeometry;
        [SerializeField] private ProceduralVolumeSampler volumeSampler;

        [Header("Sampling Resolution")]
        [SerializeField] private int sliceWidth = 128;
        [SerializeField] private int sliceHeight = 128;

        [Header("Filtering")]
        [SerializeField] private FilterMode textureFilterMode = FilterMode.Bilinear;

        private Texture2D sliceTexture;
        private SliceBuffer sliceBuffer;
        private ISliceGenerator sliceGenerator;
        private Stopwatch stopwatch = new Stopwatch();

        public Texture2D SliceTexture => sliceTexture;
        public SliceBuffer SliceBuffer => sliceBuffer;
        public float LastRenderTimeMs { get; private set; }

        public event Action<Texture2D> OnTextureUpdated;

        public int SliceWidth
        {
            get => sliceWidth;
            set
            {
                if (sliceWidth != value && value > 0)
                {
                    sliceWidth = value;
                    RecreateTextureAndBuffer();
                }
            }
        }

        public int SliceHeight
        {
            get => sliceHeight;
            set
            {
                if (sliceHeight != value && value > 0)
                {
                    sliceHeight = value;
                    RecreateTextureAndBuffer();
                }
            }
        }

        private void Awake()
        {
            if (probeGeometry == null) probeGeometry = FindObjectOfType<ProbeGeometry>();
            if (volumeSampler == null) volumeSampler = FindObjectOfType<ProceduralVolumeSampler>();

            sliceGenerator = new CPUSliceGenerator();
            RecreateTextureAndBuffer();
        }

        public void SetGenerator(ISliceGenerator generator)
        {
            sliceGenerator = generator ?? new CPUSliceGenerator();
        }

        public void SetVolumeSampler(ProceduralVolumeSampler sampler)
        {
            volumeSampler = sampler;
        }

        public void SetProbeGeometry(ProbeGeometry geometry)
        {
            probeGeometry = geometry;
        }

        public void RecreateTextureAndBuffer()
        {
            if (sliceBuffer == null)
            {
                sliceBuffer = new SliceBuffer(sliceWidth, sliceHeight);
            }
            else
            {
                sliceBuffer.Resize(sliceWidth, sliceHeight);
            }

            if (sliceTexture != null)
            {
                if (Application.isPlaying) Destroy(sliceTexture);
                else DestroyImmediate(sliceTexture);
            }

            sliceTexture = new Texture2D(sliceWidth, sliceHeight, TextureFormat.RGBA32, false)
            {
                name = "UltrasoundSliceTexture",
                filterMode = textureFilterMode,
                wrapMode = TextureWrapMode.Clamp
            };

            RenderSlice();
        }

        private void LateUpdate()
        {
            RenderSlice();
        }

        /// <summary>
        /// Renders a new 2D ultrasound slice synchronously and updates the texture.
        /// </summary>
        public void RenderSlice()
        {
            if (probeGeometry == null || volumeSampler == null || sliceGenerator == null || sliceBuffer == null || sliceTexture == null)
            {
                return;
            }

            stopwatch.Restart();

            sliceGenerator.GenerateSlice(
                probeGeometry.Origin,
                probeGeometry.Orientation,
                probeGeometry.ApertureWidth,
                probeGeometry.MaxDepth,
                probeGeometry.Type,
                volumeSampler,
                sliceBuffer
            );

            // Upload pixel buffer to GPU texture
            sliceTexture.SetPixels32(sliceBuffer.Pixels);
            sliceTexture.Apply(false);

            stopwatch.Stop();
            LastRenderTimeMs = (float)stopwatch.Elapsed.TotalMilliseconds;

            OnTextureUpdated?.Invoke(sliceTexture);
        }

        private void OnDestroy()
        {
            if (sliceTexture != null)
            {
                if (Application.isPlaying) Destroy(sliceTexture);
                else DestroyImmediate(sliceTexture);
            }
        }
    }
}
