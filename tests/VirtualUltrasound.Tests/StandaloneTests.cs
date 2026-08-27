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

        private class CountingMockVolumeSampler : IVolumeSampler
        {
            public Vector3 SphereCenter = new Vector3(0f, 0f, 0.05f); // 50mm deep
            public float SphereRadius = 0.025f; // 25mm radius
            public float InsideIntensity = 0.8f;
            public float OutsideIntensity = 0.0f;
            public int SampleCount = 0;

            public SampleResult SampleWorld(Vector3 worldPosition)
            {
                SampleCount++;
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
        public void PolarToProbeSpace_And_ProbeSpaceToPolar_RoundTripIsExact()
        {
            float apexRadius = 0.040f; // 40mm
            float angleDeg = 25f;
            float angleRad = angleDeg * Mathf.Deg2Rad;
            float radius = 0.100f; // 100mm from apex

            Vector3 pP = CoordinateTransform.PolarToProbeSpace(angleRad, radius, apexRadius);

            CoordinateTransform.ProbeSpaceToPolar(pP, apexRadius, out float reconstructedAngleRad, out float reconstructedRadius);

            Assert.AreEqual(angleRad, reconstructedAngleRad, 1e-4f);
            Assert.AreEqual(radius, reconstructedRadius, 1e-4f);
        }

        [Test]
        public void CurvilinearRayAngles_MatchSectorBoundaries()
        {
            float sectorAngleDeg = 60f;
            float apexRadius = 0.040f;
            float maxDepth = 0.120f;

            // u=0 (left boundary) -> angle = -30 deg
            Vector3 leftPt = CoordinateTransform.UVToCurvilinearProbeSpace(new Vector2(0f, 1f), sectorAngleDeg, apexRadius, maxDepth);
            CoordinateTransform.ProbeSpaceToPolar(leftPt, apexRadius, out float leftAngleRad, out float leftRadius);
            Assert.AreEqual(-30f * Mathf.Deg2Rad, leftAngleRad, 1e-3f);
            Assert.AreEqual(apexRadius + maxDepth, leftRadius, 1e-3f);

            // u=0.5 (center beam axis) -> angle = 0 deg
            Vector3 centerPt = CoordinateTransform.UVToCurvilinearProbeSpace(new Vector2(0.5f, 1f), sectorAngleDeg, apexRadius, maxDepth);
            CoordinateTransform.ProbeSpaceToPolar(centerPt, apexRadius, out float centerAngleRad, out float centerRadius);
            Assert.AreEqual(0f, centerAngleRad, 1e-3f);
            Assert.AreEqual(apexRadius + maxDepth, centerRadius, 1e-3f);

            // u=1.0 (right boundary) -> angle = +30 deg
            Vector3 rightPt = CoordinateTransform.UVToCurvilinearProbeSpace(new Vector2(1f, 1f), sectorAngleDeg, apexRadius, maxDepth);
            CoordinateTransform.ProbeSpaceToPolar(rightPt, apexRadius, out float rightAngleRad, out float rightRadius);
            Assert.AreEqual(30f * Mathf.Deg2Rad, rightAngleRad, 1e-3f);
            Assert.AreEqual(apexRadius + maxDepth, rightRadius, 1e-3f);
        }

        [Test]
        public void CurvilinearRayDepths_FirstAndLastSamples_MatchApexAndMaxDepth()
        {
            float apexRadius = 0.040f; // 40mm
            float maxDepth = 0.120f;   // 120mm

            // Top shallow sample (j=0, v=0) -> radius = apexRadius (40mm)
            Vector3 topPt = CoordinateTransform.PolarToProbeSpace(0f, apexRadius, apexRadius);
            Assert.AreEqual(0f, topPt.x, 1e-4f);
            Assert.AreEqual(0f, topPt.z, 1e-4f); // At probe face contact

            // Bottom deep sample (j=N-1, v=1) -> radius = apexRadius + maxDepth (160mm)
            Vector3 bottomPt = CoordinateTransform.PolarToProbeSpace(0f, apexRadius + maxDepth, apexRadius);
            Assert.AreEqual(0f, bottomPt.x, 1e-4f);
            Assert.AreEqual(maxDepth, bottomPt.z, 1e-4f); // 120mm deep along central beam
        }

        [Test]
        public void ScanConversion_NearestVsBilinear_ModesBehaveDifferentlyForCoarseAcquisition()
        {
            // Create a very coarse 2x2 acquisition buffer
            PolarBuffer polarCoarse = new PolarBuffer(2, 2);
            polarCoarse.SetSample(0, 0, 0.0f);
            polarCoarse.SetSample(1, 0, 1.0f);
            polarCoarse.SetSample(0, 1, 0.0f);
            polarCoarse.SetSample(1, 1, 1.0f);

            SliceBuffer displayBilinear = new SliceBuffer(16, 16);
            SliceBuffer displayNearest = new SliceBuffer(16, 16);
            CPUSliceGenerator generator = new CPUSliceGenerator();

            generator.ScanConvert(0.05f, 0.120f, ProbeType.Curvilinear, 65f, 0.040f, polarCoarse, displayBilinear, ScanConversionFilterMode.Bilinear);
            generator.ScanConvert(0.05f, 0.120f, ProbeType.Curvilinear, 65f, 0.040f, polarCoarse, displayNearest, ScanConversionFilterMode.NearestNeighbor);

            // Bilinear produces continuous gradient values between 0 and 1
            bool hasIntermediateValue = false;
            for (int i = 0; i < displayBilinear.TotalPixels; i++)
            {
                float val = displayBilinear.Intensities[i];
                if (val > 0.1f && val < 0.9f)
                {
                    hasIntermediateValue = true;
                    break;
                }
            }
            Assert.IsTrue(hasIntermediateValue, "Bilinear scan conversion should produce continuous intermediate gradient values.");

            // Nearest-neighbor only produces 0.0 or 1.0
            for (int i = 0; i < displayNearest.TotalPixels; i++)
            {
                float val = displayNearest.Intensities[i];
                Assert.IsTrue(Mathf.Approximately(val, 0f) || Mathf.Approximately(val, 1f),
                    $"Nearest-neighbor sample was {val}, expected strictly 0.0 or 1.0");
            }
        }

        [Test]
        public void PolarBuffer_AllocationAndBilinearInterpolation_AreAccurate()
        {
            // Test 2x2 grid for exact corner bilinear interpolation
            PolarBuffer polar2x2 = new PolarBuffer(2, 2);
            Assert.AreEqual(4, polar2x2.TotalSamples);
            Assert.AreEqual(2, polar2x2.Lines);
            Assert.AreEqual(2, polar2x2.Samples);

            // Populate corners: (0,0)=0, (1,0)=1, (0,1)=0, (1,1)=1
            polar2x2.SetSample(0, 0, 0f);
            polar2x2.SetSample(1, 0, 1f);
            polar2x2.SetSample(0, 1, 0f);
            polar2x2.SetSample(1, 1, 1f);

            // Sample midpoint u=0.5, v=0.5 -> should be exactly 0.5
            float midSample = polar2x2.SampleBilinear(0.5f, 0.5f);
            Assert.AreEqual(0.5f, midSample, 1e-4f);

            // Test 4x4 grid for nearest-neighbor lookups
            PolarBuffer polar4x4 = new PolarBuffer(4, 4);
            polar4x4.SetSample(0, 0, 0f);
            polar4x4.SetSample(3, 0, 1f);

            // Nearest sample at u=0.1, v=0.1 -> index (0,0) -> 0.0
            float nearSample0 = polar4x4.SampleNearest(0.1f, 0.1f);
            Assert.AreEqual(0f, nearSample0, 1e-4f);

            // Nearest sample at u=0.9, v=0.1 -> index (3,0) -> 1.0
            float nearSample1 = polar4x4.SampleNearest(0.9f, 0.1f);
            Assert.AreEqual(1f, nearSample1, 1e-4f);
        }

        [Test]
        public void AcquisitionSampleCount_StrictlyEquals_ScanLinesTimesSamplesPerLine()
        {
            Vector3 probePos = Vector3.zero;
            Quaternion probeRot = Quaternion.identity;

            int lines = 32;
            int samples = 64;
            int expectedSamples = lines * samples; // 2,048

            PolarBuffer polarBuffer = new PolarBuffer(lines, samples);
            CountingMockVolumeSampler countingSampler = new CountingMockVolumeSampler();
            CPUSliceGenerator generator = new CPUSliceGenerator();

            generator.AcquirePolarData(
                probePos, probeRot,
                0.05f, 0.120f,
                ProbeType.Curvilinear,
                65f, 0.040f,
                countingSampler,
                polarBuffer
            );

            Assert.AreEqual(expectedSamples, countingSampler.SampleCount,
                "Stage 1 acquisition MUST perform strictly ScanLines * SamplesPerScanLine volume queries.");
        }

        [Test]
        public void DisplayResolutionChange_DoesNotAffectAcquisitionSampleCount()
        {
            Vector3 probePos = Vector3.zero;
            Quaternion probeRot = Quaternion.identity;

            int lines = 16;
            int samples = 32;
            int expectedAcquisitionSamples = lines * samples; // 512

            PolarBuffer polarBuffer = new PolarBuffer(lines, samples);
            CountingMockVolumeSampler countingSampler = new CountingMockVolumeSampler();
            CPUSliceGenerator generator = new CPUSliceGenerator();

            // Stage 1: Acquire into polar buffer
            generator.AcquirePolarData(
                probePos, probeRot,
                0.05f, 0.120f,
                ProbeType.Curvilinear,
                65f, 0.040f,
                countingSampler,
                polarBuffer
            );

            Assert.AreEqual(expectedAcquisitionSamples, countingSampler.SampleCount);

            // Stage 2: Scan convert to small display (64x64)
            SliceBuffer smallDisplay = new SliceBuffer(64, 64);
            generator.ScanConvert(0.05f, 0.120f, ProbeType.Curvilinear, 65f, 0.040f, polarBuffer, smallDisplay);

            // Stage 2: Scan convert to large display (512x512)
            SliceBuffer largeDisplay = new SliceBuffer(512, 512);
            generator.ScanConvert(0.05f, 0.120f, ProbeType.Curvilinear, 65f, 0.040f, polarBuffer, largeDisplay);

            // Total volume queries should STILL be exactly 512! Scan conversion queries 0 volume samples!
            Assert.AreEqual(expectedAcquisitionSamples, countingSampler.SampleCount,
                "Scan conversion MUST perform zero 3D volume queries.");
        }

        [Test]
        public void TwoStageCurvilinearPipeline_ProducesFanShapeWithAcousticMask()
        {
            Vector3 probePos = Vector3.zero;
            Quaternion probeRot = Quaternion.identity;

            float sectorAngle = 65f;
            float apexRadius = 0.040f;
            float maxDepth = 0.120f;

            CountingMockVolumeSampler sampler = new CountingMockVolumeSampler
            {
                SphereCenter = new Vector3(0f, 0f, 0.050f),
                SphereRadius = 0.020f,
                InsideIntensity = 0.85f,
                OutsideIntensity = 0.20f
            };

            PolarBuffer polarBuffer = new PolarBuffer(64, 128);
            SliceBuffer displayBuffer = new SliceBuffer(256, 256);
            CPUSliceGenerator generator = new CPUSliceGenerator();

            generator.GenerateSlice(
                probePos, probeRot,
                0.05f, maxDepth,
                ProbeType.Curvilinear,
                sectorAngle, apexRadius,
                sampler,
                polarBuffer,
                displayBuffer
            );

            // Exactly 64 x 128 = 8,192 volume queries performed
            Assert.AreEqual(64 * 128, sampler.SampleCount);

            // Center of sphere should be high intensity
            int centerIdx = (displayBuffer.Height / 2) * displayBuffer.Width + (displayBuffer.Width / 2);
            Assert.Greater(displayBuffer.Intensities[centerIdx], 0.5f, "Center of organ sphere must be high intensity.");

            // Display corners outside sector fan must be pure black (0.0 intensity)
            int topLeftIdx = 0;
            int topRightIdx = displayBuffer.Width - 1;
            Assert.AreEqual(0.0f, displayBuffer.Intensities[topLeftIdx], Eps, "Top-left outside sector must be 0.0 mask.");
            Assert.AreEqual(0.0f, displayBuffer.Intensities[topRightIdx], Eps, "Top-right outside sector must be 0.0 mask.");
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
            Assert.IsFalse(PrimitiveShapes.IsInsideCylinder(new Vector3(0f, 0f, 0.15f), cStart, cEnd, cRad));
        }

        [Test]
        public void SliceBuffer_AllocationAndPixelSetting_WorksCorrectly()
        {
            SliceBuffer buffer = new SliceBuffer(64, 64);
            Assert.AreEqual(4096, buffer.TotalPixels);

            buffer.Clear();
            Assert.AreEqual(0f, buffer.Intensities[32 * 64 + 32]);
            Assert.AreEqual(0, buffer.Pixels[32 * 64 + 32].r);
        }

        [Test]
        public void Benchmark_TwoStagePipeline_PerformanceMetrics()
        {
            CPUSliceGenerator generator = new CPUSliceGenerator();
            CountingMockVolumeSampler sampler = new CountingMockVolumeSampler();
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

            (int lines, int samples, int dispW, int dispH)[] configs = new[]
            {
                (32, 32, 256, 256),
                (128, 128, 256, 256),
                (256, 512, 256, 256),
                (256, 512, 512, 512)
            };

            Console.WriteLine("\n--- CPU Reference Performance Benchmark ---");
            foreach (var cfg in configs)
            {
                PolarBuffer polar = new PolarBuffer(cfg.lines, cfg.samples);
                SliceBuffer display = new SliceBuffer(cfg.dispW, cfg.dispH);

                // Warm up
                generator.GenerateSlice(Vector3.zero, Quaternion.identity, 0.05f, 0.120f, ProbeType.Curvilinear, 65f, 0.040f, sampler, polar, display);

                // Benchmark 50 iterations
                int iterations = 50;
                sw.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    generator.GenerateSlice(Vector3.zero, Quaternion.identity, 0.05f, 0.120f, ProbeType.Curvilinear, 65f, 0.040f, sampler, polar, display);
                }
                sw.Stop();

                double avgTimeMs = sw.Elapsed.TotalMilliseconds / iterations;
                int totalSamples = cfg.lines * cfg.samples;
                Console.WriteLine($"Acquisition: {cfg.lines}x{cfg.samples} ({totalSamples:N0} vol samples) | Display: {cfg.dispW}x{cfg.dispH} | Avg Time: {avgTimeMs:F3}ms | Approx FPS: {1000.0 / avgTimeMs:F0}");
            }
        }
    }
}
