using System;
using NUnit.Framework;
using UnityEngine;
using VirtualUltrasound.Core;
using VirtualUltrasound.Rendering;
using VirtualUltrasound.Volume;

namespace VirtualUltrasound.Tests
{
    [TestFixture]
    public class StandaloneTests
    {
        private const float Eps = 1e-4f;

        private class MockVolumeSampler : IVolumeSampler
        {
            public Vector3 SphereCenter = new Vector3(0f, 0f, 0.05f); // 50mm deep
            public float SphereRadius = 0.025f; // 25mm radius
            public float InsideIntensity = 0.8f;
            public float OutsideIntensity = 0.0f;

            public SampleResult SampleWorld(Vector3 worldPosition)
            {
                if ((worldPosition - SphereCenter).sqrMagnitude <= (SphereRadius * SphereRadius))
                {
                    return new SampleResult(InsideIntensity, TissueType.Organ1);
                }
                return new SampleResult(OutsideIntensity, TissueType.Background);
            }
        }

        [Test]
        public void PixelToUV_MapsEndpointsAndCenter()
        {
            Vector2 uv0 = CoordinateTransform.PixelToUV(0, 0, 100, 100);
            Assert.AreEqual(0f, uv0.x, Eps);
            Assert.AreEqual(0f, uv0.y, Eps);

            Vector2 uvEnd = CoordinateTransform.PixelToUV(99, 99, 100, 100);
            Assert.AreEqual(1f, uvEnd.x, Eps);
            Assert.AreEqual(1f, uvEnd.y, Eps);

            Vector2 uvMid = CoordinateTransform.PixelToUV(50, 50, 101, 101);
            Assert.AreEqual(0.5f, uvMid.x, Eps);
            Assert.AreEqual(0.5f, uvMid.y, Eps);
        }

        [Test]
        public void UVToLinearProbeSpace_CentersLateralAndExtendsAxial()
        {
            float width = 0.050f; // 50mm
            float depth = 0.120f; // 120mm

            Vector3 tl = CoordinateTransform.UVToLinearProbeSpace(new Vector2(0f, 0f), width, depth);
            Assert.AreEqual(-0.025f, tl.x, Eps);
            Assert.AreEqual(0f, tl.y, Eps);
            Assert.AreEqual(0f, tl.z, Eps);

            Vector3 br = CoordinateTransform.UVToLinearProbeSpace(new Vector2(1f, 1f), width, depth);
            Assert.AreEqual(0.025f, br.x, Eps);
            Assert.AreEqual(0f, br.y, Eps);
            Assert.AreEqual(0.120f, br.z, Eps);
        }

        [Test]
        public void ProbeToWorld_And_WorldToProbe_RoundTripIsExact()
        {
            Vector3 probePos = new Vector3(0.15f, 0.22f, -0.05f);
            Quaternion probeRot = Quaternion.Euler(30f, 45f, 60f);

            Vector3 localPt = new Vector3(0.02f, 0.0f, 0.08f);
            Vector3 worldPt = CoordinateTransform.ProbeToWorld(localPt, probePos, probeRot);
            Vector3 reconstructed = CoordinateTransform.WorldToProbe(worldPt, probePos, probeRot);

            Assert.AreEqual(localPt.x, reconstructed.x, 1e-3f);
            Assert.AreEqual(localPt.y, reconstructed.y, 1e-3f);
            Assert.AreEqual(localPt.z, reconstructed.z, 1e-3f);
        }

        [Test]
        public void PrimitiveShapes_ContainmentChecksAreAccurate()
        {
            Vector3 center = new Vector3(0f, 0f, 0f);
            Vector3 radii = new Vector3(0.10f, 0.20f, 0.15f);
            Quaternion rot = Quaternion.identity;

            // Inside ellipsoid
            Assert.IsTrue(PrimitiveShapes.IsInsideEllipsoid(new Vector3(0.05f, 0.10f, 0.05f), center, radii, rot));
            // Outside ellipsoid
            Assert.IsFalse(PrimitiveShapes.IsInsideEllipsoid(new Vector3(0.15f, 0f, 0f), center, radii, rot));

            // Sphere
            Assert.IsTrue(PrimitiveShapes.IsInsideSphere(new Vector3(0.02f, 0.01f, 0f), center, 0.05f));
            Assert.IsFalse(PrimitiveShapes.IsInsideSphere(new Vector3(0.06f, 0f, 0f), center, 0.05f));

            // Cylinder along Z axis from (0,0,0) to (0,0,0.1) with radius 0.02
            Vector3 cStart = new Vector3(0f, 0f, 0f);
            Vector3 cEnd = new Vector3(0f, 0f, 0.1f);
            float cRad = 0.02f;
            Assert.IsTrue(PrimitiveShapes.IsInsideCylinder(new Vector3(0.01f, 0.01f, 0.05f), cStart, cEnd, cRad));
            Assert.IsFalse(PrimitiveShapes.IsInsideCylinder(new Vector3(0.03f, 0f, 0.05f), cStart, cEnd, cRad));
            Assert.IsFalse(PrimitiveShapes.IsInsideCylinder(new Vector3(0f, 0f, 0.15f), cStart, cEnd, cRad)); // beyond end
        }

        [Test]
        public void SliceBuffer_AllocationAndPixelSetting_WorksCorrectly()
        {
            SliceBuffer buffer = new SliceBuffer(64, 64);
            Assert.AreEqual(4096, buffer.TotalPixels);

            buffer.SetPixel(32, 32, 0.5f);
            Assert.AreEqual(0.5f, buffer.Intensities[32 * 64 + 32], 1e-4f);
            Assert.AreEqual(128, buffer.Pixels[32 * 64 + 32].r);
            Assert.AreEqual(255, buffer.Pixels[32 * 64 + 32].a);

            buffer.Clear();
            Assert.AreEqual(0f, buffer.Intensities[32 * 64 + 32]);
            Assert.AreEqual(0, buffer.Pixels[32 * 64 + 32].r);
        }

        [Test]
        public void CPUSliceGenerator_SlicingSphere_ProducesExpectedCircleDimension()
        {
            // Probe positioned at (0, 0, 0), facing along +Z axis
            Vector3 probePos = Vector3.zero;
            Quaternion probeRot = Quaternion.identity;

            float apertureWidth = 0.080f; // 80mm
            float maxDepth = 0.100f;      // 100mm

            MockVolumeSampler sampler = new MockVolumeSampler
            {
                SphereCenter = new Vector3(0f, 0f, 0.050f), // 50mm deep
                SphereRadius = 0.020f,                       // 20mm radius
                InsideIntensity = 0.9f,
                OutsideIntensity = 0.0f
            };

            int res = 100;
            SliceBuffer buffer = new SliceBuffer(res, res);
            CPUSliceGenerator generator = new CPUSliceGenerator();

            generator.GenerateSlice(probePos, probeRot, apertureWidth, maxDepth, ProbeType.Linear, sampler, buffer);

            // Center of the sphere in slice:
            // Lateral center is u = 0.5 -> x = 49.5 (approx 49 or 50)
            // Depth center is v = 0.05 / 0.10 = 0.5 -> y = 50 in display (flipped index = 49 or 50)
            int centerPixelX = 50;
            int centerPixelY = 50;

            int centerIndex = centerPixelY * res + centerPixelX;
            Assert.AreEqual(0.9f, buffer.Intensities[centerIndex], 0.05f, "Center of sphere should be inside organ.");

            // Expected pixel radius of the circle:
            // Lateral span: 80mm -> 100 pixels => 1.25 pixels/mm
            // Sphere radius = 20mm => theoretical pixel radius ~ 25 pixels
            // Points at center +/- 15 pixels should be inside (intensity = 0.9)
            // Points at center +/- 32 pixels should be outside (intensity = 0.0)

            int insideIndex = centerPixelY * res + (centerPixelX + 15);
            Assert.AreEqual(0.9f, buffer.Intensities[insideIndex], 0.05f, "Point within sphere radius must be inside.");

            int outsideIndex = centerPixelY * res + (centerPixelX + 32);
            Assert.AreEqual(0.0f, buffer.Intensities[outsideIndex], 0.05f, "Point beyond sphere radius must be outside.");
        }

        [Test]
        public void CPUSliceGenerator_TranslatingProbeAway_ProducesEmptySlice()
        {
            Vector3 probePos = new Vector3(2f, 2f, 2f);
            Quaternion probeRot = Quaternion.identity;

            MockVolumeSampler sampler = new MockVolumeSampler();
            SliceBuffer buffer = new SliceBuffer(32, 32);
            CPUSliceGenerator generator = new CPUSliceGenerator();

            generator.GenerateSlice(probePos, probeRot, 0.05f, 0.10f, ProbeType.Linear, sampler, buffer);

            for (int i = 0; i < buffer.TotalPixels; i++)
            {
                Assert.AreEqual(0f, buffer.Intensities[i]);
            }
        }
    }
}
