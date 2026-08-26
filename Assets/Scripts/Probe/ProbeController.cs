using System;
using UnityEngine;

namespace VirtualUltrasound.Probe
{
    /// <summary>
    /// Isolated controller component managing user input and rigid transform updates for the virtual probe.
    /// Decoupled from rendering and geometry math so input sources (mouse, keyboard, spatial tracker, VR)
    /// can be replaced seamlessly.
    /// </summary>
    public class ProbeController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Linear translation speed in meters per second.")]
        [SerializeField] private float moveSpeed = 0.08f;
        [Tooltip("Speed multiplier when holding Shift.")]
        [SerializeField] private float sprintMultiplier = 2.5f;
        [Tooltip("Angular rotation speed in degrees per second.")]
        [SerializeField] private float rotateSpeed = 65f;
        [Tooltip("Whether to use smooth interpolation for probe movement.")]
        [SerializeField] private bool smoothMotion = true;
        [SerializeField] private float smoothFactor = 15f;

        [Header("Initial / Home Pose")]
        [SerializeField] private Vector3 homePosition = new Vector3(0f, 0.14f, 0f);
        [SerializeField] private Vector3 homeEulerAngles = new Vector3(90f, 0f, 0f);

        private Vector3 targetPosition;
        private Quaternion targetRotation;

        public event Action<Vector3, Quaternion> OnProbePoseChanged;

        private void Start()
        {
            ResetToHome();
        }

        public void ResetToHome()
        {
            targetPosition = homePosition;
            targetRotation = Quaternion.Euler(homeEulerAngles);
            transform.position = targetPosition;
            transform.rotation = targetRotation;
            OnProbePoseChanged?.Invoke(transform.position, transform.rotation);
        }

        /// <summary>
        /// Sets probe to Transverse view orientation (probing from top down, plane aligned laterally).
        /// </summary>
        public void SetTransverseView()
        {
            targetPosition = new Vector3(0f, 0.12f, 0f);
            targetRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        /// <summary>
        /// Sets probe to Sagittal view orientation (probing from top down, plane aligned longitudinal).
        /// </summary>
        public void SetSagittalView()
        {
            targetPosition = new Vector3(0f, 0.12f, 0f);
            targetRotation = Quaternion.Euler(90f, 90f, 0f);
        }

        /// <summary>
        /// Sets probe to Coronal / Lateral view orientation (probing from side into anatomy).
        /// </summary>
        public void SetCoronalView()
        {
            targetPosition = new Vector3(0.12f, 0f, 0f);
            targetRotation = Quaternion.Euler(0f, -90f, 0f);
        }

        private void Update()
        {
            HandleInput();

            if (smoothMotion)
            {
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothFactor);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * smoothFactor);
            }
            else
            {
                transform.position = targetPosition;
                transform.rotation = targetRotation;
            }

            OnProbePoseChanged?.Invoke(transform.position, transform.rotation);
        }

        private void HandleInput()
        {
            float dt = Time.deltaTime;
            float currentMoveSpeed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? sprintMultiplier : 1f);

            // 1. Translation in Local Probe Coordinates
            Vector3 moveDelta = Vector3.zero;

            // Lateral (X_P)
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) moveDelta += transform.right;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) moveDelta -= transform.right;

            // Axial / Beam depth (Z_P)
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) moveDelta += transform.forward;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) moveDelta -= transform.forward;

            // Elevation / Height (Y_P)
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space)) moveDelta += transform.up;
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftControl)) moveDelta -= transform.up;

            if (moveDelta.sqrMagnitude > 1e-5f)
            {
                targetPosition += moveDelta.normalized * (currentMoveSpeed * dt);
            }

            // 2. Rotation around Local Probe Axes
            float pitch = 0f; // Tilt around X_P
            float yaw = 0f;   // Turn around Y_P
            float roll = 0f;  // Twist around Z_P

            // Pitch (I / K)
            if (Input.GetKey(KeyCode.I)) pitch -= rotateSpeed * dt;
            if (Input.GetKey(KeyCode.K)) pitch += rotateSpeed * dt;

            // Yaw (J / L)
            if (Input.GetKey(KeyCode.J)) yaw -= rotateSpeed * dt;
            if (Input.GetKey(KeyCode.L)) yaw += rotateSpeed * dt;

            // Roll (U / O)
            if (Input.GetKey(KeyCode.U)) roll += rotateSpeed * dt;
            if (Input.GetKey(KeyCode.O)) roll -= rotateSpeed * dt;

            if (Mathf.Abs(pitch) > 1e-4f || Mathf.Abs(yaw) > 1e-4f || Mathf.Abs(roll) > 1e-4f)
            {
                Quaternion rotDelta = Quaternion.Euler(pitch, yaw, roll);
                targetRotation = targetRotation * rotDelta;
            }

            // 3. View Hotkeys
            if (Input.GetKeyDown(KeyCode.R)) ResetToHome();
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetTransverseView();
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetSagittalView();
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetCoronalView();
        }
    }
}
