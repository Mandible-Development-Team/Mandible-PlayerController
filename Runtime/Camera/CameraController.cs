using UnityEngine;
using UnityEngine.InputSystem;
using Mandible.PlayerController;
using Mandible.Systems.Data;

namespace Mandible.PlayerController
{
    public class CameraController : MonoBehaviour
    {
        #if MANDIBLE_PLAYER_CONTROLLER
        [SerializeField] private PlayerController playerController;
        #endif
        
        [Header("Settings")]
        [Space(8)]
        [SerializeField] float mouseSensitivity = 3f;
        [SerializeField] float controllerSensitivity = 150f;
        [SerializeField] bool enableProceduralEffects = true;

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

        [Header("Config")]
        [SerializeField] PlayerConfigData configData;

        // Input System
        PlayerInputActions input;
        InputDevice currentDevice;
        Vector2 lookInput = Vector3.zero;
        Vector3 localPos;
        float yaw, pitch;

        void Awake()
        {
            InitializeInput();
        }

        void OnEnable()
        {
            input.Enable();

            //Events
            #if MANDIBLE_PLAYER_CONTROLLER
            EnablePlayerControllerEvents();
            #endif
        }

        void OnDisable()
        {
            input.Disable();

            //Events
            #if MANDIBLE_PLAYER_CONTROLLER
            DisablePlayerControllerEvents();
            #endif
        }

        void Start()
        {
            localPos = transform.localPosition;
        }

        void Update()
        {
            if (lookInput.sqrMagnitude > 0.001f)
            {
                HandleLook();
            }


            #if MANDIBLE_PLAYER_CONTROLLER
            HandleControllerParameters();
            #endif
            

            if (enableProceduralEffects)
            {
                ApplyProceduralEffects();
            }
        }

        void HandleLook()
        {
            float sensitivity = mouseSensitivity;

            if (currentDevice is Gamepad)
                sensitivity = controllerSensitivity;
            
            yaw += lookInput.x * sensitivity * Time.deltaTime;
            pitch -= lookInput.y * sensitivity * Time.deltaTime;

            pitch = Mathf.Clamp(pitch, -90f, 90f);

            transform.localRotation = Quaternion.Euler(pitch, transform.rotation.y, transform.rotation.z);
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

        #if MANDIBLE_PLAYER_CONTROLLER
        void HandleControllerParameters()
        {
            if (playerController != null)
                SetProceduralT(playerController.isInAir() ? 0f : playerController.GetVelocityT());
        }
        #endif

        //Noise

        void ApplyProceduralEffects()
        {
            Vector3 noise = GetProceduralNoise();
            Vector3 shake = GetProceduralShake();
            transform.localPosition = localPos + noise + shake;
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

        public void AddShakeImpulse(Vector3 force, float scale = 1f)
        {
            Vector3 localForce = transform.InverseTransformDirection(force) * scale;
            shakeVelocity += localForce;
        }

        public void AddFlatImpulse(Vector3 force)
        {
            AddShakeImpulse(force, 1f);
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

        #if MANDIBLE_PLAYER_CONTROLLER
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
        #endif
    }
}
