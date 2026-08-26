using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using VirtualUltrasound.CameraControl;
using VirtualUltrasound.Probe;
using VirtualUltrasound.Rendering;
using VirtualUltrasound.UI;
using VirtualUltrasound.Volume;

namespace VirtualUltrasound.Bootstrap
{
    /// <summary>
    /// Self-contained scene bootstrapper that configures the entire virtual ultrasound simulation:
    /// 3D lighting, orbiting camera, synthetic anatomy volume, virtual probe, slice renderer,
    /// and split-screen UI HUD with zero manual scene setup required.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SceneBootstrapper : MonoBehaviour
    {
        [Header("Bootstrap Options")]
        [SerializeField] private bool autoBuildOnStart = true;

        private void Awake()
        {
            if (autoBuildOnStart)
            {
                BuildScene();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitializeOnPlay()
        {
            if (FindObjectOfType<SceneBootstrapper>() == null && FindObjectOfType<ProbeGeometry>() == null)
            {
                GameObject bootstrapperObj = new GameObject("AppBootstrapper");
                SceneBootstrapper bootstrapper = bootstrapperObj.AddComponent<SceneBootstrapper>();
                bootstrapper.BuildScene();
            }
        }

        public void BuildScene()
        {
            // 1. Lighting
            EnsureLighting();

            // 2. Event System (Required for UI buttons/toggles)
            EnsureEventSystem();

            // 3. Anatomy Volume & 3D Visualizer
            SyntheticAnatomyVolume anatomy = EnsureAnatomy();

            // 4. Volume Sampler
            ProceduralVolumeSampler sampler = EnsureSampler(anatomy);

            // 5. Probe & Visualizer
            ProbeGeometry probeGeometry = EnsureProbe();

            // 6. Slice Renderer
            SliceRenderer sliceRenderer = EnsureSliceRenderer(probeGeometry, sampler);

            // 7. 3D Scene Camera with Orbit Controller
            Camera mainCam = EnsureCamera();

            // 8. Split-screen UI Canvas
            BuildUI(sliceRenderer, probeGeometry);
        }

        private void EnsureLighting()
        {
            Light dirLight = FindObjectOfType<Light>();
            if (dirLight == null)
            {
                GameObject lightObj = new GameObject("Directional Light");
                dirLight = lightObj.AddComponent<Light>();
                dirLight.type = LightType.Directional;
                dirLight.color = new Color(0.98f, 0.98f, 1.0f);
                dirLight.intensity = 1.1f;
                lightObj.transform.rotation = Quaternion.Euler(45f, 40f, 0f);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.38f, 0.45f);
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }
        }

        private SyntheticAnatomyVolume EnsureAnatomy()
        {
            SyntheticAnatomyVolume anatomy = FindObjectOfType<SyntheticAnatomyVolume>();
            if (anatomy == null)
            {
                GameObject anatObj = new GameObject("SyntheticAnatomyVolume");
                anatObj.transform.position = Vector3.zero;
                anatomy = anatObj.AddComponent<SyntheticAnatomyVolume>();
                anatObj.AddComponent<AnatomyVisualizer>();
            }
            return anatomy;
        }

        private ProceduralVolumeSampler EnsureSampler(SyntheticAnatomyVolume anatomy)
        {
            ProceduralVolumeSampler sampler = FindObjectOfType<ProceduralVolumeSampler>();
            if (sampler == null)
            {
                sampler = anatomy.gameObject.AddComponent<ProceduralVolumeSampler>();
                sampler.AnatomyVolume = anatomy;
            }
            return sampler;
        }

        private ProbeGeometry EnsureProbe()
        {
            ProbeGeometry probe = FindObjectOfType<ProbeGeometry>();
            if (probe == null)
            {
                GameObject probeObj = new GameObject("VirtualUltrasoundProbe");
                probeObj.transform.position = new Vector3(0f, 0.14f, 0f);
                probeObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                probe = probeObj.AddComponent<ProbeGeometry>();
                probeObj.AddComponent<ProbeController>();
                probeObj.AddComponent<ProbeVisualizer>();
            }
            return probe;
        }

        private SliceRenderer EnsureSliceRenderer(ProbeGeometry probe, ProceduralVolumeSampler sampler)
        {
            SliceRenderer renderer = FindObjectOfType<SliceRenderer>();
            if (renderer == null)
            {
                GameObject rendObj = new GameObject("SliceRenderer");
                renderer = rendObj.AddComponent<SliceRenderer>();
                renderer.SetProbeGeometry(probe);
                renderer.SetVolumeSampler(sampler);
                renderer.SliceWidth = 128;
                renderer.SliceHeight = 128;
            }
            return renderer;
        }

        private Camera EnsureCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                cam = FindObjectOfType<Camera>();
            }

            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                cam = camObj.AddComponent<Camera>();
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f); // Sleek modern dark slate background
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;

            // Configure 3D viewport to occupy left 62% of the screen
            cam.rect = new Rect(0f, 0f, 0.62f, 1f);

            SceneCameraController camCtrl = cam.GetComponent<SceneCameraController>();
            if (camCtrl == null)
            {
                camCtrl = cam.gameObject.AddComponent<SceneCameraController>();
            }

            return cam;
        }

        private void BuildUI(SliceRenderer sliceRenderer, ProbeGeometry probe)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null) return; // UI already exists

            GameObject canvasObj = new GameObject("UI_SplitScreenCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            UIController uiController = canvasObj.AddComponent<UIController>();

            Font defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // --- 1. Right Ultrasound Panel (38% width, right aligned) ---
            GameObject usPanel = new GameObject("UltrasoundPanel");
            usPanel.transform.SetParent(canvas.transform, false);
            RectTransform usRect = usPanel.AddComponent<RectTransform>();
            usRect.anchorMin = new Vector2(0.62f, 0f);
            usRect.anchorMax = new Vector2(1f, 1f);
            usRect.offsetMin = Vector2.zero;
            usRect.offsetMax = Vector2.zero;

            Image usPanelBg = usPanel.AddComponent<Image>();
            usPanelBg.color = new Color(0.04f, 0.05f, 0.07f, 1.0f); // Deep dark ultrasound monitor bezel

            // Title
            GameObject titleObj = CreateText("PanelTitle", usPanel.transform, "LIVE 2D ULTRASOUND VIEW", 18, FontStyle.Bold, TextAnchor.MiddleCenter, defaultFont);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.94f);
            titleRect.anchorMax = new Vector2(1f, 0.99f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            // Ultrasound RawImage Frame
            GameObject usFrame = new GameObject("UltrasoundFrame");
            usFrame.transform.SetParent(usPanel.transform, false);
            RectTransform frameRect = usFrame.AddComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0.08f, 0.28f);
            frameRect.anchorMax = new Vector2(0.92f, 0.92f);
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;

            Image frameBorder = usFrame.AddComponent<Image>();
            frameBorder.color = new Color(0.15f, 0.20f, 0.28f, 1.0f);

            // RawImage
            GameObject rawImgObj = new GameObject("UltrasoundRawImage");
            rawImgObj.transform.SetParent(usFrame.transform, false);
            RectTransform rawRect = rawImgObj.AddComponent<RectTransform>();
            rawRect.anchorMin = new Vector2(0.02f, 0.02f);
            rawRect.anchorMax = new Vector2(0.98f, 0.98f);
            rawRect.offsetMin = Vector2.zero;
            rawRect.offsetMax = Vector2.zero;

            RawImage rawImage = rawImgObj.AddComponent<RawImage>();
            rawImage.color = Color.white;
            AspectRatioFitter fitter = rawImgObj.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;

            UltrasoundDisplay usDisplay = rawImgObj.AddComponent<UltrasoundDisplay>();
            usDisplay.BindSliceRenderer(sliceRenderer);

            // Orientation marker dot (Top-left of US frame)
            GameObject markerDot = new GameObject("OrientationMarkerDot");
            markerDot.transform.SetParent(rawImgObj.transform, false);
            RectTransform dotRect = markerDot.AddComponent<RectTransform>();
            dotRect.anchorMin = new Vector2(0.04f, 0.92f);
            dotRect.anchorMax = new Vector2(0.08f, 0.96f);
            dotRect.offsetMin = Vector2.zero;
            dotRect.offsetMax = Vector2.zero;
            Image dotImg = markerDot.AddComponent<Image>();
            dotImg.color = new Color(0.95f, 0.35f, 0.2f, 1.0f);

            // Telemetry Text
            GameObject telemetryObj = CreateText("TelemetryText", usPanel.transform, "Probe Pose: Loading...", 14, FontStyle.Normal, TextAnchor.UpperLeft, defaultFont);
            RectTransform telRect = telemetryObj.GetComponent<RectTransform>();
            telRect.anchorMin = new Vector2(0.08f, 0.14f);
            telRect.anchorMax = new Vector2(0.92f, 0.26f);
            telRect.offsetMin = Vector2.zero;
            telRect.offsetMax = Vector2.zero;

            // Performance Text
            GameObject perfObj = CreateText("PerformanceText", usPanel.transform, "FPS: 60 | 128x128", 13, FontStyle.Italic, TextAnchor.LowerRight, defaultFont);
            RectTransform perfRect = perfObj.GetComponent<RectTransform>();
            perfRect.anchorMin = new Vector2(0.08f, 0.01f);
            perfRect.anchorMax = new Vector2(0.92f, 0.05f);
            perfRect.offsetMin = Vector2.zero;
            perfRect.offsetMax = Vector2.zero;

            // --- 2. Left HUD Overlay (3D View Controls & View Presets) ---
            GameObject leftHud = new GameObject("LeftHUD");
            leftHud.transform.SetParent(canvas.transform, false);
            RectTransform leftRect = leftHud.AddComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0f, 0f);
            leftRect.anchorMax = new Vector2(0.62f, 1f);
            leftRect.offsetMin = Vector2.zero;
            leftRect.offsetMax = Vector2.zero;

            // App Title Banner
            GameObject headerObj = CreateText("AppTitle", leftHud.transform, "<b>VIRTUAL ULTRASOUND SIMULATOR</b>", 20, FontStyle.Bold, TextAnchor.UpperLeft, defaultFont);
            RectTransform headRect = headerObj.GetComponent<RectTransform>();
            headRect.anchorMin = new Vector2(0.03f, 0.92f);
            headRect.anchorMax = new Vector2(0.95f, 0.98f);
            headRect.offsetMin = Vector2.zero;
            headRect.offsetMax = Vector2.zero;

            // Controls Guide Box
            string controlsString =
                "<b>Interactive Controls:</b>\n" +
                "• <b>Translate Probe:</b> <b>W/S</b> (Depth), <b>A/D</b> (Lateral), <b>Q/E</b> (Elevation) [Hold <b>Shift</b>: Fast]\n" +
                "• <b>Rotate Probe:</b> <b>I/K</b> (Pitch), <b>J/L</b> (Yaw), <b>U/O</b> (Roll)\n" +
                "• <b>Preset Views:</b> <b>1</b> Transverse | <b>2</b> Sagittal | <b>3</b> Coronal | <b>R</b> Reset Home\n" +
                "• <b>3D Scene Camera:</b> <b>Right-Click Drag</b> (Orbit), <b>Middle-Click Drag</b> (Pan), <b>Scroll</b> (Zoom)";

            GameObject guideObj = CreateText("ControlsGuide", leftHud.transform, controlsString, 13, FontStyle.Normal, TextAnchor.LowerLeft, defaultFont);
            RectTransform guideRect = guideObj.GetComponent<RectTransform>();
            guideRect.anchorMin = new Vector2(0.03f, 0.02f);
            guideRect.anchorMax = new Vector2(0.95f, 0.16f);
            guideRect.offsetMin = Vector2.zero;
            guideRect.offsetMax = Vector2.zero;

            // Bind UIController references
            uiController.FindReferences();
        }

        private GameObject CreateText(string name, Transform parent, string text, int fontSize, FontStyle style, TextAnchor alignment, Font font)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            Text t = obj.AddComponent<Text>();
            t.text = text;
            t.font = font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = alignment;
            t.color = new Color(0.90f, 0.93f, 0.98f, 1.0f);
            t.supportRichText = true;
            return obj;
        }
    }
}
