using UnityEngine;
using UnityEngine.InputSystem;
using Mandible.PlayerController;
using Mandible.Core.Data;

namespace Mandible.PlayerController
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;
        private Camera camera;
        
        [Header("Settings")]
        [Space(8)]
        [SerializeField] public float mouseSensitivity = 3f;
        [SerializeField] public float controllerSensitivity = 150f;
        [SerializeField] public float baseFOV = 60f;
        [SerializeField] public bool enableProceduralEffects = true;

        [Header("Procedural Motion - Bob")]
        [Space(8)]
        [SerializeField] float bobBaseAmplitude = 0.02f;
        [SerializeField] float bobMaxAmplitude = 0.05f;
        [SerializeField] float bobFrequency = 1.2f;
        [SerializeField] float bobHorizontalFactor = 1f;
        [Range(0f, 1f)]
        [SerializeField] float proceduralT = 0f;
        [Space(8)]
        [SerializeField] float noiseBaseAmplitude = 0.005f;
        [SerializeField] float noiseMaxAmplitude = 0.02f;
        [SerializeField] float noiseBaseFrequency = 0.8f;

        [Header("Procedural Motion - Impact Response")]
        [Space(8)]
        [SerializeField] private float springStiffness = 150f;
        [SerializeField] private float springDamping = 25f;
        [Space(8)]
        [SerializeField] private float shakeNoiseAmplitude = 0.02f;
        [SerializeField] private float shakeNoiseFrequency = 25f;
        [SerializeField] float shakeNoiseResetSpeed = 5f;
        [SerializeField] float shakeNoiseResetSmoothness = 10f;
        [HideInInspector] private Vector3 shakeOffset = Vector3.zero;
        [HideInInspector] private Vector3 shakeVelocity = Vector3.zero;

        [Header("Advanced")]
        [Range(0f, 1f)]
        public float cameraStability = 0f;

        [Header("Config")]
        [SerializeField] PlayerConfigData configData;
    
        // Input System
        PlayerInputActions input;
        InputDevice currentDevice;
        Vector2 lookInput = Vector3.zero;
        Vector3 localPos;

        // Transform
        float yaw, pitch;
        Vector2 recoil;

        void Awake()
        {
            InitializeInput();
        }

        void OnEnable()
        {
            input.Enable();

            //Events
            EnablePlayerControllerEvents();
        }

        void OnDisable()
        {
            input.Disable();

            //Events
            DisablePlayerControllerEvents();
        }

        void Start()
        {
            camera = GetComponent<Camera>();

            //Initialize
            localPos = transform.localPosition;
            SetFOV(baseFOV);
        }

        void Update()
        {
            HandleLook();

            HandleControllerParameters();

            if (enableProceduralEffects)
            {
                ApplyProceduralEffects();
            }
        }

        //Look
        private const float LOOK_PITCH_EPSILON = 1e-3f;
        void HandleLook()
        {
            float sensitivity = mouseSensitivity;

            if (currentDevice is Gamepad)
                sensitivity = controllerSensitivity;
            
            //yaw += lookInput.x * sensitivity * Time.deltaTime;
            pitch -= lookInput.y * sensitivity * Time.deltaTime;

            pitch = Mathf.Clamp(pitch, -90f + LOOK_PITCH_EPSILON, 90f - LOOK_PITCH_EPSILON);

            transform.localRotation = Quaternion.Euler(pitch, transform.localRotation.y, transform.localRotation.z);
        }

        //FOV

        public void SetFOV(float fov)
        {
            if(camera == null) return;
            camera.fieldOfView = fov;
        }

        public float GetFOV()
        {
            if(camera == null) return baseFOV;
            return camera.fieldOfView;
        }

        //Input

        void InitializeInput()
        {
            input = new PlayerInputActions();

            //Look
            input.Player.Look.performed += ctx => 
            {
                lookInput = ctx.ReadValue<Vector2>();
                currentDevice = ctx.control.device;
            };

            input.Player.Look.canceled += _ => lookInput = Vector2.zero;
        }

        void HandleControllerParameters()
        {
            if (playerController != null)
                SetProceduralT(playerController.IsInAir() ? 0f : playerController.GetVelocityT());
        }

        //Noise

        void ApplyProceduralEffects()
        {
            Vector3 noise = GetProceduralNoise();
            Vector3 shake = GetProceduralShake();
            Vector3 finalPos = localPos + (noise + shake) * GetStabilityInverse();
            transform.localPosition = finalPos;
        }

        Vector3 smoothedNoise;
        float bobPhase;
        float noisePhase;

        Vector3 GetProceduralNoise()
        {
            const float BASE_HORIZONTAL_MIN = 0.2f;
            const float BASE_FREQ_MIN = 0.1f;
            const float BASE_FREQ_MAX = 1.0f;
            const float RESPONSE_SPEED = 6f; 

            float bobAmp = Mathf.Min(
                Mathf.Lerp(bobBaseAmplitude, bobMaxAmplitude, proceduralT),
                bobMaxAmplitude
            );
            float bobHoriz = Mathf.Lerp(BASE_HORIZONTAL_MIN, bobHorizontalFactor, Mathf.Clamp01(proceduralT));
            float bobFreq = bobFrequency * Mathf.Lerp(BASE_FREQ_MIN, BASE_FREQ_MAX, proceduralT);

            bobPhase += bobFreq * Time.deltaTime;

            float xBase = Mathf.Sin(bobPhase) * bobAmp * bobHoriz;
            float yBase = Mathf.Sin(bobPhase * 2f) * bobAmp;
            Vector3 baseBob = new Vector3(xBase, yBase, 0f);

            float noiseAmp = Mathf.Min(
                Mathf.Lerp(noiseBaseAmplitude, noiseMaxAmplitude, proceduralT),
                noiseMaxAmplitude
            );
            float noiseFreq = noiseBaseFrequency * Mathf.Lerp(BASE_FREQ_MIN, BASE_FREQ_MAX, proceduralT);

            noisePhase += noiseFreq * Time.deltaTime;

            float nx = Mathf.PerlinNoise(noisePhase, 0.37f) - 0.5f;
            float ny = Mathf.PerlinNoise(noisePhase + 10f, 0.71f) - 0.5f;
            Vector3 noise = new Vector3(nx, ny, 0f) * noiseAmp;
            
            Vector3 target = baseBob + noise;
            smoothedNoise = Vector3.Lerp(smoothedNoise, target, RESPONSE_SPEED * Time.deltaTime);

            return smoothedNoise;
        }

        Vector3 GetProceduralShake()
        {
            Vector3 springForce = -springStiffness * shakeOffset - springDamping * shakeVelocity;
            shakeVelocity += springForce * Time.deltaTime;

            shakeOffset = Vector3.Lerp(shakeOffset, shakeOffset + shakeVelocity * Time.deltaTime, shakeNoiseResetSmoothness * Time.deltaTime);

            shakeOffset = Vector3.Lerp(shakeOffset, Vector3.zero, shakeNoiseResetSpeed * Time.deltaTime);

            if(shakeOffset.sqrMagnitude < 0.00001f && shakeVelocity.sqrMagnitude < 0.00001f)
            {
                shakeOffset = Vector3.zero;
                shakeVelocity = Vector3.zero;
                return Vector3.zero;
            }

            float velocityFactor = Mathf.Clamp01(shakeVelocity.magnitude * 0.5f);

            //Noise
            float nx = (Mathf.PerlinNoise(Time.deltaTime * shakeNoiseFrequency, 0f) - 0.5f) * shakeNoiseAmplitude;
            float ny = (Mathf.PerlinNoise(Time.deltaTime * shakeNoiseFrequency, 1f) - 0.5f) * shakeNoiseAmplitude;
            float nz = (Mathf.PerlinNoise(Time.deltaTime * shakeNoiseFrequency, 2f) - 0.5f) * shakeNoiseAmplitude;

            Vector3 noise = new Vector3(nx, ny, nz) * Mathf.Lerp(0, 1f, velocityFactor);

            return shakeOffset + noise;
        }

        public void SetProceduralT(float t)
        {
            this.proceduralT = t;
        }

        public float GetStabilityInverse()
        {
            return 1f - cameraStability;
        }

        public void AddShakeImpulse(Vector3 force, float scale = 1f)
        {
            Vector3 localForce = transform.InverseTransformDirection(force) * scale;
            shakeVelocity += localForce;
        }

        public void AddFlatImpulse(Vector3 force)
        {
            AddShakeImpulse(force, 1f);
        }

        public void AddRecoil(Vector2 recoilAmount)
        {
            recoil += recoilAmount;
        }

        //Settings

        void UpdateConfig(PlayerConfig config)
        {
            if (config == null) return;
            
            if(config.input != null)
            {
                mouseSensitivity = config.input.mouseSensitivity;
                controllerSensitivity = config.input.controllerSensitivity;
            }
        }

        //Events
        void EnablePlayerControllerEvents()
        {
            if (playerController == null) return;
            
            playerController.OnForceApplied += AddFlatImpulse;
            playerController.OnGroundImpact += AddFlatImpulse;
        }

        void DisablePlayerControllerEvents()
        {
            if (playerController == null) return;
            
            playerController.OnForceApplied -= AddFlatImpulse;
            playerController.OnGroundImpact -= AddFlatImpulse;
        }
    }
}
