using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System;

using Mandible.PlayerController;
using Mandible.Core;

namespace Mandible.PlayerController
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour, IAgent
    {
        [SerializeField] private Transform playerObject;
        public IPlayer player;
        [Header("Core Components")]
        [SerializeField] public new Camera camera;
        private CapsuleCollider capsule;
        public CharacterController controller { get; set; }
        [Header("State")]
        [SerializeField] bool isGrounded;
        [HideInInspector] public PlayerMovementStateFSM movementStateMachine; //EXPERIMENTAL

        [Header("Settings")]
        [SerializeField] float movementSpeed = 7f;
        [SerializeField] float jumpForce = 5f;
        [SerializeField] float gravity = 7f;
        [SerializeField] Vector3 gravityDirection = new Vector3(0, -1f, 0);

        [Header("Advanced Settings")]
        [SerializeField] float mass = 5f;
        [SerializeField] float movementSmoothing = 7f;
        [SerializeField] float externalDampening = 5f;
        [SerializeField] float moveDampening = 1f;
        [SerializeField] float mouseSensitivity = 3f;
        [SerializeField] float controllerSensitivity = 150f;
        [SerializeField] float gravityAlignSpeed = 8f;
        [HideInInspector] bool cancelYDampening = false; //EXPERIMENTAL

        [Header("Config")]
        [SerializeField] PlayerConfigData configData;

        //Events
        public event Action<Vector3> OnForceApplied;
        public event Action<Vector3> OnGroundImpact;

        // Position / Speed
        protected float currentSpeed;
        protected Vector3 currentVelocity;
        protected Vector3 lastPosition;

        // Velocities
        protected Vector3 moveVelocity;
        protected Vector3 externalVelocity;
        protected Vector3 gravityVelocity;
        protected Vector3 groundImpact;

        Vector3 smoothedMove;
        bool wasGrounded = false;

        //Transform
        Quaternion initialRotation = Quaternion.Euler(0f, 0f, 0f);
        float yaw, pitch;

        //Ground
        const float RADIUS_ADJUST = 0.99f;
        const float RADIUS_DIST = 0.025f;

        // Agent
        public Vector3 lookDirection { get; set; }

        //Capabilities
        [SerializeField] protected List<ICapability> capabilities = new List<ICapability>();

        // Input
        private PlayerInputSystem playerInputSystem;
        public IInputSystem Input => playerInputSystem;
        public PlayerInputActions input;
        InputDevice currentDevice;
        Vector2 moveInput;
        Vector2 lookInput;

        protected virtual void Awake()
        {
            if(camera == null) camera = GetComponentInChildren<Camera>();
            if(controller == null) controller = GetComponent<CharacterController>();
            if(capsule == null) capsule = GetComponent<CapsuleCollider>();
        }

        protected virtual void Start()
        {
            InitializePlayer();
            InitializeCamera();

            InitializeTransform();
            InitializeInput();

            InitializeCapabilities();
        }
        
        protected virtual void Update()
        {
            HandleLook();
            HandleMovement();
            HandleGravity();

            ApplyMovement();

            GroundCheck();
            UpdateMovementData();
        }

        //Initialization

        void InitializePlayer()
        {
            if(playerObject == null)
                playerObject = this.transform;
            
            player = playerObject.GetComponent<IPlayer>();
            
            if(player == null)
                Debug.LogWarning("No IPlayer found on object — add a MonoBehaviour that implements IPlayer.");
        }

        void InitializeCamera()
        {
            if(camera == null) return;
            if(!camera.GetComponent<CameraController>())
                camera.gameObject.AddComponent<CameraController>();
        }

        void InitializeTransform()
        {
            initialRotation = transform.rotation;
        }

        void InitializeInput()
        {
            if(player == null) return;

            playerInputSystem = player.Input as PlayerInputSystem;
            input = playerInputSystem.GetPlayerInputActions();

            input.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
            input.Player.Move.canceled += _ => moveInput = Vector2.zero;

            input.Player.Look.performed += ctx => 
            {
                lookInput = ctx.ReadValue<Vector2>();
                currentDevice = ctx.control.device;
            };
            input.Player.Look.canceled += _ => lookInput = Vector2.zero;

            input.Player.Jump.performed += _ => Jump();
        }

        void InitializeCapabilities()
        {
            foreach(var capability in capabilities)
            {
                capability.Initialize(this);
            }
        }

        //Movement

        void UpdateMovementData()
        {
            //Look
            lookDirection = camera.transform.forward;

            //Speed
            currentSpeed = (transform.position - lastPosition).magnitude / Time.deltaTime;
            currentVelocity = (transform.position - lastPosition) / Time.deltaTime;
            lastPosition = transform.position;
        }

        void HandleMovement()
        {
            Vector3 g = gravityDirection.normalized;

            Vector3 camForward = Vector3.ProjectOnPlane(camera.transform.forward, g).normalized;
            Vector3 camRight   = Vector3.ProjectOnPlane(camera.transform.right, g).normalized;

            Vector3 inputDir = camRight * moveInput.x + camForward * moveInput.y;

            Vector3 targetMove = inputDir * movementSpeed;

            moveVelocity = Vector3.Lerp(moveVelocity, targetMove, movementSmoothing * Time.deltaTime);
        }

        void ApplyMovement()
        {
            moveVelocity *= Mathf.Exp(-moveDampening * Time.deltaTime);

            if (cancelYDampening)
            {
                Vector3 g = gravityDirection.normalized;

                Vector3 vG = Vector3.Project(externalVelocity, g);
                Vector3 vP = externalVelocity - vG;

                vP *= Mathf.Exp(-externalDampening * Time.deltaTime);
                externalVelocity = vP + vG;
            }
            else
            {
                externalVelocity *= Mathf.Exp(-externalDampening * Time.deltaTime);
            }

            Vector3 finalVelocity =
                moveVelocity +
                externalVelocity +
                gravityVelocity;

            controller.Move(finalVelocity * Time.deltaTime);
        }

        void Jump()
        {
            if (isGrounded)
            {
                Vector3 g = gravityDirection.normalized;

                gravityVelocity = (-g * jumpForce);

                isGrounded = false;
            }
        }

        //Physics

        void HandleGravity()
        {
            Vector3 g = gravityDirection.normalized;

            RotateToGravity();

            gravityVelocity += g * gravity * Time.deltaTime;
        }

        const float GROUND_FORCE_SCALAR = 1f;

        void GroundCheck()
        {
            Vector3 g = gravityDirection.normalized;

            Vector3 bottomCenter =
                transform.TransformPoint(controller.center)
                + g * (controller.height * 0.5f - controller.radius + controller.skinWidth + RADIUS_DIST);

            float radius = controller.radius * RADIUS_ADJUST;

            // Ground detection
            int playerLayer = gameObject.layer;
            int layerMask = ~(1 << playerLayer);

            isGrounded = Physics.CheckSphere(
                bottomCenter,
                radius,
                layerMask,
                QueryTriggerInteraction.Ignore
            );

            if (isGrounded)
            {
                float downward = Vector3.Dot(gravityVelocity, g);

                if (downward > 0f){
                    gravityVelocity -= g * downward;
                    groundImpact = mass * downward * g * GROUND_FORCE_SCALAR;
                } 
            }

            HandleLanding();
        }

        void RotateToGravity()
        {
            Vector3 g = gravityDirection.normalized;
            Vector3 gravityUp = -g;

            Vector3 smoothedUp = Vector3.Slerp(
                transform.up,
                gravityUp,
                gravityAlignSpeed * Time.deltaTime
            );

            Quaternion gravityBasis = Quaternion.FromToRotation(transform.up, smoothedUp) * transform.rotation;
            transform.rotation = gravityBasis * yawFrame;
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody rb = hit.collider.attachedRigidbody;
            if (rb == null || rb.isKinematic) return;

            Vector3 pushDir = hit.moveDirection;
            pushDir.y = 0f;

            float m1 = mass;
            float m2 = rb.mass;

            float v1 = (moveVelocity + externalVelocity).magnitude;
            if (v1 < 0.5f) return;

            float e = 0.1f;
            float j = (1 + e) * (m1 * v1) / (m1 + m2);

            Vector3 impulse = pushDir * j * 0.25f;
            rb.AddForce(impulse, ForceMode.Impulse);
        }

        //Capabilities

        public T GetCapability<T>() where T : ICapability
        {
            foreach(var capability in capabilities)
            {
                if(capability is T) return (T)capability;
            }

            return default(T);
        }

        public void AddCapability<T>() where T : ICapability
        {
            T capability = Activator.CreateInstance<T>();
            capabilities.Add(capability);
        }

        //Input

        Quaternion yawFrame = Quaternion.identity;
        void HandleLook()
        {
            float sensitivity = mouseSensitivity;

            if(currentDevice is Gamepad)
                sensitivity = controllerSensitivity;

            float yawContribution = lookInput.x * sensitivity * Time.deltaTime;
            yawFrame = Quaternion.AngleAxis(yawContribution, Vector3.up);

            yaw += yawContribution;
        }

        //Events

        void HandleLanding()
        {
            if(!wasGrounded && isGrounded){
                OnGroundImpact?.Invoke(groundImpact);
            }

            wasGrounded = isGrounded;
        }

        //Utilities

        public void ApplyImpulse(Vector3 impulse)
        {
            OnForceApplied?.Invoke(impulse);
            externalVelocity += impulse;
        }

        public void CancelGravity()
        {
            gravityVelocity = Vector3.zero;
        }

        public float GetVelocityT()
        {
            return currentSpeed / movementSpeed;
        }

        public Vector3 GetLookDirection()
        {
            return lookDirection;
        }

        void UpdateConfig(PlayerConfig config)
        {
            if (config == null) return;

            if (config.controller != null)
                movementSpeed = config.controller.walkSpeed;

            if (config.input != null){
                mouseSensitivity = config.input.mouseSensitivity;
                controllerSensitivity = config.input.controllerSensitivity;
            }
        }

        //Helpers

        public bool IsWalking()
        {
            return IsMoving() && moveInput.sqrMagnitude > 0.1f;
        }

        public bool IsMoving()
        {
            return currentSpeed > 0.1f;
        }

        public bool IsInAir()
        {
            return !isGrounded;
        }

        //Editor

        void OnDrawGizmos()
        {
            if (controller == null) return;

            Vector3 g = gravityDirection.normalized;

            Vector3 bottomCenter =
                transform.TransformPoint(controller.center)
                + g * (controller.height * 0.5f - controller.radius + controller.skinWidth + RADIUS_DIST);

            float radius = controller.radius * RADIUS_ADJUST;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(bottomCenter, radius);
        }
    }
}


