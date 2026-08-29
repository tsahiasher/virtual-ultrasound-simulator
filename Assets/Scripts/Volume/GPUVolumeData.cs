using System;
using UnityEngine;
using VirtualUltrasound.Core;

namespace VirtualUltrasound.Volume
{
    /// <summary>
    /// Manages the 3D GPU volumetric texture representation (Texture3D) of the anatomical data.
    /// Bakes scalar intensity values from IVolumeData once on startup or when anatomy properties change.
    /// Provides physical world-to-volume bounding box transformations to map world coordinates (x_W, y_W, z_W)
    /// into normalized Texture3D UVW coordinates in [0, 1]^3.
    /// </summary>
    public class GPUVolumeData : MonoBehaviour, IDisposable
    {
        [Header("Volume Texture Resolution")]
        [Tooltip("Resolution along X, Y, Z axes for the baked 3D volumetric texture.")]
        [SerializeField] private Vector3Int textureResolution = new Vector3Int(128, 128, 128);

        [Header("Filtering")]
        [SerializeField] private FilterMode filterMode = FilterMode.Trilinear;

        [Header("Data Source")]
        [SerializeField] private SyntheticAnatomyVolume sourceVolume;

        private Texture3D volumeTexture;
        private Vector3 boundsMin;
        private Vector3 boundsMax;
        private Vector3 boundsSize;
        private bool isDirty = true;

        public Texture3D VolumeTexture => volumeTexture;
        public Vector3Int TextureResolution
        {
            get => textureResolution;
            set
            {
                if (textureResolution != value)
                {
                    textureResolution = value;
                    isDirty = true;
                    if (Application.isPlaying) BakeFromSource();
                }
            }
        }
        public Vector3 BoundsMin => boundsMin;
        public Vector3 BoundsMax => boundsMax;
        public Vector3 BoundsSize => boundsSize;

        private void Awake()
        {
            if (sourceVolume == null) sourceVolume = GetComponent<SyntheticAnatomyVolume>() ?? FindObjectOfType<SyntheticAnatomyVolume>();
            EnsureTextureAllocated();
        }

        private void Start()
        {
            if (isDirty || volumeTexture == null)
            {
                BakeFromSource();
            }
        }

        private void OnValidate()
        {
            textureResolution.x = Mathf.Clamp(textureResolution.x, 16, 512);
            textureResolution.y = Mathf.Clamp(textureResolution.y, 16, 512);
            textureResolution.z = Mathf.Clamp(textureResolution.z, 16, 512);

            if (Application.isPlaying)
            {
                isDirty = true;
                BakeFromSource();
            }
        }

        public void SetSourceVolume(SyntheticAnatomyVolume volume)
        {
            sourceVolume = volume;
            isDirty = true;
            BakeFromSource();
        }

        public void MarkDirty()
        {
            isDirty = true;
        }

        /// <summary>
        /// Allocates the 3D texture if not already allocated or if resolution changed.
        /// </summary>
        public void EnsureTextureAllocated()
        {
            int rx = Mathf.Max(16, textureResolution.x);
            int ry = Mathf.Max(16, textureResolution.y);
            int rz = Mathf.Max(16, textureResolution.z);

            if (volumeTexture == null || volumeTexture.width != rx || volumeTexture.height != ry || volumeTexture.depth != rz)
            {
                if (volumeTexture != null)
                {
                    if (Application.isPlaying) Destroy(volumeTexture);
                    else DestroyImmediate(volumeTexture);
                }

                // RGBAHalf (16-bit float per channel): R = Base Intensity / Density, G = Material Backscattering
                volumeTexture = new Texture3D(rx, ry, rz, TextureFormat.RGBAHalf, false)
                {
                    name = "GPU_VolumeData_Texture3D",
                    filterMode = filterMode,
                    wrapMode = TextureWrapMode.Clamp
                };

                isDirty = true;
            }
        }

        /// <summary>
        /// Bakes scalar intensities and scattering properties from the CPU IVolumeData source into the 3D texture.
        /// Updates the world bounding box so shader coordinate transforms are exact.
        /// </summary>
        public void BakeFromSource()
        {
            if (sourceVolume == null)
            {
                sourceVolume = GetComponent<SyntheticAnatomyVolume>() ?? FindObjectOfType<SyntheticAnatomyVolume>();
                if (sourceVolume == null) return;
            }

            EnsureTextureAllocated();

            // Calculate world bounding box
            VolumeBounds wb = sourceVolume.WorldBounds;
            boundsMin = wb.Min;
            boundsMax = wb.Max;
            boundsSize = wb.Size;

            int rx = volumeTexture.width;
            int ry = volumeTexture.height;
            int rz = volumeTexture.depth;
            int totalVoxels = rx * ry * rz;

            Color[] colors = new Color[totalVoxels];

            float invRx = rx > 1 ? 1.0f / (rx - 1) : 0f;
            float invRy = ry > 1 ? 1.0f / (ry - 1) : 0f;
            float invRz = rz > 1 ? 1.0f / (rz - 1) : 0f;

            float stepX = (boundsMax.x - boundsMin.x) * invRx * 0.25f;
            float stepY = (boundsMax.y - boundsMin.y) * invRy * 0.25f;
            float stepZ = (boundsMax.z - boundsMin.z) * invRz * 0.25f;

            int idx = 0;
            for (int z = 0; z < rz; z++)
            {
                float wz = Mathf.Lerp(boundsMin.z, boundsMax.z, z * invRz);
                for (int y = 0; y < ry; y++)
                {
                    float wy = Mathf.Lerp(boundsMin.y, boundsMax.y, y * invRy);
                    for (int x = 0; x < rx; x++)
                    {
                        float wx = Mathf.Lerp(boundsMin.x, boundsMax.x, x * invRx);
                        Vector3 worldPos = new Vector3(wx, wy, wz);

                        SampleResult sample = sourceVolume.EvaluateSample(worldPos);
                        colors[idx++] = new Color(sample.Intensity, sample.Scattering, 0f, 1f);
                    }
                }
            }

            volumeTexture.SetPixels(colors);
            volumeTexture.Apply(false, false);
            isDirty = false;
        }

        /// <summary>
        /// Converts a continuous 3D world position into normalized Texture3D UVW coordinates in [0, 1]^3.
        /// </summary>
        public Vector3 WorldToVolumeUVW(Vector3 worldPos)
        {
            float u = boundsSize.x > 1e-5f ? (worldPos.x - boundsMin.x) / boundsSize.x : 0.5f;
            float v = boundsSize.y > 1e-5f ? (worldPos.y - boundsMin.y) / boundsSize.y : 0.5f;
            float w = boundsSize.z > 1e-5f ? (worldPos.z - boundsMin.z) / boundsSize.z : 0.5f;
            return new Vector3(u, v, w);
        }

        /// <summary>
        /// Converts normalized Texture3D UVW coordinates back into 3D world position.
        /// </summary>
        public Vector3 VolumeUVWToWorld(Vector3 uvw)
        {
            return new Vector3(
                boundsMin.x + (uvw.x * boundsSize.x),
                boundsMin.y + (uvw.y * boundsSize.y),
                boundsMin.z + (uvw.z * boundsSize.z)
            );
        }

        public void Dispose()
        {
            if (volumeTexture != null)
            {
                if (Application.isPlaying) Destroy(volumeTexture);
                else DestroyImmediate(volumeTexture);
                volumeTexture = null;
            }
        }

        private void OnDestroy()
        {
            Dispose();
        }
    }
}
