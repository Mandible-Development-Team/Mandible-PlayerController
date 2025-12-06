using UnityEngine;

[CreateAssetMenu(fileName = "New PlayerConfigData", menuName = "Config/PlayerConfig")]
public class PlayerConfigData : ScriptableObject
{
    public PlayerConfig config;
}

[System.Serializable]
public class PlayerConfig
{
    public ControllerConfig controller;
    public InputConfig input;
}

[System.Serializable]
public class ControllerConfig
{
    public float walkSpeed = 5f;
}

[System.Serializable]
public class InputConfig
{
    public float mouseSensitivity = 3f;
    public float controllerSensitivity = 150f;
}