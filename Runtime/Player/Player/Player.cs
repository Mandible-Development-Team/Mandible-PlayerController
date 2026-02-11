using UnityEngine;
using Mandible.PlayerController;

namespace Mandible.PlayerController 
{
    public class Player : MonoBehaviour, IPlayer
    {
        [Header("Components")]
        [SerializeField] private PlayerController controller;
        [SerializeField] private new CameraController camera;

        public PlayerController Controller => controller;
        public CameraController Camera => camera;

        public IInputSystem Input { get; private set; }
        
        void Awake()
        {
            controller = GetComponent<PlayerController>();

            Input = new PlayerInputSystem();
        }

        void Update()
        {
            Input?.Update();
        }
    }
}
