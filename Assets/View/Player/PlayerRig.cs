using UnityEngine;
using Aerow.View.Input;

namespace Aerow.View
{
    /// <summary>
    /// Minimal first-person player + camera rig for exercising the input layer — walk, look,
    /// sprint, jump. This is a throwaway test harness, not a final controller: it reads only
    /// <see cref="GameInput"/> semantic signals (never the Input System directly), so it also
    /// demonstrates the split. Put it on a capsule (a CharacterController is added
    /// automatically) with the Camera childed directly to it at eye height.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerRig : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The Camera childed directly to this player (pitches up/down). Auto-found if left empty.")]
        [SerializeField] private Transform playerCamera;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 9f;
        [SerializeField] private float jumpHeight = 1.4f;
        [SerializeField] private float gravity = -20f;

        [Header("Look")]
        [SerializeField] private float lookSensitivity = 0.1f;
        [SerializeField] private float pitchMin = -85f;
        [SerializeField] private float pitchMax = 85f;

        private CharacterController _controller;
        private float _pitch;
        private float _verticalVelocity;
        private bool _warnedNoInput;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (playerCamera == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null) playerCamera = cam.transform;
            }
            if (playerCamera == null)
                Debug.LogWarning("[PlayerRig] No playerCamera assigned and no child Camera found — " +
                                 "look won't work. Parent a Camera under this object.", this);
        }

        private void OnEnable() => SetCursorLocked(true);
        private void OnDisable() => SetCursorLocked(false);

        private void Update()
        {
            if (!GameInput.IsInitialized)
            {
                if (!_warnedNoInput)
                {
                    Debug.LogWarning("[PlayerRig] GameInput not initialized — is a GameBootstrap " +
                                     "in the scene?", this);
                    _warnedNoInput = true;
                }
                return;
            }

            // Escape frees the mouse for testing (no UI yet to own it).
            if (GameInput.Global.EscapePressed)
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);

            Look();
            Move();
        }

        private void Look()
        {
            // Only steer while the cursor is captured. Mouse delta is already per-frame, so it
            // is NOT scaled by deltaTime.
            if (Cursor.lockState != CursorLockMode.Locked) return;

            Vector2 look = GameInput.OnFoot.Look * lookSensitivity;

            transform.Rotate(Vector3.up, look.x); // yaw the body

            _pitch = Mathf.Clamp(_pitch - look.y, pitchMin, pitchMax);
            if (playerCamera != null)
                playerCamera.localRotation = Quaternion.Euler(_pitch, 0f, 0f); // pitch the camera
        }

        private void Move()
        {
            Vector2 move = GameInput.OnFoot.Move; // WASD (normalized by the composite)
            float speed = GameInput.OnFoot.SprintHeld ? sprintSpeed : walkSpeed;

            Vector3 planar = (transform.right * move.x + transform.forward * move.y) * speed;

            if (_controller.isGrounded)
            {
                _verticalVelocity = -1f; // small stick-to-ground force
                if (GameInput.OnFoot.JumpPressed)
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 velocity = planar + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
