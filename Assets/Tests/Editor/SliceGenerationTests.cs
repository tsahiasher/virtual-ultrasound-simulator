using NUnit.Framework;
using UnityEngine;
using VirtualUltrasound.Core;
using VirtualUltrasound.Rendering;
using VirtualUltrasound.Volume;

namespace VirtualUltrasound.Tests
{
    [TestFixture]
    public class SliceGenerationTests
    {
        private GameObject host;
        private SyntheticAnatomyVolume volume;
        private ProceduralVolumeSampler sampler;
        private CPUSliceGenerator generator;
        private PolarBuffer polarBuffer;
        private SliceBuffer displayBuffer;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("SliceTestHost");
            volume = host.AddComponent<SyntheticAnatomyVolume>();
            sampler = host.AddComponent<ProceduralVolumeSampler>();
            sampler.AnatomyVolume = volume;
            generator = new CPUSliceGenerator();
            polarBuffer = new PolarBuffer(64, 64);
            displayBuffer = new SliceBuffer(64, 64);
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void OutsideProbe_GeneratesEmptySlice()
        {
            // Probe placed far outside anatomy pointing away
            Vector3 outsidePos = new Vector3(5f, 5f, 5f);
            Quaternion rot = Quaternion.identity;

            generator.GenerateSlice(outsidePos, rot, 0.05f, 0.10f, ProbeType.Linear, 65f, 0.04f, sampler, polarBuffer, displayBuffer);

            for (int i = 0; i < displayBuffer.TotalPixels; i++)
            {
                Assert.AreEqual(0f, displayBuffer.Intensities[i]);
                Assert.AreEqual(0, displayBuffer.Pixels[i].r);
            }
        }

        [Test]
        public void InsideBody_LinearProbe_GeneratesNonEmptySlice()
        {
            // Place probe at top of body pointing down (into body)
            Vector3 probePos = new Vector3(0f, 0.10f, 0f);
            Quaternion probeRot = Quaternion.Euler(90f, 0f, 0f); // Beam points downward (-Y in world)

            generator.GenerateSlice(probePos, probeRot, 0.05f, 0.10f, ProbeType.Linear, 65f, 0.04f, sampler, polarBuffer, displayBuffer);

            int nonZeroCount = 0;
            for (int i = 0; i < displayBuffer.TotalPixels; i++)
            {
                if (displayBuffer.Intensities[i] > 0.01f)
                {
                    nonZeroCount++;
                }
            }

            Assert.Greater(nonZeroCount, 0, "Slice through body should contain tissue pixels.");
        }

        [Test]
        public void InsideBody_CurvilinearSector_GeneratesFanSectorWithMask()
        {
            Vector3 probePos = new Vector3(0f, 0.12f, 0f);
            Quaternion probeRot = Quaternion.Euler(90f, 0f, 0f);

            generator.GenerateSlice(probePos, probeRot, 0.05f, 0.12f, ProbeType.Curvilinear, 65f, 0.04f, sampler, polarBuffer, displayBuffer);

            // Center of fan should have tissue intensity (> 0.1f)
            int centerIdx = (displayBuffer.Height / 2) * displayBuffer.Width + (displayBuffer.Width / 2);
            Assert.Greater(displayBuffer.Intensities[centerIdx], 0.1f, "Center of sector should intersect tissue.");

            // Top corners of display grid should be masked black (0.0f)
            int topLeftIdx = 0;
            int topRightIdx = displayBuffer.Width - 1;
            Assert.AreEqual(0.0f, displayBuffer.Intensities[topLeftIdx], 1e-4f, "Corner outside fan sector should be black mask.");
            Assert.AreEqual(0.0f, displayBuffer.Intensities[topRightIdx], 1e-4f, "Corner outside fan sector should be black mask.");
        }

        [Test]
        public void SliceBuffer_ClearAndResize_WorksWithoutErrors()
        {
            displayBuffer.Resize(32, 32);
            Assert.AreEqual(32, displayBuffer.Width);
            Assert.AreEqual(32, displayBuffer.Height);
            Assert.AreEqual(1024, displayBuffer.TotalPixels);

            displayBuffer.SetPixel(10, 10, 0.75f);
            Assert.AreEqual(0.75f, displayBuffer.Intensities[10 * 32 + 10], 1e-3f);

            displayBuffer.Clear();
            Assert.AreEqual(0f, displayBuffer.Intensities[10 * 32 + 10]);
            Assert.AreEqual(0, displayBuffer.Pixels[10 * 32 + 10].r);
        }

        [Test]
        public void GPUVolumeData_BakesTextureAndMapsWorldBounds()
        {
            GPUVolumeData gpuVol = host.AddComponent<GPUVolumeData>();
            gpuVol.SetSourceVolume(volume);
            gpuVol.BakeFromSource();

            Assert.IsNotNull(gpuVol.VolumeTexture);
            Assert.AreEqual(128, gpuVol.VolumeTexture.width);
            Assert.AreEqual(128, gpuVol.VolumeTexture.height);
            Assert.AreEqual(128, gpuVol.VolumeTexture.depth);

            // Center of volume should map to normalized UVW (0.5, 0.5, 0.5)
            Vector3 centerUVW = gpuVol.WorldToVolumeUVW(volume.WorldBounds.Center);
            Assert.AreEqual(0.5f, centerUVW.x, 1e-3f);
            Assert.AreEqual(0.5f, centerUVW.y, 1e-3f);
            Assert.AreEqual(0.5f, centerUVW.z, 1e-3f);
        }

        [Test]
        public void AppearanceSettings_AcquirePolarData_ProducesValidBModeSignal()
        {
            Vector3 probePos = new Vector3(0f, 0.10f, 0f);
            Quaternion probeRot = Quaternion.Euler(90f, 0f, 0f);

            UltrasoundAppearanceSettings app = UltrasoundAppearanceSettings.Default;
            generator.AcquirePolarData(probePos, probeRot, 0.05f, 0.10f, ProbeType.Linear, 0f, 0f, sampler, polarBuffer, app);

            // Polar samples should be in [0, 1] range
            for (int i = 0; i < polarBuffer.Lines; i++)
            {
                for (int j = 0; j < polarBuffer.Samples; j++)
                {
                    float s = polarBuffer.GetSample(i, j);
                    Assert.GreaterOrEqual(s, 0.0f);
                    Assert.LessOrEqual(s, 1.0f);
                }
            }
        }
    }
}
