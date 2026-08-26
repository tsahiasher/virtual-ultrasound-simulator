using UnityEngine;

namespace VirtualUltrasound.CameraControl
{
    /// <summary>
    /// Smooth orbit, pan, and zoom 3D scene camera controller.
    /// Operates independently of probe controls.
    /// </summary>
    public class SceneCameraController : MonoBehaviour
    {
        [Header("Target & Distance")]
        [SerializeField] private Vector3 targetPivot = Vector3.zero;
        [SerializeField] private float distance = 0.45f;
        [SerializeField] private float minDistance = 0.10f;
        [SerializeField] private float maxDistance = 1.20f;

        [Header("Orbit Angles")]
        [SerializeField] private float yaw = 35f;
        [SerializeField] private float pitch = 25f;
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        [Header("Sensitivity")]
        [SerializeField] private float orbitSensitivity = 3.5f;
        [SerializeField] private float zoomSensitivity = 0.08f;
        [SerializeField] private float panSensitivity = 0.003f;
        [SerializeField] private float smoothFactor = 12f;

        private float targetYaw;
        private float targetPitch;
        private float targetDistance;
        private Vector3 currentPivot;
        private Vector3 targetPivotPos;

        public Vector3 TargetPivot
        {
            get => targetPivot;
            set
            {
                targetPivot = value;
                targetPivotPos = value;
            }
        }

        private void Start()
        {
            targetYaw = yaw;
            targetPitch = pitch;
            targetDistance = distance;
            currentPivot = targetPivot;
            targetPivotPos = targetPivot;
            UpdateCameraTransform(true);
        }

        private void LateUpdate()
        {
            HandleInput();
            UpdateCameraTransform(false);
        }

        private void HandleInput()
        {
            // 1. Orbit (Right Mouse Button or Left Mouse Button when not clicking UI)
            if (Input.GetMouseButton(1) || (Input.GetMouseButton(0) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))))
            {
                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");

                targetYaw += mouseX * orbitSensitivity;
                targetPitch -= mouseY * orbitSensitivity;
                targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
            }

            // 2. Pan (Middle Mouse Button or Shift + Right Mouse Button)
            if (Input.GetMouseButton(2) || (Input.GetMouseButton(1) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))))
            {
                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");

                Vector3 right = transform.right;
                Vector3 up = transform.up;
                targetPivotPos -= (right * mouseX + up * mouseY) * (panSensitivity * targetDistance);
            }

            // 3. Zoom (Mouse Scroll Wheel)
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 1e-4f)
            {
                targetDistance -= scroll * (zoomSensitivity * targetDistance);
                targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
            }

            // 4. Focus target shortcut (F key)
            if (Input.GetKeyDown(KeyCode.F))
            {
                targetPivotPos = Vector3.zero;
                targetDistance = 0.45f;
            }
        }

        private void UpdateCameraTransform(bool instant)
        {
            if (instant)
            {
                yaw = targetYaw;
                pitch = targetPitch;
                distance = targetDistance;
                currentPivot = targetPivotPos;
            }
            else
            {
                float dt = Time.deltaTime * smoothFactor;
                yaw = Mathf.Lerp(yaw, targetYaw, dt);
                pitch = Mathf.Lerp(pitch, targetPitch, dt);
                distance = Mathf.Lerp(distance, targetDistance, dt);
                currentPivot = Vector3.Lerp(currentPivot, targetPivotPos, dt);
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 offset = rotation * new Vector3(0f, 0f, -distance);
            transform.position = currentPivot + offset;
            transform.rotation = rotation;
        }
    }
}
