using System;
using UnityEngine;
using VirtualUltrasound.Core;
using VirtualUltrasound.Volume;

namespace VirtualUltrasound.Rendering
{
    /// <summary>
    /// GPU-accelerated implementation of the two-stage ultrasound slice generation pipeline:
    ///   Stage 1: Polar Acoustic Acquisition (GPU Compute Shader sampling Texture3D -> Polar RenderTexture)
    ///   Stage 2: Cartesian Scan Conversion (GPU Compute Shader interpolating Polar RT -> Display RenderTexture)
    /// Operates entirely in GPU memory with zero CPU readbacks in the steady state.
    /// </summary>
    public class GPUSliceGenerator : IDisposable
    {
        private ComputeShader computeShader;

        private int kernelAcquireCurvilinear = -1;
        private int kernelAcquireLinear = -1;
        private int kernelScanConvertCurvilinear = -1;
        private int kernelScanConvertLinear = -1;
        private int kernelComputeDifference = -1;

        private RenderTexture polarRenderTexture;
        private RenderTexture displayRenderTexture;
        private RenderTexture diffRenderTexture;

        public RenderTexture PolarRenderTexture => polarRenderTexture;
        public RenderTexture DisplayRenderTexture => displayRenderTexture;
        public RenderTexture DiffRenderTexture => diffRenderTexture;

        public GPUSliceGenerator(ComputeShader shader = null)
        {
            SetComputeShader(shader);
        }

        public void SetComputeShader(ComputeShader shader)
        {
            computeShader = shader;
            if (computeShader != null)
            {
                kernelAcquireCurvilinear = computeShader.FindKernel("AcquireCurvilinear");
                kernelAcquireLinear = computeShader.FindKernel("AcquireLinear");
                kernelScanConvertCurvilinear = computeShader.FindKernel("ScanConvertCurvilinear");
                kernelScanConvertLinear = computeShader.FindKernel("ScanConvertLinear");
                kernelComputeDifference = computeShader.FindKernel("ComputeDifference");
            }
        }

        /// <summary>
        /// Ensures render textures match the requested acquisition and display dimensions.
        /// Reallocates GPU resources strictly when dimensions actually change.
        /// </summary>
        public void EnsureResources(int scanLines, int samplesPerScanLine, int sliceWidth, int sliceHeight)
        {
            scanLines = Mathf.Max(8, scanLines);
            samplesPerScanLine = Mathf.Max(8, samplesPerScanLine);
            sliceWidth = Mathf.Max(16, sliceWidth);
            sliceHeight = Mathf.Max(16, sliceHeight);

            // 1. Polar buffer render texture
            if (polarRenderTexture == null || polarRenderTexture.width != scanLines || polarRenderTexture.height != samplesPerScanLine)
            {
                if (polarRenderTexture != null)
                {
                    polarRenderTexture.Release();
                    if (Application.isPlaying) UnityEngine.Object.Destroy(polarRenderTexture);
                    else UnityEngine.Object.DestroyImmediate(polarRenderTexture);
                }

                polarRenderTexture = new RenderTexture(scanLines, samplesPerScanLine, 0, RenderTextureFormat.RFloat)
                {
                    name = "GPU_PolarBuffer_RT",
                    enableRandomWrite = true,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                polarRenderTexture.Create();
            }

            // 2. Display render texture
            if (displayRenderTexture == null || displayRenderTexture.width != sliceWidth || displayRenderTexture.height != sliceHeight)
            {
                if (displayRenderTexture != null)
                {
                    displayRenderTexture.Release();
                    if (Application.isPlaying) UnityEngine.Object.Destroy(displayRenderTexture);
                    else UnityEngine.Object.DestroyImmediate(displayRenderTexture);
                }

                displayRenderTexture = new RenderTexture(sliceWidth, sliceHeight, 0, RenderTextureFormat.ARGB32)
                {
                    name = "GPU_DisplaySlice_RT",
                    enableRandomWrite = true,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                displayRenderTexture.Create();
            }

            // 3. Difference render texture
            if (diffRenderTexture == null || diffRenderTexture.width != sliceWidth || diffRenderTexture.height != sliceHeight)
            {
                if (diffRenderTexture != null)
                {
                    diffRenderTexture.Release();
                    if (Application.isPlaying) UnityEngine.Object.Destroy(diffRenderTexture);
                    else UnityEngine.Object.DestroyImmediate(diffRenderTexture);
                }

                diffRenderTexture = new RenderTexture(sliceWidth, sliceHeight, 0, RenderTextureFormat.ARGB32)
                {
                    name = "GPU_Difference_RT",
                    enableRandomWrite = true,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                diffRenderTexture.Create();
            }
        }

        /// <summary>
        /// Stage 1: Dispatches GPU compute shader to acquire polar acoustic data from Texture3D volume.
        /// </summary>
        public void AcquirePolarDataGPU(
            Vector3 probePos,
            Quaternion probeRot,
            float apertureWidth,
            float maxDepth,
            ProbeType probeType,
            float sectorAngleDeg,
            float apexRadius,
            GPUVolumeData gpuVolume,
            VirtualUltrasound.Volume.SyntheticAnatomyVolume anatomyVolume,
            int scanLines,
            int samplesPerScanLine,
            UltrasoundAppearanceSettings appearance)
        {
            if (computeShader == null || polarRenderTexture == null)
                return;

            int kernel = (probeType == ProbeType.Linear) ? kernelAcquireLinear : kernelAcquireCurvilinear;
            if (kernel < 0) return;

            Matrix4x4 rotMat = Matrix4x4.Rotate(probeRot);

            if (gpuVolume != null && gpuVolume.VolumeTexture != null)
            {
                computeShader.SetTexture(kernel, "_VolumeTexture", gpuVolume.VolumeTexture);
                computeShader.SetVector("_VolumeBoundsMin", gpuVolume.BoundsMin);
                computeShader.SetVector("_VolumeBoundsSize", gpuVolume.BoundsSize);
                Vector3Int volRes = gpuVolume.TextureResolution;
                computeShader.SetVector("_VolumeResolution", new Vector4(volRes.x, volRes.y, volRes.z, 0f));
            }

            computeShader.SetTexture(kernel, "_PolarBufferOutput", polarRenderTexture);

            computeShader.SetInt("_ScanLines", scanLines);
            computeShader.SetInt("_SamplesPerScanLine", samplesPerScanLine);
            computeShader.SetVector("_ProbePos", probePos);
            computeShader.SetMatrix("_ProbeRotMatrix", rotMat);

            computeShader.SetFloat("_ApertureWidth", apertureWidth);
            computeShader.SetFloat("_MaxDepth", maxDepth);
            computeShader.SetFloat("_SectorAngleRad", sectorAngleDeg * Mathf.Deg2Rad);
            computeShader.SetFloat("_ApexRadius", apexRadius);

            // Direct continuous GPU analytical evaluation
            if (anatomyVolume != null)
            {
                computeShader.SetInt("_UseAnalyticalVolume", 1);
                computeShader.SetVector("_BodyCenter", anatomyVolume.BodyCenter);
                computeShader.SetVector("_BodyRadii", anatomyVolume.BodyRadii);
                Quaternion invRot = Quaternion.Inverse(anatomyVolume.transform.rotation);
                computeShader.SetVector("_BodyRotInv", new Vector4(invRot.x, invRot.y, invRot.z, invRot.w));
                computeShader.SetFloat("_BodyIntensity", anatomyVolume.BodyIntensity);
                computeShader.SetFloat("_BodyScattering", anatomyVolume.BodyScattering);

                computeShader.SetVector("_Organ1Center", anatomyVolume.Organ1Center);
                computeShader.SetFloat("_Organ1Radius", anatomyVolume.Organ1Radius);
                computeShader.SetFloat("_Organ1Intensity", anatomyVolume.Organ1Intensity);
                computeShader.SetFloat("_Organ1Scattering", anatomyVolume.Organ1Scattering);

                computeShader.SetVector("_Organ2Center", anatomyVolume.Organ2Center);
                computeShader.SetFloat("_Organ2Radius", anatomyVolume.Organ2Radius);
                computeShader.SetFloat("_Organ2Intensity", anatomyVolume.Organ2Intensity);
                computeShader.SetFloat("_Organ2Scattering", anatomyVolume.Organ2Scattering);

                computeShader.SetVector("_VesselStart", anatomyVolume.VesselStart);
                computeShader.SetVector("_VesselEnd", anatomyVolume.VesselEnd);
                computeShader.SetFloat("_VesselOuterRadius", anatomyVolume.VesselOuterRadius);
                computeShader.SetFloat("_VesselWallThickness", anatomyVolume.VesselWallThickness);
                computeShader.SetFloat("_VesselWallIntensity", anatomyVolume.VesselWallIntensity);
                computeShader.SetFloat("_VesselWallScattering", anatomyVolume.VesselWallScattering);
                computeShader.SetFloat("_VesselLumenIntensity", anatomyVolume.VesselLumenIntensity);
                computeShader.SetFloat("_VesselLumenScattering", anatomyVolume.VesselLumenScattering);
            }
            else
            {
                computeShader.SetInt("_UseAnalyticalVolume", 0);
            }

            // B-Mode Appearance Uniforms (Phase 4)
            computeShader.SetInt("_AppearanceEnabled", appearance.Enabled ? 1 : 0);
            computeShader.SetInt("_DebugView", (int)appearance.DebugView);
            computeShader.SetFloat("_Gain", appearance.Gain);
            computeShader.SetFloat("_BoundaryStrength", appearance.BoundaryStrength);
            computeShader.SetFloat("_SpeckleStrength", appearance.SpeckleStrength);
            computeShader.SetFloat("_SpeckleScale", appearance.SpeckleScale);
            computeShader.SetFloat("_DepthAttenuation", appearance.DepthAttenuation);
            computeShader.SetFloat("_CompressionRatio", appearance.CompressionRatio);

            int threadGroupsX = Mathf.CeilToInt(scanLines / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(samplesPerScanLine / 8.0f);
            computeShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
        }

        /// <summary>
        /// Stage 2: Dispatches GPU compute shader to scan-convert Polar RenderTexture into Cartesian Display RenderTexture.
        /// </summary>
        public void ScanConvertGPU(
            float apertureWidth,
            float maxDepth,
            ProbeType probeType,
            float sectorAngleDeg,
            float apexRadius,
            int scanLines,
            int samplesPerScanLine,
            int sliceWidth,
            int sliceHeight,
            ScanConversionFilterMode filterMode)
        {
            if (computeShader == null || polarRenderTexture == null || displayRenderTexture == null)
                return;

            int kernel = (probeType == ProbeType.Linear) ? kernelScanConvertLinear : kernelScanConvertCurvilinear;
            if (kernel < 0) return;

            CoordinateTransform.GetSectorBoundingDimensions(sectorAngleDeg, apexRadius, maxDepth, out float lateralSpan, out _);
            float effectiveLateralSpan = lateralSpan * 1.06f;

            computeShader.SetTexture(kernel, "_PolarBufferInput", polarRenderTexture);
            computeShader.SetTexture(kernel, "_DisplayOutput", displayRenderTexture);

            computeShader.SetInt("_ScanLines", scanLines);
            computeShader.SetInt("_SamplesPerScanLine", samplesPerScanLine);
            computeShader.SetInt("_SliceWidth", sliceWidth);
            computeShader.SetInt("_SliceHeight", sliceHeight);

            computeShader.SetFloat("_ApertureWidth", apertureWidth);
            computeShader.SetFloat("_MaxDepth", maxDepth);
            computeShader.SetFloat("_SectorAngleRad", sectorAngleDeg * Mathf.Deg2Rad);
            computeShader.SetFloat("_ApexRadius", apexRadius);
            computeShader.SetFloat("_EffectiveLateralSpan", effectiveLateralSpan);
            computeShader.SetInt("_FilterMode", (int)filterMode);

            int threadGroupsX = Mathf.CeilToInt(sliceWidth / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(sliceHeight / 8.0f);
            computeShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
        }

        /// <summary>
        /// Computes visual difference heatmap between two textures on the GPU.
        /// </summary>
        public void ComputeDifferenceGPU(Texture sourceA, Texture sourceB, int sliceWidth, int sliceHeight)
        {
            if (computeShader == null || kernelComputeDifference < 0 || sourceA == null || sourceB == null || diffRenderTexture == null)
                return;

            computeShader.SetTexture(kernelComputeDifference, "_SourceTextureA", sourceA);
            computeShader.SetTexture(kernelComputeDifference, "_SourceTextureB", sourceB);
            computeShader.SetTexture(kernelComputeDifference, "_DiffOutput", diffRenderTexture);
            computeShader.SetInt("_SliceWidth", sliceWidth);
            computeShader.SetInt("_SliceHeight", sliceHeight);

            int threadGroupsX = Mathf.CeilToInt(sliceWidth / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(sliceHeight / 8.0f);
            computeShader.Dispatch(kernelComputeDifference, threadGroupsX, threadGroupsY, 1);
        }

        public void Dispose()
        {
            if (polarRenderTexture != null)
            {
                polarRenderTexture.Release();
                if (Application.isPlaying) UnityEngine.Object.Destroy(polarRenderTexture);
                else UnityEngine.Object.DestroyImmediate(polarRenderTexture);
                polarRenderTexture = null;
            }

            if (displayRenderTexture != null)
            {
                displayRenderTexture.Release();
                if (Application.isPlaying) UnityEngine.Object.Destroy(displayRenderTexture);
                else UnityEngine.Object.DestroyImmediate(displayRenderTexture);
                displayRenderTexture = null;
            }

            if (diffRenderTexture != null)
            {
                diffRenderTexture.Release();
                if (Application.isPlaying) UnityEngine.Object.Destroy(diffRenderTexture);
                else UnityEngine.Object.DestroyImmediate(diffRenderTexture);
                diffRenderTexture = null;
            }
        }
    }
}
