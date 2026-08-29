using UnityEngine;
using UnityEngine.UI;
using VirtualUltrasound.Core;
using VirtualUltrasound.Probe;
using VirtualUltrasound.Rendering;
using VirtualUltrasound.Volume;

namespace VirtualUltrasound.UI
{
    /// <summary>
    /// Manages the heads-up display, telemetry data, mode toggles, and view presets.
    /// </summary>
    public class UIController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private ProbeController probeController;
        [SerializeField] private ProbeGeometry probeGeometry;
        [SerializeField] private ProbeVisualizer probeVisualizer;
        [SerializeField] private AnatomyVisualizer anatomyVisualizer;
        [SerializeField] private SliceRenderer sliceRenderer;

        [Header("UI Text References")]
        [SerializeField] private Text telemetryText;
        [SerializeField] private Text performanceText;
        [SerializeField] private Text controlsHelpText;

        private float fpsAccumulator = 0f;
        private int fpsFrames = 0;
        private float currentFps = 60f;
        private float fpsUpdateTimer = 0.5f;

        private void Start()
        {
            FindReferences();
        }

        public void FindReferences()
        {
            if (probeController == null) probeController = FindObjectOfType<ProbeController>();
            if (probeGeometry == null) probeGeometry = FindObjectOfType<ProbeGeometry>();
            if (probeVisualizer == null) probeVisualizer = FindObjectOfType<ProbeVisualizer>();
            if (anatomyVisualizer == null) anatomyVisualizer = FindObjectOfType<AnatomyVisualizer>();
            if (sliceRenderer == null) sliceRenderer = FindObjectOfType<SliceRenderer>();

            if (telemetryText == null || performanceText == null || controlsHelpText == null)
            {
                Text[] allTexts = FindObjectsOfType<Text>(true);
                foreach (var t in allTexts)
                {
                    if (t.gameObject.name == "TelemetryText") telemetryText = t;
                    else if (t.gameObject.name == "PerformanceText") performanceText = t;
                    else if (t.gameObject.name == "ControlsGuide") controlsHelpText = t;
                }
            }
        }

        private void Update()
        {
            if (probeGeometry == null || telemetryText == null)
            {
                FindReferences();
            }

            // Keyboard shortcut for cycling render modes (GPU -> CPU Reference -> Difference)
            if (Input.GetKeyDown(KeyCode.M))
            {
                if (sliceRenderer != null)
                {
                    sliceRenderer.ToggleRenderMode();
                }
            }

            // Keyboard shortcut for cycling appearance debug views (Final -> Raw -> Boundary -> Speckle)
            if (Input.GetKeyDown(KeyCode.V))
            {
                if (sliceRenderer != null)
                {
                    sliceRenderer.CycleDebugView();
                }
            }

            UpdateFPS();
            UpdateTelemetry();
        }

        private void UpdateFPS()
        {
            fpsAccumulator += Time.unscaledDeltaTime;
            fpsFrames++;

            fpsUpdateTimer -= Time.unscaledDeltaTime;
            if (fpsUpdateTimer <= 0f)
            {
                currentFps = fpsFrames / fpsAccumulator;
                fpsAccumulator = 0f;
                fpsFrames = 0;
                fpsUpdateTimer = 0.5f;
            }

            if (performanceText != null && sliceRenderer != null && probeGeometry != null)
            {
                string modeStr = sliceRenderer.RenderMode switch
                {
                    UltrasoundRenderMode.GPU => "<color=#4ade80>[GPU]</color>",
                    UltrasoundRenderMode.CPUReference => "<color=#60a5fa>[CPU Ref]</color>",
                    UltrasoundRenderMode.Difference => "<color=#f87171>[Diff]</color>",
                    _ => "[GPU]"
                };

                string viewStr = sliceRenderer.AppearanceSettings.DebugView switch
                {
                    AppearanceDebugView.FinalUltrasound => "B-Mode",
                    AppearanceDebugView.RawAnatomical => "Raw",
                    AppearanceDebugView.BoundaryResponse => "Boundary",
                    AppearanceDebugView.SpeckleScattering => "Speckle",
                    _ => "B-Mode"
                };

                performanceText.text = $"FPS: {currentFps:F0} | {modeStr} ({viewStr}) | {sliceRenderer.LastRenderTimeMs:F1}ms (Acq: {sliceRenderer.LastAcquisitionTimeMs:F1}ms, Scan: {sliceRenderer.LastScanConvertTimeMs:F1}ms)";
            }
        }

        private void UpdateTelemetry()
        {
            if (telemetryText != null && probeGeometry != null && sliceRenderer != null)
            {
                Vector3 pos = probeGeometry.Origin * 1000f; // in millimeters for clinical realism
                Vector3 rot = probeGeometry.Orientation.eulerAngles;

                string geomDetails = probeGeometry.Type == Core.ProbeType.Curvilinear
                    ? $"Optics: <b>Convex Sector</b> (FOV {probeGeometry.SectorAngleDegrees:F0}°, Depth {probeGeometry.MaxDepth * 1000f:F0}mm, R={probeGeometry.ConvexRadius * 1000f:F0}mm)"
                    : $"Optics: <b>Linear Array</b> (Aperture {probeGeometry.ApertureWidth * 1000f:F0}mm, Depth {probeGeometry.MaxDepth * 1000f:F0}mm)";

                int totalSamples = probeGeometry.ScanLines * probeGeometry.SamplesPerScanLine;

                string modeLabel = sliceRenderer.RenderMode switch
                {
                    UltrasoundRenderMode.GPU => "<b>Mode:</b> <color=#4ade80>GPU Hardware Accelerated</color>",
                    UltrasoundRenderMode.CPUReference => "<b>Mode:</b> <color=#60a5fa>CPU Reference</color>",
                    UltrasoundRenderMode.Difference => $"<b>Mode:</b> <color=#f87171>Difference Analysis</color> (Max Diff: {sliceRenderer.MaxDifference:P1}, Mean: {sliceRenderer.MeanDifference:P2})",
                    _ => "Mode: GPU"
                };

                var app = sliceRenderer.AppearanceSettings;
                string appDetails = app.Enabled
                    ? $"<b>Appearance:</b> <color=#f59e0b>{app.DebugView}</color> | Gain {app.Gain:F1}x | Atten {app.DepthAttenuation:F1}m⁻¹ | Speckle {app.SpeckleStrength:P0}"
                    : "<b>Appearance:</b> <color=#94a3b8>Disabled (Raw Grayscale)</color>";

                telemetryText.text =
                    $"{modeLabel}\n" +
                    $"{appDetails}\n" +
                    $"<b>Probe Pose:</b> ({pos.x:F1}, {pos.y:F1}, {pos.z:F1}) mm | Rot: P={rot.x:F0}° Y={rot.y:F0}° R={rot.z:F0}°\n" +
                    $"{geomDetails}\n" +
                    $"<b>Acquisition:</b> {probeGeometry.ScanLines} lines × {probeGeometry.SamplesPerScanLine} samples = <b>{totalSamples:N0} 3D samples</b>\n" +
                    $"<b>Display:</b> {sliceRenderer.SliceWidth}×{sliceRenderer.SliceHeight} ({sliceRenderer.ScanConversionFilter})";
            }
        }

        // --- Action Handlers for Buttons / Toggles ---

        public void OnClickTransverse()
        {
            if (probeController != null) probeController.SetTransverseView();
        }

        public void OnClickSagittal()
        {
            if (probeController != null) probeController.SetSagittalView();
        }

        public void OnClickCoronal()
        {
            if (probeController != null) probeController.SetCoronalView();
        }

        public void OnClickResetProbe()
        {
            if (probeController != null) probeController.ResetToHome();
        }

        public void OnToggleImagingPlane(bool enabled)
        {
            if (probeVisualizer != null) probeVisualizer.ShowImagingPlane = enabled;
        }

        public void OnToggleNormalVector(bool enabled)
        {
            if (probeVisualizer != null) probeVisualizer.ShowNormalVector = enabled;
        }

        public void OnToggleAnatomyMesh(bool enabled)
        {
            if (anatomyVisualizer != null) anatomyVisualizer.ShowBodyMesh = enabled;
        }

        public void OnChangeResolution(int resolution)
        {
            if (sliceRenderer != null)
            {
                sliceRenderer.SliceWidth = resolution;
                sliceRenderer.SliceHeight = resolution;
            }
        }
    }
}
