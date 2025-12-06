using UnityEngine;
using Mandible.PlayerController;

namespace Mandible.PlayerController{
    public interface IPlayer
    {
        //Monobehaviours
        PlayerController Controller { get;}
        
        //Other
        IInputSystem Input { get; } 
    }
}