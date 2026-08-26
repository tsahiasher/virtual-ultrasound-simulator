using UnityEngine;
using UnityEngine.UI;
using VirtualUltrasound.Probe;
using VirtualUltrasound.Rendering;
using VirtualUltrasound.Volume;

namespace VirtualUltrasound.UI
{
    /// <summary>
    /// Manages the heads-up display, telemetry data, debug toggles, and view presets.
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
        }

        private void Update()
        {
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

            if (performanceText != null && sliceRenderer != null)
            {
                performanceText.text = $"FPS: {currentFps:F0} | Slice: {sliceRenderer.SliceWidth}x{sliceRenderer.SliceHeight} | Time: {sliceRenderer.LastRenderTimeMs:F1}ms";
            }
        }

        private void UpdateTelemetry()
        {
            if (telemetryText != null && probeGeometry != null)
            {
                Vector3 pos = probeGeometry.Origin * 1000f; // in millimeters for clinical realism
                Vector3 rot = probeGeometry.Orientation.eulerAngles;

                telemetryText.text =
                    $"<b>Probe Pose:</b>\n" +
                    $"Pos (mm): X={pos.x:F1} Y={pos.y:F1} Z={pos.z:F1}\n" +
                    $"Rot (deg): Pitch={rot.x:F1}° Yaw={rot.y:F1}° Roll={rot.z:F1}°\n" +
                    $"Aperture: {probeGeometry.ApertureWidth * 1000f:F0}mm | Depth: {probeGeometry.MaxDepth * 1000f:F0}mm";
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
