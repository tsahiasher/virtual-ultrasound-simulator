using NUnit.Framework;
using UnityEngine;
using VirtualUltrasound.Core;

namespace VirtualUltrasound.Tests
{
    [TestFixture]
    public class CoordinateTransformTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void PixelToUV_MapsCorrectly()
        {
            Vector2 uv0 = CoordinateTransform.PixelToUV(0, 0, 100, 100);
            Assert.AreEqual(0f, uv0.x, Tolerance);
            Assert.AreEqual(0f, uv0.y, Tolerance);

            Vector2 uvEnd = CoordinateTransform.PixelToUV(99, 99, 100, 100);
            Assert.AreEqual(1f, uvEnd.x, Tolerance);
            Assert.AreEqual(1f, uvEnd.y, Tolerance);

            Vector2 uvMid = CoordinateTransform.PixelToUV(50, 50, 101, 101);
            Assert.AreEqual(0.5f, uvMid.x, Tolerance);
            Assert.AreEqual(0.5f, uvMid.y, Tolerance);
        }

        [Test]
        public void UVToLinearProbeSpace_CentersLateralAndExtendsAxial()
        {
            float width = 0.050f; // 50mm
            float depth = 0.120f; // 120mm

            // Top-Left (u=0, v=0): (-width/2, 0, 0)
            Vector3 tl = CoordinateTransform.UVToLinearProbeSpace(new Vector2(0f, 0f), width, depth);
            Assert.AreEqual(-0.025f, tl.x, Tolerance);
            Assert.AreEqual(0f, tl.y, Tolerance);
            Assert.AreEqual(0f, tl.z, Tolerance);

            // Center (u=0.5, v=0.5): (0, 0, depth/2)
            Vector3 mid = CoordinateTransform.UVToLinearProbeSpace(new Vector2(0.5f, 0.5f), width, depth);
            Assert.AreEqual(0f, mid.x, Tolerance);
            Assert.AreEqual(0f, mid.y, Tolerance);
            Assert.AreEqual(0.060f, mid.z, Tolerance);

            // Bottom-Right (u=1, v=1): (+width/2, 0, depth)
            Vector3 br = CoordinateTransform.UVToLinearProbeSpace(new Vector2(1f, 1f), width, depth);
            Assert.AreEqual(0.025f, br.x, Tolerance);
            Assert.AreEqual(0f, br.y, Tolerance);
            Assert.AreEqual(0.120f, br.z, Tolerance);
        }

        [Test]
        public void ProbeToWorld_And_WorldToProbe_RoundTripIsExact()
        {
            Vector3 probePos = new Vector3(0.15f, 0.22f, -0.05f);
            Quaternion probeRot = Quaternion.Euler(30f, 45f, 60f);

            Vector3 localPt = new Vector3(0.02f, 0.0f, 0.08f);

            Vector3 worldPt = CoordinateTransform.ProbeToWorld(localPt, probePos, probeRot);
            Vector3 reconstructedLocal = CoordinateTransform.WorldToProbe(worldPt, probePos, probeRot);

            Assert.AreEqual(localPt.x, reconstructedLocal.x, Tolerance);
            Assert.AreEqual(localPt.y, reconstructedLocal.y, Tolerance);
            Assert.AreEqual(localPt.z, reconstructedLocal.z, Tolerance);
        }

        [Test]
        public void WorldToVolume_PreservesRelativeOffset()
        {
            Vector3 volCenter = new Vector3(0.1f, 0.2f, 0.3f);
            Quaternion volRot = Quaternion.identity;

            Vector3 testWorldPt = new Vector3(0.15f, 0.22f, 0.30f);
            Vector3 localPt = CoordinateTransform.WorldToVolume(testWorldPt, volCenter, volRot);

            Assert.AreEqual(0.05f, localPt.x, Tolerance);
            Assert.AreEqual(0.02f, localPt.y, Tolerance);
            Assert.AreEqual(0.00f, localPt.z, Tolerance);
        }
    }
}
