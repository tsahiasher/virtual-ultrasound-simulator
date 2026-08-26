using NUnit.Framework;
using UnityEngine;
using VirtualUltrasound.Core;
using VirtualUltrasound.Volume;

namespace VirtualUltrasound.Tests
{
    [TestFixture]
    public class SyntheticVolumeTests
    {
        private GameObject anatomyHost;
        private SyntheticAnatomyVolume volume;

        [SetUp]
        public void SetUp()
        {
            anatomyHost = new GameObject("TestAnatomyHost");
            volume = anatomyHost.AddComponent<SyntheticAnatomyVolume>();
        }

        [TearDown]
        public void TearDown()
        {
            if (anatomyHost != null)
            {
                Object.DestroyImmediate(anatomyHost);
            }
        }

        [Test]
        public void OutsideBody_ReturnsZeroIntensity()
        {
            Vector3 farOutside = new Vector3(2.0f, 2.0f, 2.0f);
            SampleResult result = volume.EvaluateSample(farOutside);

            Assert.AreEqual(0f, result.Intensity);
            Assert.AreEqual(TissueType.Background, result.Tissue);
        }

        [Test]
        public void InsideBodyParenchyma_ReturnsBodyIntensity()
        {
            // Point inside body ellipsoid but away from inner organs
            Vector3 parenchymaPoint = new Vector3(0.08f, 0.08f, 0.0f);
            SampleResult result = volume.EvaluateSample(parenchymaPoint);

            Assert.AreEqual(volume.BodyIntensity, result.Intensity, 1e-4f);
            Assert.AreEqual(TissueType.BodyTissue, result.Tissue);
        }

        [Test]
        public void InsideOrgan1_ReturnsOrgan1Intensity()
        {
            Vector3 organ1Pt = volume.Organ1Center;
            SampleResult result = volume.EvaluateSample(organ1Pt);

            Assert.AreEqual(volume.Organ1Intensity, result.Intensity, 1e-4f);
            Assert.AreEqual(TissueType.Organ1, result.Tissue);
        }

        [Test]
        public void InsideOrgan2_ReturnsCystIntensity()
        {
            Vector3 cystPt = volume.Organ2Center;
            SampleResult result = volume.EvaluateSample(cystPt);

            Assert.AreEqual(volume.Organ2Intensity, result.Intensity, 1e-4f);
            Assert.AreEqual(TissueType.Fluid, result.Tissue);
        }

        [Test]
        public void PrimitiveShapes_SphereSDF_MatchesAnalyticalFormula()
        {
            Vector3 center = new Vector3(0f, 0f, 0f);
            float radius = 0.05f;

            Assert.IsTrue(PrimitiveShapes.IsInsideSphere(new Vector3(0.02f, 0.02f, 0f), center, radius));
            Assert.IsFalse(PrimitiveShapes.IsInsideSphere(new Vector3(0.06f, 0f, 0f), center, radius));
            Assert.AreEqual(0.01f, PrimitiveShapes.SphereSDF(new Vector3(0.06f, 0f, 0f), center, radius), 1e-4f);
        }
    }
}
