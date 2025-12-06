using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;
using Mandible.Systems.Data;

public class PlayerInputSystem : IInputSystem
{
    [SerializeField] private PlayerInputActions inputActions;

    [SerializeField]
    private readonly SerializedDictionary<string, InputAction> actions = new();

    private readonly Dictionary<string, InputSignal> signals = new();
    private readonly Dictionary<string, bool> wasHeldLastFrame = new();

    public bool debug = true;

    //Start
    public PlayerInputSystem()
    {
        this.inputActions = new PlayerInputActions();
        inputActions.Enable();

        InitializeActions();     
    }

    public PlayerInputSystem(PlayerInputActions inputActions)
    {
        this.inputActions = inputActions;

        InitializeActions();
    }

    private void InitializeActions()
    {
        if(this.actions == null) return;
        
        foreach (var map in inputActions.asset.actionMaps)
        {
            foreach (var action in map.actions)
            {
                InitializeAction(action.name, action);
            }
        }        
    }

    private void InitializeAction(string name, InputAction action)
    {
        actions[name] = action;
        wasHeldLastFrame[name] = false;
        signals[name] = new InputSignal();
    }

    //Update
    public void Update()
    {
        foreach (var kvp in actions)
        {
            UpdateActionState(kvp.Key, kvp.Value);
        }
    }

    //Signal
    public InputSignal CreateSignal()
    {
        InputSignal sig = new InputSignal();
        return sig;  
    }

    public InputSignal GetSignal(string actionName)
    {
        return signals.TryGetValue(actionName, out var signal) 
            ? signal 
            : CreateSignal();
    }

    public bool WasActivatedThisFrame(string actionName)
    {
        return GetSignal(actionName).Pressed;
    }

    public PlayerInputActions GetPlayerInputActions() => inputActions;

    public void Activate(string actionName) { }

    //Actions
    public bool Pressed(string action) => GetSignal(action).Pressed;
    public bool Held(string action)    => GetSignal(action).Held;
    public bool Released(string action)=> GetSignal(action).Released;

    public bool Consume(string actionName, InputType type)
    {
        if (!signals.TryGetValue(actionName, out var sig))
            return false;

        bool value = type switch
        {
            InputType.Pressed  => sig.Pressed,
            InputType.Held     => sig.Held,
            InputType.Released => sig.Released,
            _ => false
        };

        if (!value) return false;

        if (sig.IsConsumed(type)) 
            return false;

        sig.Consume(type);
        signals[actionName] = sig;

        return true;
    }

    public bool ConsumePressed(string actionName) => Consume(actionName, InputType.Pressed);

    public bool ConsumeHeld(string actionName) => Consume(actionName, InputType.Held);

    public bool ConsumeReleased(string actionName) => Consume(actionName, InputType.Released);

    private void UpdateActionState(string name, InputAction action)
    {
        bool held = action.IsPressed();
        bool wasHeld = wasHeldLastFrame[name];

        signals[name] = new InputSignal
        {
            Pressed = held && !wasHeld,
            Released = !held && wasHeld,
            Held = held
        };

        wasHeldLastFrame[name] = held;
    }

    //Advanced
    public T GetContext<T>(string actionName) where T : struct
    {
        if (actions.TryGetValue(actionName, out var action))
            return action.ReadValue<T>();

        return default;
    }

}
