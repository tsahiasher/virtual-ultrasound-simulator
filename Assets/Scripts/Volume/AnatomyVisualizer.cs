using UnityEngine;
using VirtualUltrasound.Core;

namespace VirtualUltrasound.Volume
{
    /// <summary>
    /// Visualizes the 3D anatomy in the 3D scene using procedural meshes and wireframes.
    /// Provides clear spatial feedback showing where the probe's imaging plane intersects anatomical structures.
    /// </summary>
    [ExecuteAlways]
    public class AnatomyVisualizer : MonoBehaviour
    {
        [SerializeField] private SyntheticAnatomyVolume anatomy;
        [SerializeField] private bool showBodyMesh = true;
        [SerializeField] private bool showInternalStructures = true;
        [SerializeField] private bool showWireframes = true;

        [Header("Colors")]
        [SerializeField] private Color bodyColor = new Color(0.35f, 0.45f, 0.6f, 0.22f);
        [SerializeField] private Color organ1Color = new Color(0.85f, 0.75f, 0.35f, 0.45f);
        [SerializeField] private Color organ2Color = new Color(0.2f, 0.6f, 0.85f, 0.45f);
        [SerializeField] private Color vesselColor = new Color(0.85f, 0.25f, 0.25f, 0.50f);

        private GameObject visualRoot;

        public bool ShowBodyMesh
        {
            get => showBodyMesh;
            set { showBodyMesh = value; UpdateVisibility(); }
        }

        public bool ShowInternalStructures
        {
            get => showInternalStructures;
            set { showInternalStructures = value; UpdateVisibility(); }
        }

        private void OnEnable()
        {
            if (anatomy == null)
                anatomy = GetComponent<SyntheticAnatomyVolume>() ?? FindObjectOfType<SyntheticAnatomyVolume>();

            BuildVisuals();
        }

        private void UpdateVisibility()
        {
            if (visualRoot != null)
            {
                visualRoot.SetActive(showBodyMesh || showInternalStructures);
            }
        }

        public void BuildVisuals()
        {
            if (visualRoot != null)
            {
                if (Application.isPlaying)
                    Destroy(visualRoot);
                else
                    DestroyImmediate(visualRoot);
            }

            if (anatomy == null) return;

            visualRoot = new GameObject("Anatomy_3D_Visuals");
            visualRoot.transform.SetParent(anatomy.transform, false);

            // Create transparent material
            Material transparentMat = CreateTransparentMaterial();

            // 1. Body Ellipsoid
            GameObject bodyObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bodyObj.name = "Visual_BodyEllipsoid";
            bodyObj.transform.SetParent(visualRoot.transform, false);
            bodyObj.transform.localPosition = Vector3.zero;
            bodyObj.transform.localScale = anatomy.BodyRadii * 2f;
            RemoveCollider(bodyObj);
            ApplyMaterial(bodyObj, transparentMat, bodyColor);

            // 2. Organ 1 (Hyperechoic sphere)
            GameObject organ1Obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            organ1Obj.name = "Visual_Organ1";
            organ1Obj.transform.SetParent(visualRoot.transform, false);
            organ1Obj.transform.position = anatomy.Organ1Center;
            organ1Obj.transform.localScale = Vector3.one * (anatomy.Organ1Radius * 2f);
            RemoveCollider(organ1Obj);
            ApplyMaterial(organ1Obj, transparentMat, organ1Color);

            // 3. Organ 2 (Cyst sphere)
            GameObject organ2Obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            organ2Obj.name = "Visual_Organ2_Cyst";
            organ2Obj.transform.SetParent(visualRoot.transform, false);
            organ2Obj.transform.position = anatomy.Organ2Center;
            organ2Obj.transform.localScale = Vector3.one * (anatomy.Organ2Radius * 2f);
            RemoveCollider(organ2Obj);
            ApplyMaterial(organ2Obj, transparentMat, organ2Color);

            // 4. Vessel (Cylinder)
            Vector3 vStart = anatomy.VesselStart;
            Vector3 vEnd = anatomy.VesselEnd;
            Vector3 vAxis = vEnd - vStart;
            float length = vAxis.magnitude;

            if (length > 1e-4f)
            {
                GameObject vesselObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                vesselObj.name = "Visual_Vessel";
                vesselObj.transform.SetParent(visualRoot.transform, false);
                vesselObj.transform.position = (vStart + vEnd) * 0.5f;
                vesselObj.transform.up = vAxis.normalized;
                vesselObj.transform.localScale = new Vector3(
                    anatomy.VesselOuterRadius * 2f,
                    length * 0.5f,
                    anatomy.VesselOuterRadius * 2f
                );
                RemoveCollider(vesselObj);
                ApplyMaterial(vesselObj, transparentMat, vesselColor);
            }
        }

        private Material CreateTransparentMaterial()
        {
            Shader standardShader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            Material mat = new Material(standardShader);
            mat.SetFloat("_Mode", 3); // Transparent mode for standard shader
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            return mat;
        }

        private void ApplyMaterial(GameObject obj, Material baseMat, Color color)
        {
            MeshRenderer mr = obj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Material instanceMat = new Material(baseMat) { color = color };
                mr.sharedMaterial = instanceMat;
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
            if (!showWireframes || anatomy == null) return;

            // Draw bounding box gizmo
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.35f);
            VolumeBounds b = anatomy.WorldBounds;
            Gizmos.DrawWireCube(b.Center, b.Extents * 2f);

            // Draw Organ 1 wireframe
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(anatomy.Organ1Center, anatomy.Organ1Radius);

            // Draw Organ 2 wireframe
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
            Gizmos.DrawWireSphere(anatomy.Organ2Center, anatomy.Organ2Radius);

            // Draw vessel axis
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
            Gizmos.DrawLine(anatomy.VesselStart, anatomy.VesselEnd);
        }
    }
}
