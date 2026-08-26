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
        private SliceBuffer buffer;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("SliceTestHost");
            volume = host.AddComponent<SyntheticAnatomyVolume>();
            sampler = host.AddComponent<ProceduralVolumeSampler>();
            sampler.AnatomyVolume = volume;
            generator = new CPUSliceGenerator();
            buffer = new SliceBuffer(64, 64);
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

            generator.GenerateSlice(outsidePos, rot, 0.05f, 0.10f, ProbeType.Linear, sampler, buffer);

            for (int i = 0; i < buffer.TotalPixels; i++)
            {
                Assert.AreEqual(0f, buffer.Intensities[i]);
                Assert.AreEqual(0, buffer.Pixels[i].r);
            }
        }

        [Test]
        public void InsideBody_GeneratesNonEmptySlice()
        {
            // Place probe at top of body pointing down (into body)
            Vector3 probePos = new Vector3(0f, 0.10f, 0f);
            Quaternion probeRot = Quaternion.Euler(90f, 0f, 0f); // Beam points downward (-Y in world)

            generator.GenerateSlice(probePos, probeRot, 0.05f, 0.10f, ProbeType.Linear, sampler, buffer);

            int nonZeroCount = 0;
            for (int i = 0; i < buffer.TotalPixels; i++)
            {
                if (buffer.Intensities[i] > 0.01f)
                {
                    nonZeroCount++;
                }
            }

            Assert.Greater(nonZeroCount, 0, "Slice through body should contain tissue pixels.");
        }

        [Test]
        public void SliceBuffer_ClearAndResize_WorksWithoutErrors()
        {
            buffer.Resize(32, 32);
            Assert.AreEqual(32, buffer.Width);
            Assert.AreEqual(32, buffer.Height);
            Assert.AreEqual(1024, buffer.TotalPixels);

            buffer.SetPixel(10, 10, 0.75f);
            Assert.AreEqual(0.75f, buffer.Intensities[10 * 32 + 10], 1e-3f);

            buffer.Clear();
            Assert.AreEqual(0f, buffer.Intensities[10 * 32 + 10]);
            Assert.AreEqual(0, buffer.Pixels[10 * 32 + 10].r);
        }
    }
}
