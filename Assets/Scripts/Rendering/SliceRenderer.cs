using System;
using System.Diagnostics;
using UnityEngine;
using VirtualUltrasound.Core;
using VirtualUltrasound.Probe;
using VirtualUltrasound.Volume;

namespace VirtualUltrasound.Rendering
{
    public enum UltrasoundRenderMode
    {
        GPU = 0,
        CPUReference = 1,
        Difference = 2
    }

    /// <summary>
    /// Coordinates the two-stage slice generation pipeline across CPU and GPU backends:
    ///   Stage 1: Polar Acoustic Acquisition (ScanLines x SamplesPerScanLine volume samples)
    ///   Stage 2: Cartesian Scan Conversion (Polar Buffer -> Display Texture / RenderTexture)
    /// Supports dynamic runtime switching between GPU, CPU Reference, and Difference modes.
    /// </summary>
    public class SliceRenderer : MonoBehaviour
    {
        [Header("Pipeline Mode")]
        [SerializeField] private UltrasoundRenderMode renderMode = UltrasoundRenderMode.GPU;

        [Header("B-Mode Appearance (Phase 4)")]
        [SerializeField] private UltrasoundAppearanceSettings appearanceSettings = UltrasoundAppearanceSettings.Default;

        [Header("References")]
        [SerializeField] private ProbeGeometry probeGeometry;
        [SerializeField] private ProceduralVolumeSampler volumeSampler;
        [SerializeField] private SyntheticAnatomyVolume anatomyVolume;
        [SerializeField] private GPUVolumeData gpuVolumeData;
        [SerializeField] private ComputeShader ultrasoundComputeShader;

        [Header("Display Resolution (Output Texture)")]
        [Tooltip("Width of Cartesian output display bitmap (independent of acquisition ray count).")]
        [Range(32, 1024)]
        [SerializeField] private int sliceWidth = 512;
        [Tooltip("Height of Cartesian output display bitmap (independent of acquisition sample count).")]
        [Range(32, 1024)]
        [SerializeField] private int sliceHeight = 512;

        [Header("Filtering Options")]
        [Tooltip("Interpolation algorithm applied during polar-to-Cartesian scan conversion.")]
        [SerializeField] private ScanConversionFilterMode scanConversionFilter = ScanConversionFilterMode.Bilinear;
        [Tooltip("Hardware texture filter applied when displaying texture on UI.")]
        [SerializeField] private FilterMode textureFilterMode = FilterMode.Bilinear;

        // CPU Pipeline state
        private Texture2D sliceTexture;
        private SliceBuffer sliceBuffer;
        private PolarBuffer polarBuffer;
        private CPUSliceGenerator cpuSliceGenerator;

        // GPU Pipeline state
        private GPUSliceGenerator gpuSliceGenerator;

        // Active output reference
        private Texture activeTexture;

        private Stopwatch totalStopwatch = new Stopwatch();
        private Stopwatch stageStopwatch = new Stopwatch();

        public UltrasoundRenderMode RenderMode
        {
            get => renderMode;
            set => renderMode = value;
        }

        public UltrasoundAppearanceSettings AppearanceSettings
        {
            get => appearanceSettings;
            set => appearanceSettings = value;
        }

        public Texture ActiveTexture => activeTexture;
        public Texture2D SliceTexture => sliceTexture;
        public SliceBuffer SliceBuffer => sliceBuffer;
        public PolarBuffer PolarBuffer => polarBuffer;
        public GPUSliceGenerator GPUSliceGenerator => gpuSliceGenerator;

        public float LastRenderTimeMs { get; private set; }
        public float LastAcquisitionTimeMs { get; private set; }
        public float LastScanConvertTimeMs { get; private set; }
        public int LastAcquisitionSamplesCount => (probeGeometry != null) ? (probeGeometry.ScanLines * probeGeometry.SamplesPerScanLine) : 0;

        public float MeanDifference { get; private set; }
        public float MaxDifference { get; private set; }

        public event Action<Texture> OnTextureUpdated;

        public int SliceWidth
        {
            get => sliceWidth;
            set
            {
                if (sliceWidth != value && value > 0)
                {
                    sliceWidth = value;
                    EnsureBuffersAllocated();
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
                    EnsureBuffersAllocated();
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
            FindReferences();

            cpuSliceGenerator = new CPUSliceGenerator();
            gpuSliceGenerator = new GPUSliceGenerator(ultrasoundComputeShader);

            EnsureBuffersAllocated();
        }

        public void FindReferences()
        {
            if (probeGeometry == null) probeGeometry = FindObjectOfType<ProbeGeometry>();
            if (volumeSampler == null) volumeSampler = FindObjectOfType<ProceduralVolumeSampler>();
            if (anatomyVolume == null) anatomyVolume = FindObjectOfType<SyntheticAnatomyVolume>();
            if (gpuVolumeData == null) gpuVolumeData = FindObjectOfType<GPUVolumeData>();
            if (ultrasoundComputeShader == null)
            {
                ultrasoundComputeShader = Resources.Load<ComputeShader>("UltrasoundPipeline");
            }
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                if (gpuSliceGenerator != null && ultrasoundComputeShader != null)
                {
                    gpuSliceGenerator.SetComputeShader(ultrasoundComputeShader);
                }
                EnsureBuffersAllocated();
            }
        }

        public void SetProbeGeometry(ProbeGeometry geometry)
        {
            probeGeometry = geometry;
            EnsureBuffersAllocated();
        }

        public void SetVolumeSampler(ProceduralVolumeSampler sampler)
        {
            volumeSampler = sampler;
        }

        public void SetGPUVolumeData(GPUVolumeData gpuVolume)
        {
            gpuVolumeData = gpuVolume;
        }

        public void SetComputeShader(ComputeShader shader)
        {
            ultrasoundComputeShader = shader;
            if (gpuSliceGenerator != null)
            {
                gpuSliceGenerator.SetComputeShader(shader);
            }
        }

        public void EnsureBuffersAllocated()
        {
            int reqLines = probeGeometry != null ? probeGeometry.ScanLines : 128;
            int reqSamples = probeGeometry != null ? probeGeometry.SamplesPerScanLine : 128;

            // 1. CPU Stage 1 Polar Buffer
            if (polarBuffer == null)
            {
                polarBuffer = new PolarBuffer(reqLines, reqSamples);
            }
            else if (polarBuffer.Lines != reqLines || polarBuffer.Samples != reqSamples)
            {
                polarBuffer.Resize(reqLines, reqSamples);
            }

            // 2. CPU Stage 2 Display SliceBuffer & Texture2D
            if (sliceBuffer == null || sliceBuffer.Width != sliceWidth || sliceBuffer.Height != sliceHeight)
            {
                if (sliceBuffer == null) sliceBuffer = new SliceBuffer(sliceWidth, sliceHeight);
                else sliceBuffer.Resize(sliceWidth, sliceHeight);
            }

            if (sliceTexture == null || sliceTexture.width != sliceWidth || sliceTexture.height != sliceHeight)
            {
                if (sliceTexture != null)
                {
                    if (Application.isPlaying) Destroy(sliceTexture);
                    else DestroyImmediate(sliceTexture);
                }

                sliceTexture = new Texture2D(sliceWidth, sliceHeight, TextureFormat.RGBA32, false)
                {
                    name = "UltrasoundSliceTexture_CPU",
                    filterMode = textureFilterMode,
                    wrapMode = TextureWrapMode.Clamp
                };
            }

            // 3. GPU Resources
            if (gpuSliceGenerator != null)
            {
                gpuSliceGenerator.EnsureResources(reqLines, reqSamples, sliceWidth, sliceHeight);
            }
        }

        private void LateUpdate()
        {
            EnsureBuffersAllocated();
            RenderSlice();
        }

        /// <summary>
        /// Executes the two-stage slice generation pipeline according to the active RenderMode:
        ///   - GPU: Pure compute shader execution directly to Display RenderTexture.
        ///   - CPUReference: Pure CPU reference execution into SliceBuffer / Texture2D.
        ///   - Difference: Executes both and produces difference heatmap texture + error metrics.
        /// </summary>
        public void RenderSlice()
        {
            if (probeGeometry == null) return;

            int lines = probeGeometry.ScanLines;
            int samples = probeGeometry.SamplesPerScanLine;

            totalStopwatch.Restart();

            if (renderMode == UltrasoundRenderMode.CPUReference)
            {
                RenderCPU(lines, samples);
                activeTexture = sliceTexture;
            }
            else if (renderMode == UltrasoundRenderMode.GPU)
            {
                RenderGPU(lines, samples);
                activeTexture = gpuSliceGenerator?.DisplayRenderTexture;
            }
            else // Difference mode
            {
                RenderCPU(lines, samples);
                RenderGPU(lines, samples);

                if (gpuSliceGenerator != null && sliceTexture != null)
                {
                    gpuSliceGenerator.ComputeDifferenceGPU(sliceTexture, gpuSliceGenerator.DisplayRenderTexture, sliceWidth, sliceHeight);
                    activeTexture = gpuSliceGenerator.DiffRenderTexture;
                    CalculateDifferenceMetrics();
                }
            }

            totalStopwatch.Stop();
            LastRenderTimeMs = (float)totalStopwatch.Elapsed.TotalMilliseconds;

            if (activeTexture != null)
            {
                OnTextureUpdated?.Invoke(activeTexture);
            }
        }

        private void RenderCPU(int lines, int samples)
        {
            if (volumeSampler == null || cpuSliceGenerator == null || sliceBuffer == null || polarBuffer == null || sliceTexture == null)
                return;

            stageStopwatch.Restart();
            cpuSliceGenerator.AcquirePolarData(
                probeGeometry.Origin,
                probeGeometry.Orientation,
                probeGeometry.ApertureWidth,
                probeGeometry.MaxDepth,
                probeGeometry.Type,
                probeGeometry.SectorAngleDegrees,
                probeGeometry.ConvexRadius,
                volumeSampler,
                polarBuffer,
                appearanceSettings
            );
            stageStopwatch.Stop();
            LastAcquisitionTimeMs = (float)stageStopwatch.Elapsed.TotalMilliseconds;

            stageStopwatch.Restart();
            cpuSliceGenerator.ScanConvert(
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

            sliceTexture.SetPixels32(sliceBuffer.Pixels);
            sliceTexture.Apply(false);
        }

        private void RenderGPU(int lines, int samples)
        {
            if (gpuVolumeData == null || gpuSliceGenerator == null)
            {
                FindReferences();
                if (gpuVolumeData == null || gpuSliceGenerator == null) return;
            }

            if (gpuVolumeData.VolumeTexture == null)
            {
                gpuVolumeData.BakeFromSource();
            }

            stageStopwatch.Restart();
            gpuSliceGenerator.AcquirePolarDataGPU(
                probeGeometry.Origin,
                probeGeometry.Orientation,
                probeGeometry.ApertureWidth,
                probeGeometry.MaxDepth,
                probeGeometry.Type,
                probeGeometry.SectorAngleDegrees,
                probeGeometry.ConvexRadius,
                gpuVolumeData,
                anatomyVolume,
                lines,
                samples,
                appearanceSettings
            );
            stageStopwatch.Stop();
            LastAcquisitionTimeMs = (float)stageStopwatch.Elapsed.TotalMilliseconds;

            stageStopwatch.Restart();
            gpuSliceGenerator.ScanConvertGPU(
                probeGeometry.ApertureWidth,
                probeGeometry.MaxDepth,
                probeGeometry.Type,
                probeGeometry.SectorAngleDegrees,
                probeGeometry.ConvexRadius,
                lines,
                samples,
                sliceWidth,
                sliceHeight,
                scanConversionFilter
            );
            stageStopwatch.Stop();
            LastScanConvertTimeMs = (float)stageStopwatch.Elapsed.TotalMilliseconds;
        }

        public void CycleDebugView()
        {
            appearanceSettings.DebugView = (AppearanceDebugView)(((int)appearanceSettings.DebugView + 1) % 5);
        }

        private void CalculateDifferenceMetrics()
        {
            // Samples pixel differences when comparison is active
            if (sliceBuffer == null || gpuSliceGenerator == null || gpuSliceGenerator.DisplayRenderTexture == null)
                return;

            RenderTexture activeRT = RenderTexture.active;
            RenderTexture.active = gpuSliceGenerator.DisplayRenderTexture;

            Texture2D tempGPU = new Texture2D(sliceWidth, sliceHeight, TextureFormat.RGBA32, false);
            tempGPU.ReadPixels(new Rect(0, 0, sliceWidth, sliceHeight), 0, 0);
            tempGPU.Apply();
            RenderTexture.active = activeRT;

            Color32[] gpuPixels = tempGPU.GetPixels32();
            Color32[] cpuPixels = sliceBuffer.Pixels;

            float maxDiff = 0f;
            float sumDiff = 0f;
            int count = gpuPixels.Length;

            for (int i = 0; i < count; i++)
            {
                float diff = Mathf.Abs((gpuPixels[i].r - cpuPixels[i].r) / 255.0f);
                if (diff > maxDiff) maxDiff = diff;
                sumDiff += diff;
            }

            MaxDifference = maxDiff;
            MeanDifference = count > 0 ? (sumDiff / count) : 0f;

            if (Application.isPlaying) Destroy(tempGPU);
            else DestroyImmediate(tempGPU);
        }

        public void ToggleRenderMode()
        {
            renderMode = (UltrasoundRenderMode)(((int)renderMode + 1) % 3);
        }

        private void OnDestroy()
        {
            if (sliceTexture != null)
            {
                if (Application.isPlaying) Destroy(sliceTexture);
                else DestroyImmediate(sliceTexture);
            }

            gpuSliceGenerator?.Dispose();
        }
    }
}
