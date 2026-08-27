using System;
using System.Diagnostics;
using UnityEngine;
using VirtualUltrasound.Core;
using VirtualUltrasound.Probe;
using VirtualUltrasound.Volume;

namespace VirtualUltrasound.Rendering
{
    /// <summary>
    /// Coordinates the two-stage slice generation pipeline between probe pose and volume sampler:
    ///   Stage 1: Polar Acoustic Acquisition (ScanLines x SamplesPerScanLine volume samples)
    ///   Stage 2: Cartesian Scan Conversion (PolarBuffer -> SliceBuffer / Texture2D)
    /// Manages independent acquisition and display buffers with dynamic runtime resizing.
    /// </summary>
    public class SliceRenderer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ProbeGeometry probeGeometry;
        [SerializeField] private ProceduralVolumeSampler volumeSampler;

        [Header("Display Resolution (Output Texture)")]
        [Tooltip("Width of Cartesian output display bitmap (independent of acquisition ray count).")]
        [Range(32, 1024)]
        [SerializeField] private int sliceWidth = 256;
        [Tooltip("Height of Cartesian output display bitmap (independent of acquisition sample count).")]
        [Range(32, 1024)]
        [SerializeField] private int sliceHeight = 256;

        [Header("Filtering Options")]
        [Tooltip("Interpolation algorithm applied during polar-to-Cartesian scan conversion.")]
        [SerializeField] private ScanConversionFilterMode scanConversionFilter = ScanConversionFilterMode.Bilinear;
        [Tooltip("Hardware texture filter applied when displaying texture on UI.")]
        [SerializeField] private FilterMode textureFilterMode = FilterMode.Bilinear;

        private Texture2D sliceTexture;
        private SliceBuffer sliceBuffer;
        private PolarBuffer polarBuffer;
        private ISliceGenerator sliceGenerator;

        private Stopwatch totalStopwatch = new Stopwatch();
        private Stopwatch stageStopwatch = new Stopwatch();

        public Texture2D SliceTexture => sliceTexture;
        public SliceBuffer SliceBuffer => sliceBuffer;
        public PolarBuffer PolarBuffer => polarBuffer;

        public float LastRenderTimeMs { get; private set; }
        public float LastAcquisitionTimeMs { get; private set; }
        public float LastScanConvertTimeMs { get; private set; }
        public int LastAcquisitionSamplesCount => polarBuffer != null ? polarBuffer.TotalSamples : 0;

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

        public ScanConversionFilterMode ScanConversionFilter
        {
            get => scanConversionFilter;
            set => scanConversionFilter = value;
        }

        private void Awake()
        {
            if (probeGeometry == null) probeGeometry = FindObjectOfType<ProbeGeometry>();
            if (volumeSampler == null) volumeSampler = FindObjectOfType<ProceduralVolumeSampler>();

            sliceGenerator = new CPUSliceGenerator();
            EnsureBuffersAllocated();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                EnsureBuffersAllocated();
            }
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
            EnsureBuffersAllocated();
        }

        public void EnsureBuffersAllocated()
        {
            int reqLines = probeGeometry != null ? probeGeometry.ScanLines : 128;
            int reqSamples = probeGeometry != null ? probeGeometry.SamplesPerScanLine : 128;

            // 1. Stage 1 Polar Buffer
            if (polarBuffer == null)
            {
                polarBuffer = new PolarBuffer(reqLines, reqSamples);
            }
            else if (polarBuffer.Lines != reqLines || polarBuffer.Samples != reqSamples)
            {
                polarBuffer.Resize(reqLines, reqSamples);
            }

            // 2. Stage 2 Display SliceBuffer & Texture2D
            if (sliceBuffer == null || sliceBuffer.Width != sliceWidth || sliceBuffer.Height != sliceHeight || sliceTexture == null)
            {
                RecreateTextureAndBuffer();
            }
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
            EnsureBuffersAllocated();
            RenderSlice();
        }

        /// <summary>
        /// Executes the two-stage slice generation pipeline synchronously:
        ///   1. Stage 1 Polar Acoustic Acquisition (queries 3D volume at ScanLines x SamplesPerScanLine points)
        ///   2. Stage 2 Cartesian Scan Conversion (interpolates PolarBuffer into SliceBuffer)
        /// </summary>
        public void RenderSlice()
        {
            if (probeGeometry == null || volumeSampler == null || sliceGenerator == null || sliceBuffer == null || sliceTexture == null || polarBuffer == null)
            {
                return;
            }

            totalStopwatch.Restart();

            // --- STAGE 1: Polar Acoustic Acquisition ---
            stageStopwatch.Restart();
            sliceGenerator.AcquirePolarData(
                probeGeometry.Origin,
                probeGeometry.Orientation,
                probeGeometry.ApertureWidth,
                probeGeometry.MaxDepth,
                probeGeometry.Type,
                probeGeometry.SectorAngleDegrees,
                probeGeometry.ConvexRadius,
                volumeSampler,
                polarBuffer
            );
            stageStopwatch.Stop();
            LastAcquisitionTimeMs = (float)stageStopwatch.Elapsed.TotalMilliseconds;

            // --- STAGE 2: Cartesian Scan Conversion ---
            stageStopwatch.Restart();
            sliceGenerator.ScanConvert(
                probeGeometry.ApertureWidth,
                probeGeometry.MaxDepth,
                probeGeometry.Type,
                probeGeometry.SectorAngleDegrees,
                probeGeometry.ConvexRadius,
                polarBuffer,
                sliceBuffer,
                scanConversionFilter
            );
            stageStopwatch.Stop();
            LastScanConvertTimeMs = (float)stageStopwatch.Elapsed.TotalMilliseconds;

            // Upload pixel buffer to GPU texture
            sliceTexture.SetPixels32(sliceBuffer.Pixels);
            sliceTexture.Apply(false);

            totalStopwatch.Stop();
            LastRenderTimeMs = (float)totalStopwatch.Elapsed.TotalMilliseconds;

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
