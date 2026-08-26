#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using VirtualUltrasound.Bootstrap;
using VirtualUltrasound.Probe;

namespace VirtualUltrasound.Editor
{
    public static class SceneSetupMenu
    {
        [MenuItem("Virtual Ultrasound/Setup Simulator Scene in Editor")]
        public static void SetupScene()
        {
            SceneBootstrapper bootstrapper = Object.FindObjectOfType<SceneBootstrapper>();
            if (bootstrapper == null)
            {
                GameObject bootstrapperObj = new GameObject("AppBootstrapper");
                bootstrapper = bootstrapperObj.AddComponent<SceneBootstrapper>();
                Undo.RegisterCreatedObjectUndo(bootstrapperObj, "Create AppBootstrapper");
            }

            bootstrapper.BuildScene();
            EditorUtility.SetDirty(bootstrapper.gameObject);
            EditorUtility.DisplayDialog(
                "Virtual Ultrasound Simulator",
                "Scene successfully set up!\n\nSwitch to the Game tab or press Play (Ctrl + P) to interact with the virtual probe and live 2D ultrasound view.",
                "OK"
            );
        }
    }
}
#endif
