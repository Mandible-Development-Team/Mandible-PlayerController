using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using Mandible.Systems.Data;

public class PlayerMovementStateFSM : FiniteStateMachine
{
    public Transform player;

    public enum PlayerMovementState
    {
        Idle,
        Running,
        Jumping
    }

    [SerializedDictionary("Movement State", "Definition")]
    [SerializeField] private SerializedDictionary<PlayerMovementState, FiniteState> states = new();

    [SerializeField] public PlayerMovementState currentState;

    public PlayerContext ctx = new PlayerContext();
    
    void Start()
    {
        ctx.controller = player.GetComponent<PlayerController>();
    }
    
    void Update()
    {
        OnUpdate(ctx);
    }
    
    public void OnUpdate(PlayerContext ctx = default)
    {
        PlayerController controller = ctx.controller;

        if (controller.isInAir())
        {
            currentState = PlayerMovementState.Jumping;
        }
        else if (controller.isMoving())
        {
            currentState = PlayerMovementState.Running;
        }
        else
        {
            currentState = PlayerMovementState.Idle;
        }

        if (states.TryGetValue(currentState, out FiniteState state))
        {
            state.OnEventUpdate();
        }
        else
        {
            Debug.LogWarning($"No state found for {currentState}");
        }
    }
}

public struct PlayerContext
{
    public PlayerController controller;
}