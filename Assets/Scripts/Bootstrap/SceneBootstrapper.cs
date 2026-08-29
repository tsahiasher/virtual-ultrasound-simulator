using System;
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
    /// 3D lighting, background clearer, orbiting camera, synthetic anatomy volume, virtual probe,
    /// slice renderer, and split-screen UI HUD with zero manual scene setup required.
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
            SceneBootstrapper bootstrapper = FindObjectOfType<SceneBootstrapper>();
            if (bootstrapper == null && FindObjectOfType<ProbeGeometry>() == null)
            {
                GameObject bootstrapperObj = new GameObject("AppBootstrapper");
                bootstrapper = bootstrapperObj.AddComponent<SceneBootstrapper>();
            }

            if (bootstrapper != null)
            {
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

            // 7. Cameras (Background Clearer + Main 3D Viewport)
            EnsureCameras();

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
            RenderSettings.ambientLight = new Color(0.40f, 0.44f, 0.52f);
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

            if (anatomy.GetComponent<GPUVolumeData>() == null)
            {
                anatomy.gameObject.AddComponent<GPUVolumeData>();
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
                probe.SamplesPerScanLine = 1024;
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
                renderer.SliceWidth = 512;
                renderer.SliceHeight = 512;
            }

            GPUVolumeData gpuVol = FindObjectOfType<GPUVolumeData>();
            if (gpuVol != null)
            {
                renderer.SetGPUVolumeData(gpuVol);
            }

            ComputeShader cs = Resources.Load<ComputeShader>("UltrasoundPipeline");
            if (cs != null)
            {
                renderer.SetComputeShader(cs);
            }

            return renderer;
        }

        private void EnsureCameras()
        {
            // 1. Background Camera (clears entire screen to dark slate so UI/right-side is never un-cleared black)
            Camera bgCam = null;
            GameObject bgCamObj = GameObject.Find("Background_Camera");
            if (bgCamObj == null)
            {
                bgCamObj = new GameObject("Background_Camera");
                bgCam = bgCamObj.AddComponent<Camera>();
            }
            else
            {
                bgCam = bgCamObj.GetComponent<Camera>();
            }

            bgCam.depth = -10;
            bgCam.clearFlags = CameraClearFlags.SolidColor;
            bgCam.backgroundColor = new Color(0.06f, 0.08f, 0.11f, 1f); // Dark slate monitor bezel color
            bgCam.cullingMask = 0; // Render nothing, just clear full screen
            bgCam.rect = new Rect(0f, 0f, 1f, 1f);

            // 2. Main 3D Viewport Camera (renders 3D scene on left 62%)
            Camera cam = Camera.main;
            if (cam == null)
            {
                Camera[] cams = FindObjectsOfType<Camera>();
                foreach (var c in cams)
                {
                    if (c != bgCam) { cam = c; break; }
                }
            }

            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                cam = camObj.AddComponent<Camera>();
            }

            cam.depth = 0;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.11f, 0.14f, 0.19f, 1f); // Modern dark slate 3D viewport
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;
            cam.rect = new Rect(0f, 0f, 0.62f, 1f);

            SceneCameraController camCtrl = cam.GetComponent<SceneCameraController>();
            if (camCtrl == null)
            {
                camCtrl = cam.gameObject.AddComponent<SceneCameraController>();
            }
        }

        private static Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Segoe UI", 14);
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Tahoma", 14);
            if (font == null)
            {
                Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();
                if (fonts != null && fonts.Length > 0) font = fonts[0];
            }
            return font;
        }

        private void BuildUI(SliceRenderer sliceRenderer, ProbeGeometry probe)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null && canvas.transform.Find("UltrasoundPanel") != null)
            {
                return; // Complete UI already exists
            }

            GameObject canvasObj;
            if (canvas == null)
            {
                canvasObj = new GameObject("UI_SplitScreenCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
                canvasObj.AddComponent<UIController>();
            }
            else
            {
                canvasObj = canvas.gameObject;
            }

            UIController uiController = canvasObj.GetComponent<UIController>() ?? canvasObj.AddComponent<UIController>();
            Font defaultFont = GetDefaultFont();

            // --- 1. Right Ultrasound Panel (38% width, right aligned) ---
            Transform existingUsPanel = canvas.transform.Find("UltrasoundPanel");
            if (existingUsPanel != null) DestroyImmediate(existingUsPanel.gameObject);

            GameObject usPanel = new GameObject("UltrasoundPanel");
            usPanel.transform.SetParent(canvas.transform, false);
            RectTransform usRect = usPanel.AddComponent<RectTransform>();
            usRect.anchorMin = new Vector2(0.62f, 0f);
            usRect.anchorMax = new Vector2(1f, 1f);
            usRect.offsetMin = Vector2.zero;
            usRect.offsetMax = Vector2.zero;

            Image usPanelBg = usPanel.AddComponent<Image>();
            usPanelBg.color = new Color(0.07f, 0.09f, 0.13f, 1.0f); // Sleek medical ultrasound console frame

            // Title Banner Box
            GameObject titleBox = new GameObject("TitleBox");
            titleBox.transform.SetParent(usPanel.transform, false);
            RectTransform titleBoxRect = titleBox.AddComponent<RectTransform>();
            titleBoxRect.anchorMin = new Vector2(0.05f, 0.93f);
            titleBoxRect.anchorMax = new Vector2(0.95f, 0.985f);
            titleBoxRect.offsetMin = Vector2.zero;
            titleBoxRect.offsetMax = Vector2.zero;
            Image titleBoxBg = titleBox.AddComponent<Image>();
            titleBoxBg.color = new Color(0.12f, 0.17f, 0.24f, 1.0f);

            GameObject titleObj = CreateText("PanelTitle", titleBox.transform, "LIVE 2D ULTRASOUND VIEW", 16, FontStyle.Bold, TextAnchor.MiddleCenter, defaultFont);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            // Ultrasound Display Frame (Medical CRT Border)
            GameObject usFrame = new GameObject("UltrasoundFrame");
            usFrame.transform.SetParent(usPanel.transform, false);
            RectTransform frameRect = usFrame.AddComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0.06f, 0.32f);
            frameRect.anchorMax = new Vector2(0.94f, 0.91f);
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;

            Image frameBorder = usFrame.AddComponent<Image>();
            frameBorder.color = new Color(0.16f, 0.22f, 0.30f, 1.0f); // Bezel

            // Screen Inner Dark Background
            GameObject screenInner = new GameObject("ScreenInner");
            screenInner.transform.SetParent(usFrame.transform, false);
            RectTransform innerRect = screenInner.AddComponent<RectTransform>();
            innerRect.anchorMin = new Vector2(0.015f, 0.015f);
            innerRect.anchorMax = new Vector2(0.985f, 0.985f);
            innerRect.offsetMin = Vector2.zero;
            innerRect.offsetMax = Vector2.zero;
            Image innerBg = screenInner.AddComponent<Image>();
            innerBg.color = new Color(0.02f, 0.02f, 0.03f, 1.0f); // Screen cavity

            // Ultrasound RawImage
            GameObject rawImgObj = new GameObject("UltrasoundRawImage");
            rawImgObj.transform.SetParent(screenInner.transform, false);
            RectTransform rawRect = rawImgObj.AddComponent<RectTransform>();
            rawRect.anchorMin = Vector2.zero;
            rawRect.anchorMax = Vector2.one;
            rawRect.offsetMin = Vector2.zero;
            rawRect.offsetMax = Vector2.zero;

            RawImage rawImage = rawImgObj.AddComponent<RawImage>();
            rawImage.color = Color.white;
            AspectRatioFitter fitter = rawImgObj.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;

            UltrasoundDisplay usDisplay = rawImgObj.AddComponent<UltrasoundDisplay>();
            usDisplay.BindSliceRenderer(sliceRenderer);

            // Orientation Marker Dot (Top-Left of US frame)
            GameObject markerDot = new GameObject("OrientationMarkerDot");
            markerDot.transform.SetParent(rawImgObj.transform, false);
            RectTransform dotRect = markerDot.AddComponent<RectTransform>();
            dotRect.anchorMin = new Vector2(0.04f, 0.92f);
            dotRect.anchorMax = new Vector2(0.09f, 0.97f);
            dotRect.offsetMin = Vector2.zero;
            dotRect.offsetMax = Vector2.zero;
            Image dotImg = markerDot.AddComponent<Image>();
            dotImg.color = new Color(0.95f, 0.35f, 0.2f, 1.0f);

            // Telemetry Box
            GameObject telBox = new GameObject("TelemetryBox");
            telBox.transform.SetParent(usPanel.transform, false);
            RectTransform telBoxRect = telBox.AddComponent<RectTransform>();
            telBoxRect.anchorMin = new Vector2(0.06f, 0.12f);
            telBoxRect.anchorMax = new Vector2(0.94f, 0.30f);
            telBoxRect.offsetMin = Vector2.zero;
            telBoxRect.offsetMax = Vector2.zero;
            Image telBoxBg = telBox.AddComponent<Image>();
            telBoxBg.color = new Color(0.11f, 0.15f, 0.21f, 1.0f);

            GameObject telemetryObj = CreateText("TelemetryText", telBox.transform, "Probe Pose: Initializing...", 13, FontStyle.Normal, TextAnchor.UpperLeft, defaultFont);
            RectTransform telRect = telemetryObj.GetComponent<RectTransform>();
            telRect.anchorMin = new Vector2(0.04f, 0.05f);
            telRect.anchorMax = new Vector2(0.96f, 0.95f);
            telRect.offsetMin = Vector2.zero;
            telRect.offsetMax = Vector2.zero;

            // Performance Text
            GameObject perfObj = CreateText("PerformanceText", usPanel.transform, "FPS: 60 | 128x128", 12, FontStyle.Italic, TextAnchor.MiddleRight, defaultFont);
            RectTransform perfRect = perfObj.GetComponent<RectTransform>();
            perfRect.anchorMin = new Vector2(0.06f, 0.02f);
            perfRect.anchorMax = new Vector2(0.94f, 0.08f);
            perfRect.offsetMin = Vector2.zero;
            perfRect.offsetMax = Vector2.zero;

            // --- 2. Left HUD Overlay (3D View Controls Guide) ---
            Transform existingLeftHud = canvas.transform.Find("LeftHUD");
            if (existingLeftHud != null) DestroyImmediate(existingLeftHud.gameObject);

            GameObject leftHud = new GameObject("LeftHUD");
            leftHud.transform.SetParent(canvas.transform, false);
            RectTransform leftRect = leftHud.AddComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0f, 0f);
            leftRect.anchorMax = new Vector2(0.62f, 1f);
            leftRect.offsetMin = Vector2.zero;
            leftRect.offsetMax = Vector2.zero;

            // App Title Banner
            GameObject titleBanner = new GameObject("TitleBanner");
            titleBanner.transform.SetParent(leftHud.transform, false);
            RectTransform bannerRect = titleBanner.AddComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0.03f, 0.92f);
            bannerRect.anchorMax = new Vector2(0.65f, 0.98f);
            bannerRect.offsetMin = Vector2.zero;
            bannerRect.offsetMax = Vector2.zero;
            Image bannerBg = titleBanner.AddComponent<Image>();
            bannerBg.color = new Color(0.08f, 0.11f, 0.16f, 0.85f);

            GameObject headerObj = CreateText("AppTitle", titleBanner.transform, "  <b>VIRTUAL ULTRASOUND SIMULATOR</b>", 18, FontStyle.Bold, TextAnchor.MiddleLeft, defaultFont);
            RectTransform headRect = headerObj.GetComponent<RectTransform>();
            headRect.anchorMin = Vector2.zero;
            headRect.anchorMax = Vector2.one;
            headRect.offsetMin = Vector2.zero;
            headRect.offsetMax = Vector2.zero;

            // Controls Guide Box
            GameObject guideBox = new GameObject("GuideBox");
            guideBox.transform.SetParent(leftHud.transform, false);
            RectTransform guideBoxRect = guideBox.AddComponent<RectTransform>();
            guideBoxRect.anchorMin = new Vector2(0.03f, 0.02f);
            guideBoxRect.anchorMax = new Vector2(0.97f, 0.17f);
            guideBoxRect.offsetMin = Vector2.zero;
            guideBoxRect.offsetMax = Vector2.zero;
            Image guideBg = guideBox.AddComponent<Image>();
            guideBg.color = new Color(0.07f, 0.09f, 0.14f, 0.88f);

            string controlsString =
                "<b>Interactive Controls:</b>\n" +
                "• <b>Translate Probe:</b> <b>W/S</b> (Depth), <b>A/D</b> (Lateral), <b>Q/E</b> (Elevation) [Hold <b>Shift</b>: Fast]\n" +
                "• <b>Rotate Probe:</b> <b>I/K</b> (Pitch), <b>J/L</b> (Yaw), <b>U/O</b> (Roll) | <b>1/2/3</b> Views | <b>R</b> Reset\n" +
                "• <b>Pipeline & Optics:</b> <b>M</b> (GPU/CPU/Diff) | <b>V</b> (Debug View) | <b>T</b> (Probe Type) | <b>+/-</b> (Depth) | <b>[ / ]</b> (FOV)\n" +
                "• <b>3D Viewport Camera:</b> <b>Right-Click Drag</b> (Orbit), <b>Middle-Click Drag</b> (Pan), <b>Scroll</b> (Zoom)";

            GameObject guideObj = CreateText("ControlsGuide", guideBox.transform, controlsString, 13, FontStyle.Normal, TextAnchor.MiddleLeft, defaultFont);
            RectTransform guideRect = guideObj.GetComponent<RectTransform>();
            guideRect.anchorMin = new Vector2(0.02f, 0.05f);
            guideRect.anchorMax = new Vector2(0.98f, 0.95f);
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
            t.font = font ?? GetDefaultFont();
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = alignment;
            t.color = new Color(0.92f, 0.95f, 1.0f, 1.0f);
            t.supportRichText = true;
            return obj;
        }
    }
}
