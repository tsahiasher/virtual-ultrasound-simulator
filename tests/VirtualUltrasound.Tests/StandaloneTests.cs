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
            public float InsideScattering = 0.6f;
            public float OutsideIntensity = 0.0f;
            public float OutsideScattering = 0.0f;
            public int SampleCount = 0;

            public SampleResult SampleWorld(Vector3 worldPosition)
            {
                SampleCount++;
                if ((worldPosition - SphereCenter).sqrMagnitude <= (SphereRadius * SphereRadius))
                {
                    return new SampleResult(InsideIntensity, InsideScattering, TissueType.Organ1);
                }
                return new SampleResult(OutsideIntensity, OutsideScattering, TissueType.Background);
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
        public void GPUVolumeBounds_WorldToVolumeUVW_And_VolumeUVWToWorld_RoundTripIsExact()
        {
            Vector3 boundsMin = new Vector3(-0.15f, -0.20f, -0.12f);
            Vector3 boundsMax = new Vector3(0.15f, 0.20f, 0.12f);
            Vector3 boundsSize = boundsMax - boundsMin;

            Vector3 worldPos = new Vector3(0.045f, -0.080f, 0.030f);

            // Compute UVW: (worldPos - boundsMin) / boundsSize
            float u = (worldPos.x - boundsMin.x) / boundsSize.x;
            float v = (worldPos.y - boundsMin.y) / boundsSize.y;
            float w = (worldPos.z - boundsMin.z) / boundsSize.z;
            Vector3 uvw = new Vector3(u, v, w);

            // Reconstruct world position
            Vector3 reconstructed = new Vector3(
                boundsMin.x + (uvw.x * boundsSize.x),
                boundsMin.y + (uvw.y * boundsSize.y),
                boundsMin.z + (uvw.z * boundsSize.z)
            );

            Assert.AreEqual(worldPos.x, reconstructed.x, 1e-4f);
            Assert.AreEqual(worldPos.y, reconstructed.y, 1e-4f);
            Assert.AreEqual(worldPos.z, reconstructed.z, 1e-4f);
        }

        [Test]
        public void GPUShaderMath_ScanLineDirections_FirstCenterLast_MatchCPUCoordinateTransform()
        {
            float sectorAngleDeg = 65f;
            float sectorAngleRad = sectorAngleDeg * Mathf.Deg2Rad;
            float apexRadius = 0.040f;
            float maxDepth = 0.120f;
            int scanLines = 128;

            // 1. First scan line (id.x = 0, u = 0.0) -> left sector boundary (-halfAngle)
            float uFirst = 0.0f;
            float halfAngle = sectorAngleRad * 0.5f;
            float angleFirst = Mathf.Lerp(-halfAngle, halfAngle, uFirst);
            float rEnd = apexRadius + maxDepth;
            Vector3 gpuFirstRayProbe = new Vector3(rEnd * Mathf.Sin(angleFirst), 0f, (rEnd * Mathf.Cos(angleFirst)) - apexRadius);

            Vector3 cpuFirstRayProbe = CoordinateTransform.UVToCurvilinearProbeSpace(new Vector2(0f, 1f), sectorAngleDeg, apexRadius, maxDepth);
            Assert.AreEqual(cpuFirstRayProbe.x, gpuFirstRayProbe.x, 1e-4f);
            Assert.AreEqual(cpuFirstRayProbe.z, gpuFirstRayProbe.z, 1e-4f);

            // 2. Center scan line (u = 0.5) -> central beam axis (angle = 0.0)
            float uCenter = 0.5f;
            float angleCenter = Mathf.Lerp(-halfAngle, halfAngle, uCenter);
            Vector3 gpuCenterRayProbe = new Vector3(rEnd * Mathf.Sin(angleCenter), 0f, (rEnd * Mathf.Cos(angleCenter)) - apexRadius);

            Vector3 cpuCenterRayProbe = CoordinateTransform.UVToCurvilinearProbeSpace(new Vector2(0.5f, 1f), sectorAngleDeg, apexRadius, maxDepth);
            Assert.AreEqual(0f, gpuCenterRayProbe.x, 1e-4f);
            Assert.AreEqual(cpuCenterRayProbe.x, gpuCenterRayProbe.x, 1e-4f);
            Assert.AreEqual(cpuCenterRayProbe.z, gpuCenterRayProbe.z, 1e-4f);

            // 3. Last scan line (id.x = scanLines - 1, u = 1.0) -> right sector boundary (+halfAngle)
            float uLast = 1.0f;
            float angleLast = Mathf.Lerp(-halfAngle, halfAngle, uLast);
            Vector3 gpuLastRayProbe = new Vector3(rEnd * Mathf.Sin(angleLast), 0f, (rEnd * Mathf.Cos(angleLast)) - apexRadius);

            Vector3 cpuLastRayProbe = CoordinateTransform.UVToCurvilinearProbeSpace(new Vector2(1f, 1f), sectorAngleDeg, apexRadius, maxDepth);
            Assert.AreEqual(cpuLastRayProbe.x, gpuLastRayProbe.x, 1e-4f);
            Assert.AreEqual(cpuLastRayProbe.z, gpuLastRayProbe.z, 1e-4f);
        }

        [Test]
        public void CPUVsGPU_AnalyticalParity_SyntheticPrimitivesProduceMatchingSlices()
        {
            Vector3 probePos = new Vector3(0.01f, 0.02f, -0.01f);
            Quaternion probeRot = Quaternion.Euler(15f, 25f, 0f);
            Matrix4x4 probeRotMat = Matrix4x4.Rotate(probeRot);

            float sectorAngleDeg = 60f;
            float apexRadius = 0.040f;
            float maxDepth = 0.100f;
            int scanLines = 64;
            int samplesPerLine = 64;
            int dispW = 128;
            int dispH = 128;

            CountingMockVolumeSampler sampler = new CountingMockVolumeSampler
            {
                SphereCenter = new Vector3(0.01f, 0.02f, 0.05f),
                SphereRadius = 0.020f,
                InsideIntensity = 0.85f,
                OutsideIntensity = 0.15f
            };

            // 1. Run CPU Reference Pipeline
            PolarBuffer cpuPolar = new PolarBuffer(scanLines, samplesPerLine);
            SliceBuffer cpuDisplay = new SliceBuffer(dispW, dispH);
            CPUSliceGenerator cpuGen = new CPUSliceGenerator();
            cpuGen.GenerateSlice(probePos, probeRot, 0.05f, maxDepth, ProbeType.Curvilinear, sectorAngleDeg, apexRadius, sampler, cpuPolar, cpuDisplay);

            // 2. Simulate GPU Compute Shader Pipeline analytically
            float[,] gpuPolar = new float[scanLines, samplesPerLine];
            float invLines = 1.0f / (scanLines - 1);
            float invSamples = 1.0f / (samplesPerLine - 1);
            float halfAngleRad = (sectorAngleDeg * 0.5f) * Mathf.Deg2Rad;

            for (int x = 0; x < scanLines; x++)
            {
                float u = x * invLines;
                float angleRad = Mathf.Lerp(-halfAngleRad, halfAngleRad, u);
                for (int y = 0; y < samplesPerLine; y++)
                {
                    float v = y * invSamples;
                    float r = apexRadius + (v * maxDepth);
                    float xP = r * Mathf.Sin(angleRad);
                    float zP = (r * Mathf.Cos(angleRad)) - apexRadius;
                    Vector3 pP = new Vector3(xP, 0f, zP);
                    Vector3 pW = probePos + probeRotMat.MultiplyVector(pP);

                    gpuPolar[x, y] = sampler.SampleWorld(pW).Intensity;
                }
            }

            // Verify Stage 1 Polar acquisition parity between CPU and GPU math
            float maxPolarDiff = 0f;
            for (int x = 0; x < scanLines; x++)
            {
                for (int y = 0; y < samplesPerLine; y++)
                {
                    float diff = Mathf.Abs(cpuPolar.GetSample(x, y) - gpuPolar[x, y]);
                    if (diff > maxPolarDiff) maxPolarDiff = diff;
                }
            }
            Assert.AreEqual(0.0f, maxPolarDiff, 1e-4f, "CPU and GPU Stage 1 Polar Acquisition must match analytically.");

            // 3. Simulate GPU Stage 2 Scan Conversion
            CoordinateTransform.GetSectorBoundingDimensions(sectorAngleDeg, apexRadius, maxDepth, out float lateralSpan, out _);
            float effectiveLateralSpan = lateralSpan * 1.06f;
            float[,] gpuDisplay = new float[dispW, dispH];
            float invW = 1.0f / (dispW - 1);
            float invH = 1.0f / (dispH - 1);

            for (int y = 0; y < dispH; y++)
            {
                float v = ((dispH - 1 - y) * invH);
                float zP = v * maxDepth;
                float zApex = zP + apexRadius;

                for (int x = 0; x < dispW; x++)
                {
                    float u = (x * invW) - 0.5f;
                    float xP = u * effectiveLateralSpan;
                    float r = Mathf.Sqrt(xP * xP + zApex * zApex);
                    float angleRad = Mathf.Atan2(xP, zApex);

                    if (Mathf.Abs(angleRad) > halfAngleRad || r < apexRadius || r > (apexRadius + maxDepth))
                    {
                        gpuDisplay[x, y] = 0.0f; // Acoustic mask
                    }
                    else
                    {
                        float uPolar = (angleRad + halfAngleRad) / (2f * halfAngleRad);
                        float vPolar = (r - apexRadius) / maxDepth;
                        gpuDisplay[x, y] = cpuPolar.SampleBilinear(uPolar, vPolar);
                    }
                }
            }

            // Verify Stage 2 Scan Conversion parity
            float maxDispDiff = 0f;
            float sumDispDiff = 0f;
            for (int y = 0; y < dispH; y++)
            {
                for (int x = 0; x < dispW; x++)
                {
                    int cpuIdx = y * dispW + x;
                    float diff = Mathf.Abs(cpuDisplay.Intensities[cpuIdx] - gpuDisplay[x, y]);
                    if (diff > maxDispDiff) maxDispDiff = diff;
                    sumDispDiff += diff;
                }
            }

            float meanDispDiff = sumDispDiff / (dispW * dispH);
            Assert.AreEqual(0.0f, maxDispDiff, 1e-4f, "CPU and GPU Stage 2 Cartesian Scan Conversion must match analytically.");
            Assert.AreEqual(0.0f, meanDispDiff, 1e-4f, "Mean difference must be 0.0.");
        }

        [Test]
        public void CPUVsGPU_ProbeOutsideVolume_ProducesStrictZeroIntensities()
        {
            Vector3 probePosFar = new Vector3(10.0f, 10.0f, 10.0f); // 10 meters away
            Quaternion probeRot = Quaternion.identity;

            CountingMockVolumeSampler sampler = new CountingMockVolumeSampler
            {
                SphereCenter = Vector3.zero,
                SphereRadius = 0.05f,
                InsideIntensity = 1.0f,
                OutsideIntensity = 0.0f
            };

            PolarBuffer polar = new PolarBuffer(32, 32);
            SliceBuffer display = new SliceBuffer(64, 64);
            CPUSliceGenerator gen = new CPUSliceGenerator();

            gen.GenerateSlice(probePosFar, probeRot, 0.05f, 0.120f, ProbeType.Curvilinear, 65f, 0.040f, sampler, polar, display);

            for (int i = 0; i < display.TotalPixels; i++)
            {
                Assert.AreEqual(0.0f, display.Intensities[i], 1e-5f);
            }
        }

        [Test]
        public void BoundaryResponse_HomogeneousVsInterface_ProducesHigherEchoAtBoundary()
        {
            // Test that a spatial interface/boundary produces a distinctly higher gradient echo than a homogeneous region
            CountingMockVolumeSampler sampler = new CountingMockVolumeSampler
            {
                SphereCenter = new Vector3(0f, 0f, 0.05f),
                SphereRadius = 0.020f,
                InsideIntensity = 0.80f,
                InsideScattering = 0.0f, // zero scatter to isolate boundary gradient
                OutsideIntensity = 0.10f,
                OutsideScattering = 0.0f
            };

            CPUSliceGenerator generator = new CPUSliceGenerator();
            PolarBuffer polar = new PolarBuffer(32, 64);
            UltrasoundAppearanceSettings boundaryViewSettings = new UltrasoundAppearanceSettings
            {
                Enabled = true,
                DebugView = AppearanceDebugView.BoundaryResponse,
                BoundaryStrength = 2.0f,
                Gain = 1.0f
            };

            generator.AcquirePolarData(Vector3.zero, Quaternion.identity, 0.05f, 0.10f, ProbeType.Linear, 0f, 0f, sampler, polar, boundaryViewSettings);

            // Homogeneous outside region (e.g. depth 10mm -> sample index ~6)
            float homogeneousEcho = polar.GetSample(16, 6);

            // Interface boundary (depth 30mm, sphere edge -> sample index ~19)
            float boundaryEcho = polar.GetSample(16, 19);

            Assert.Greater(boundaryEcho, homogeneousEcho + 0.1f, "Material boundary must produce higher gradient echo than homogeneous interior.");
        }

        [Test]
        public void DepthAttenuation_DeeperSamples_MonotonicallyDecreaseSignal()
        {
            CountingMockVolumeSampler sampler = new CountingMockVolumeSampler
            {
                SphereCenter = Vector3.zero,
                SphereRadius = 1.0f, // Infinite homogeneous sphere
                InsideIntensity = 0.8f,
                InsideScattering = 0.5f,
                OutsideIntensity = 0.8f,
                OutsideScattering = 0.5f
            };

            CPUSliceGenerator generator = new CPUSliceGenerator();
            PolarBuffer polar = new PolarBuffer(8, 64);
            UltrasoundAppearanceSettings attenSettings = new UltrasoundAppearanceSettings
            {
                Enabled = true,
                DebugView = AppearanceDebugView.FinalUltrasound,
                SpeckleStrength = 0.0f, // zero speckle to test pure attenuation decay
                DepthAttenuation = 10.0f, // 10 m^-1 decay
                Gain = 1.0f,
                CompressionRatio = 1.0f
            };

            generator.AcquirePolarData(Vector3.zero, Quaternion.identity, 0.05f, 0.120f, ProbeType.Linear, 0f, 0f, sampler, polar, attenSettings);

            float shallowSignal = polar.GetSample(4, 5); // ~10mm depth
            float deepSignal = polar.GetSample(4, 55);    // ~100mm depth

            Assert.Greater(shallowSignal, deepSignal, "Shallow echo must be stronger than deep echo under positive depth attenuation.");
        }

        [Test]
        public void Gain_IncreasesSignalMonotonically()
        {
            CountingMockVolumeSampler sampler = new CountingMockVolumeSampler
            {
                SphereCenter = new Vector3(0f, 0f, 0.05f),
                SphereRadius = 0.03f,
                InsideIntensity = 0.5f,
                InsideScattering = 0.3f,
                OutsideIntensity = 0.0f,
                OutsideScattering = 0.0f
            };

            CPUSliceGenerator generator = new CPUSliceGenerator();
            PolarBuffer polarLow = new PolarBuffer(16, 32);
            PolarBuffer polarHigh = new PolarBuffer(16, 32);

            UltrasoundAppearanceSettings lowGain = new UltrasoundAppearanceSettings
            {
                Enabled = true,
                DebugView = AppearanceDebugView.FinalUltrasound,
                Gain = 0.5f,
                SpeckleStrength = 0.0f,
                DepthAttenuation = 0.0f,
                CompressionRatio = 10.0f
            };

            UltrasoundAppearanceSettings highGain = new UltrasoundAppearanceSettings
            {
                Enabled = true,
                DebugView = AppearanceDebugView.FinalUltrasound,
                Gain = 2.5f,
                SpeckleStrength = 0.0f,
                DepthAttenuation = 0.0f,
                CompressionRatio = 10.0f
            };

            generator.AcquirePolarData(Vector3.zero, Quaternion.identity, 0.05f, 0.10f, ProbeType.Linear, 0f, 0f, sampler, polarLow, lowGain);
            generator.AcquirePolarData(Vector3.zero, Quaternion.identity, 0.05f, 0.10f, ProbeType.Linear, 0f, 0f, sampler, polarHigh, highGain);

            float sampleLow = polarLow.GetSample(8, 16);
            float sampleHigh = polarHigh.GetSample(8, 16);

            Assert.Greater(sampleHigh, sampleLow, "Higher gain must produce higher signal intensity.");
        }

        [Test]
        public void DynamicRangeCompression_PreservesMonotonicOrdering()
        {
            float comp = 25.0f;
            float Compress(float x) => MathF.Log(1.0f + (comp * x)) / MathF.Log(1.0f + comp);

            float v0 = 0.05f;
            float v1 = 0.20f;
            float v2 = 0.80f;

            float c0 = Compress(v0);
            float c1 = Compress(v1);
            float c2 = Compress(v2);

            Assert.Less(c0, c1);
            Assert.Less(c1, c2);

            // Weak echoes (v0) should receive higher relative boost than saturated echoes (v2)
            float boostWeak = c0 / v0; // ~4.5x boost
            float boostStrong = c2 / v2; // ~1.1x boost
            Assert.Greater(boostWeak, boostStrong, "Logarithmic compression should boost weak echoes more than strong echoes.");
        }

        [Test]
        public void CoherentSpeckle_StationaryProbe_Is100PercentDeterministic()
        {
            Vector3 pos = new Vector3(0.02f, -0.015f, 0.045f);
            float scale = 150.0f;

            float val1 = CPUSliceGenerator.CoherentNoise3D(pos, scale);
            float val2 = CPUSliceGenerator.CoherentNoise3D(pos, scale);

            Assert.AreEqual(val1, val2, 1e-6f, "3D coherent noise must be 100% deterministic for identical spatial positions.");
        }

        [Test]
        public void CoherentSpeckle_SpatialTranslation_VariesCohesivelyWithPosition()
        {
            Vector3 p1 = new Vector3(0.0f, 0.0f, 0.05f);
            Vector3 p2 = new Vector3(0.02f, 0.0f, 0.05f); // 20mm laterally
            float scale = 150.0f;

            float val1 = CPUSliceGenerator.CoherentNoise3D(p1, scale);
            float val2 = CPUSliceGenerator.CoherentNoise3D(p2, scale);

            Assert.AreNotEqual(val1, val2, "Spatial noise should vary across distinct physical coordinates.");
        }

        [Test]
        public void FluidRegion_GeneratesLowInternalScatterWithEchoicBoundary()
        {
            // Fluid has very low scattering (0.02) and distinct outer boundary
            CountingMockVolumeSampler sampler = new CountingMockVolumeSampler
            {
                SphereCenter = new Vector3(0f, 0f, 0.05f),
                SphereRadius = 0.025f,
                InsideIntensity = 0.04f,
                InsideScattering = 0.02f,
                OutsideIntensity = 0.50f,
                OutsideScattering = 0.40f
            };

            CPUSliceGenerator generator = new CPUSliceGenerator();
            PolarBuffer polar = new PolarBuffer(32, 64);
            UltrasoundAppearanceSettings app = UltrasoundAppearanceSettings.Default;

            generator.AcquirePolarData(Vector3.zero, Quaternion.identity, 0.05f, 0.10f, ProbeType.Linear, 0f, 0f, sampler, polar, app);

            // Center of cyst (depth 50mm -> sample index 32)
            float centerSignal = polar.GetSample(16, 32);

            // Surrounding tissue (depth 15mm -> sample index ~9)
            float tissueSignal = polar.GetSample(16, 9);

            Assert.Less(centerSignal, tissueSignal, "Fluid interior must be darker than surrounding tissue.");
        }

        [Test]
        public void Appearance_DoesNotAlterGeometryOrAcousticSectorMask()
        {
            CountingMockVolumeSampler sampler = new CountingMockVolumeSampler
            {
                SphereCenter = new Vector3(0f, 0f, 0.05f),
                SphereRadius = 0.020f,
                InsideIntensity = 0.85f,
                InsideScattering = 0.60f,
                OutsideIntensity = 0.20f,
                OutsideScattering = 0.25f
            };

            PolarBuffer polar = new PolarBuffer(32, 64);
            SliceBuffer display = new SliceBuffer(128, 128);
            CPUSliceGenerator generator = new CPUSliceGenerator();

            UltrasoundAppearanceSettings app = UltrasoundAppearanceSettings.Default;

            generator.AcquirePolarData(Vector3.zero, Quaternion.identity, 0.05f, 0.120f, ProbeType.Curvilinear, 65f, 0.040f, sampler, polar, app);
            generator.ScanConvert(0.05f, 0.120f, ProbeType.Curvilinear, 65f, 0.040f, polar, display);

            // Display top-left corner outside sector must STILL be strictly 0.0 mask!
            int topLeftIdx = 0;
            int topRightIdx = display.Width - 1;
            Assert.AreEqual(0.0f, display.Intensities[topLeftIdx], 1e-4f, "Acoustic mask must remain strictly black outside sector.");
            Assert.AreEqual(0.0f, display.Intensities[topRightIdx], 1e-4f, "Acoustic mask must remain strictly black outside sector.");
        }

        [Test]
        public void Benchmark_TwoStagePipeline_PerformanceMetrics()
        {
            CPUSliceGenerator generator = new CPUSliceGenerator();
            CountingMockVolumeSampler sampler = new CountingMockVolumeSampler();
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

            (int lines, int samples, int dispW, int dispH, string label)[] configs = new[]
            {
                (32, 32, 256, 256, "Low"),
                (128, 256, 256, 256, "Normal"),
                (256, 512, 256, 256, "High"),
                (512, 512, 256, 256, "Stress")
            };

            Console.WriteLine("\n--- CPU Reference Performance Benchmark (Phase 4 Profiling) ---");
            foreach (var cfg in configs)
            {
                PolarBuffer polar = new PolarBuffer(cfg.lines, cfg.samples);
                SliceBuffer display = new SliceBuffer(cfg.dispW, cfg.dispH);

                // Warm up
                generator.GenerateSlice(Vector3.zero, Quaternion.identity, 0.05f, 0.120f, ProbeType.Curvilinear, 65f, 0.040f, sampler, polar, display);

                // Benchmark 30 iterations
                int iterations = 30;
                sw.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    generator.GenerateSlice(Vector3.zero, Quaternion.identity, 0.05f, 0.120f, ProbeType.Curvilinear, 65f, 0.040f, sampler, polar, display);
                }
                sw.Stop();

                double avgTimeMs = sw.Elapsed.TotalMilliseconds / iterations;
                int totalSamples = cfg.lines * cfg.samples;
                Console.WriteLine($"[{cfg.label}] Acquisition: {cfg.lines}x{cfg.samples} ({totalSamples:N0} vol samples) | Display: {cfg.dispW}x{cfg.dispH} | CPU Time: {avgTimeMs:F2}ms | CPU Approx FPS: {1000.0 / avgTimeMs:F0}");
            }
        }
    }
}
