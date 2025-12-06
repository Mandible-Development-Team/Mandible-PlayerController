using UnityEngine;

public interface IPlayer
{
    //Monobehaviours
    PlayerController Controller { get;}
    
    //Other
    IInputSystem Input { get; } 
}