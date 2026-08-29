using System.Collections.Generic;
using UnityEngine;
using VirtualUltrasound.Core;

namespace VirtualUltrasound.Probe
{
    /// <summary>
    /// Visualizes the ultrasound probe body, sector fan / imaging plane mesh, wireframe perimeter, and normal vector in 3D.
    /// Provides clear visual verification that the ultrasound field of view moves rigidly with the probe.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(ProbeGeometry))]
    public class ProbeVisualizer : MonoBehaviour
    {
        [Header("Visualization Toggles")]
        [SerializeField] private bool showProbeBody = true;
        [SerializeField] private bool showImagingPlane = true;
        [SerializeField] private bool showPlaneBorder = true;
        [SerializeField] private bool showNormalVector = true;
        [SerializeField] private bool showBeamArrow = true;

        [Header("Colors")]
        [SerializeField] private Color planeColor = new Color(0.1f, 0.85f, 0.95f, 0.35f);
        [SerializeField] private Color borderColor = new Color(0.2f, 1.0f, 0.8f, 0.95f);
        [SerializeField] private Color normalColor = new Color(1.0f, 0.9f, 0.1f, 0.9f);
        [SerializeField] private Color beamColor = new Color(0.2f, 0.6f, 1.0f, 0.9f);
        [SerializeField] private Color probeBodyColor = new Color(0.85f, 0.88f, 0.92f, 1.0f);
        [SerializeField] private Color markerDotColor = new Color(0.95f, 0.3f, 0.2f, 1.0f);

        private ProbeGeometry probeGeometry;
        private GameObject visualRoot;
        private MeshFilter planeMeshFilter;
        private MeshRenderer planeMeshRenderer;
        private LineRenderer borderLineRenderer;

        // Reusable mesh generation buffers (zero allocations in update)
        private List<Vector3> meshVertices = new List<Vector3>(64);
        private List<int> meshTriangles = new List<int>(256);
        private List<Vector2> meshUvs = new List<Vector2>(64);
        private List<Vector3> borderPoints = new List<Vector3>(64);

        public bool ShowImagingPlane
        {
            get => showImagingPlane;
            set
            {
                showImagingPlane = value;
                if (planeMeshRenderer != null) planeMeshRenderer.enabled = showImagingPlane;
            }
        }

        public bool ShowPlaneBorder
        {
            get => showPlaneBorder;
            set
            {
                showPlaneBorder = value;
                if (borderLineRenderer != null) borderLineRenderer.enabled = showPlaneBorder;
            }
        }

        public bool ShowNormalVector
        {
            get => showNormalVector;
            set => showNormalVector = value;
        }

        private void OnEnable()
        {
            probeGeometry = GetComponent<ProbeGeometry>();
            if (probeGeometry != null)
            {
                probeGeometry.OnGeometryChanged += UpdatePlaneMesh;
            }
            BuildVisuals();
        }

        private void OnDisable()
        {
            if (probeGeometry != null)
            {
                probeGeometry.OnGeometryChanged -= UpdatePlaneMesh;
            }
        }

        private void Update()
        {
            if (visualRoot == null)
            {
                BuildVisuals();
            }
        }

        public void BuildVisuals()
        {
            if (visualRoot != null)
            {
                if (Application.isPlaying) Destroy(visualRoot);
                else DestroyImmediate(visualRoot);
            }

            visualRoot = new GameObject("Probe_Visuals");
            visualRoot.transform.SetParent(transform, false);

            if (showProbeBody)
            {
                BuildProbeBody();
            }

            BuildImagingPlane();
        }

        private void BuildProbeBody()
        {
            GameObject bodyRoot = new GameObject("ProbeBody");
            bodyRoot.transform.SetParent(visualRoot.transform, false);

            Material bodyMat = CreateOpaqueMaterial(probeBodyColor);
            Material notchMat = CreateOpaqueMaterial(markerDotColor);

            float headWidth = probeGeometry.Type == ProbeType.Linear
                ? probeGeometry.ApertureWidth * 1.1f
                : 2f * probeGeometry.ConvexRadius * Mathf.Sin(probeGeometry.SectorAngleDegrees * 0.5f * Mathf.Deg2Rad) * 1.15f;

            // 1. Transducer Head / Acoustic lens (box near apex)
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "TransducerHead";
            head.transform.SetParent(bodyRoot.transform, false);
            head.transform.localPosition = new Vector3(0f, 0f, -0.015f);
            head.transform.localScale = new Vector3(Mathf.Max(0.04f, headWidth), 0.022f, 0.030f);
            RemoveCollider(head);
            ApplyMaterial(head, bodyMat);

            // 2. Handle grip (cylinder extending backward)
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "TransducerHandle";
            handle.transform.SetParent(bodyRoot.transform, false);
            handle.transform.localPosition = new Vector3(0f, 0f, -0.075f);
            handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            handle.transform.localScale = new Vector3(0.025f, 0.045f, 0.025f);
            RemoveCollider(handle);
            ApplyMaterial(handle, bodyMat);

            // 3. Orientation Notch / Marker Dot (indicates Left side of image / index mark)
            GameObject notch = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            notch.name = "OrientationMarkerDot";
            notch.transform.SetParent(bodyRoot.transform, false);
            notch.transform.localPosition = new Vector3(-headWidth * 0.45f, 0.012f, -0.010f);
            notch.transform.localScale = Vector3.one * 0.007f;
            RemoveCollider(notch);
            ApplyMaterial(notch, notchMat);
        }

        private void BuildImagingPlane()
        {
            GameObject planeObj = new GameObject("ImagingPlaneQuad");
            planeObj.transform.SetParent(visualRoot.transform, false);

            planeMeshFilter = planeObj.AddComponent<MeshFilter>();
            planeMeshRenderer = planeObj.AddComponent<MeshRenderer>();
            planeMeshRenderer.sharedMaterial = CreateDoubleSidedTransparentMaterial(planeColor);
            planeMeshRenderer.enabled = showImagingPlane;

            // Border Line Renderer
            borderLineRenderer = planeObj.AddComponent<LineRenderer>();
            borderLineRenderer.useWorldSpace = false;
            borderLineRenderer.loop = true;
            borderLineRenderer.startWidth = 0.002f;
            borderLineRenderer.endWidth = 0.002f;
            borderLineRenderer.sharedMaterial = CreateUnlitMaterial(borderColor);
            borderLineRenderer.enabled = showPlaneBorder;

            UpdatePlaneMesh();
        }

        private void UpdatePlaneMesh()
        {
            if (planeMeshFilter == null || probeGeometry == null) return;

            Mesh mesh = planeMeshFilter.sharedMesh;
            if (mesh == null)
            {
                mesh = new Mesh { name = "ImagingPlaneMesh" };
                planeMeshFilter.sharedMesh = mesh;
            }

            meshVertices.Clear();
            meshTriangles.Clear();
            meshUvs.Clear();
            borderPoints.Clear();

            int arcSegments = 24; // 24 segments for smooth fan curvature

            probeGeometry.GetSectorBoundaryPoints(arcSegments, out Vector3[] topArc, out Vector3[] bottomArc);

            // Generate quad strips between topArc and bottomArc
            // Vertices: [0..arcSegments-1] = topArc, [arcSegments..2*arcSegments-1] = bottomArc
            for (int i = 0; i < arcSegments; i++)
            {
                meshVertices.Add(topArc[i]);
                meshUvs.Add(new Vector2((float)i / (arcSegments - 1), 0f));
            }
            for (int i = 0; i < arcSegments; i++)
            {
                meshVertices.Add(bottomArc[i]);
                meshUvs.Add(new Vector2((float)i / (arcSegments - 1), 1f));
            }

            // Triangulate sector strip
            for (int i = 0; i < arcSegments - 1; i++)
            {
                int t0 = i;
                int t1 = i + 1;
                int b0 = arcSegments + i;
                int b1 = arcSegments + i + 1;

                // Front face
                meshTriangles.Add(t0);
                meshTriangles.Add(t1);
                meshTriangles.Add(b0);

                meshTriangles.Add(t1);
                meshTriangles.Add(b1);
                meshTriangles.Add(b0);

                // Back face (double-sided)
                meshTriangles.Add(b0);
                meshTriangles.Add(t1);
                meshTriangles.Add(t0);

                meshTriangles.Add(b0);
                meshTriangles.Add(b1);
                meshTriangles.Add(t1);
            }

            mesh.Clear();
            mesh.SetVertices(meshVertices);
            mesh.SetTriangles(meshTriangles, 0);
            mesh.SetUVs(0, meshUvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // Trace perimeter loop for LineRenderer: topArc (L->R) -> bottomArc (R->L)
            if (borderLineRenderer != null)
            {
                for (int i = 0; i < arcSegments; i++)
                {
                    borderPoints.Add(topArc[i]);
                }
                for (int i = arcSegments - 1; i >= 0; i--)
                {
                    borderPoints.Add(bottomArc[i]);
                }

                borderLineRenderer.positionCount = borderPoints.Count;
                borderLineRenderer.SetPositions(borderPoints.ToArray());
            }
        }

        private Material CreateOpaqueMaterial(Color color)
        {
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default") ?? Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            return mat;
        }

        private Material CreateUnlitMaterial(Color color)
        {
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            return mat;
        }

        private Material CreateDoubleSidedTransparentMaterial(Color color)
        {
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.mainTexture = Texture2D.whiteTexture;
            mat.color = color;
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            mat.renderQueue = 3050;
            return mat;
        }

        private void ApplyMaterial(GameObject obj, Material mat)
        {
            MeshRenderer mr = obj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
            }
        }

        private void RemoveCollider(GameObject obj)
        {
            Component col = obj.GetComponent("Collider");
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
        }

        private void OnDrawGizmos()
        {
            if (probeGeometry == null) probeGeometry = GetComponent<ProbeGeometry>();
            if (probeGeometry == null) return;

            Vector3 origin = transform.position;

            // Draw Normal vector (Elevation Axis Y_P)
            if (showNormalVector)
            {
                Gizmos.color = normalColor;
                Gizmos.DrawRay(origin, probeGeometry.ImagingPlaneNormal * 0.05f);
                Gizmos.DrawSphere(origin + probeGeometry.ImagingPlaneNormal * 0.05f, 0.003f);
            }

            // Draw Beam propagation vector (Axial Depth Axis Z_P)
            if (showBeamArrow)
            {
                Gizmos.color = beamColor;
                Gizmos.DrawRay(origin, probeGeometry.BeamDirection * probeGeometry.MaxDepth);
            }

            // Draw Sector Scanline rays in Gizmos
            if (probeGeometry.Type == ProbeType.Curvilinear)
            {
                Gizmos.color = new Color(0.2f, 0.9f, 0.9f, 0.35f);
                int rayCount = 7;
                for (int i = 0; i < rayCount; i++)
                {
                    int lineIdx = (int)Mathf.Lerp(0, probeGeometry.ScanLines - 1, (float)i / (rayCount - 1));
                    probeGeometry.GetScanLineRayWorldSpace(lineIdx, out Vector3 rOrigin, out Vector3 rDir);
                    Gizmos.DrawRay(rOrigin, rDir * probeGeometry.MaxDepth);
                }
            }
        }
    }
}
