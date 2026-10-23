#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace RG.Tweening
{
    [InitializeOnLoad]
    public static class RTweenEditorPlayModeReset
    {
        static RTweenEditorPlayModeReset()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Do not destroy the engine while play-mode objects are still running
            // their OnDisable/OnDestroy callbacks. Those callbacks may use RTween
            // for cleanup and would recreate the singleton while Unity is closing
            // the play-mode scene, producing the "objects were not cleaned up"
            // error. Unity tears down the runtime instance itself; this pass only
            // removes an unlikely leftover after the transition has completed.
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                var go = GameObject.Find("[RTween Engine]");
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }
    }
}
#endif
