using UnityEngine;

public class Player : MonoBehaviour, IPlayer
{
    [Header("Components")]
    [SerializeField] private PlayerController controller;

    public PlayerController Controller => controller;

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
