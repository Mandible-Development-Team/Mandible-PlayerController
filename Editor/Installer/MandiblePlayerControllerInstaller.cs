#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEditor;
using UnityEditor.PackageManager;

[InitializeOnLoad]
public static class MandiblePlayerControllerInstaller
{
    private const string PackageName = "com.mandible.playercontroller";

    private static bool test = false;
    private static bool debug = false;

    static MandiblePlayerControllerInstaller()
    {
        Events.registeredPackages += OnPackagesChanged;

        if (test) EditorApplication.delayCall += Install;
    }

    private static void OnPackagesChanged(PackageRegistrationEventArgs args)
    {
        foreach (var added in args.added)
        {
            if (added.packageId.StartsWith(PackageName))
            {
                if (debug) Debug.Log("Mandible Player Controller installed.");
                Install();
                
            }
        }
    }

    private static void Install(){
        ApplyMandibleInputSettings();   
    }

    private static void ApplyMandibleInputSettings()
    {
        var settings = InputSystem.settings;

        settings.updateMode = InputSettings.UpdateMode.ProcessEventsInFixedUpdate;

        if (debug) Debug.Log("Mandible Player Controller: Input update mode set to FixedUpdate.");
        EditorUtility.SetDirty(settings);
    }
}
#endif
