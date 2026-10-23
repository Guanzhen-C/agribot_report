#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
internal static class BottomPanelSliderSceneSync
{
    static BottomPanelSliderSceneSync()
    {
        // 等待脚本重载和场景反序列化完成后，再同步层级对象。
        EditorApplication.delayCall += EnsureSliderInOpenScene;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorSceneManager.sceneOpened += (_, _) =>
            EditorApplication.delayCall += EnsureSliderInOpenScene;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += EnsureSliderInOpenScene;
    }

    private static void EnsureSliderInOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        bottonPanel[] panels = Object.FindObjectsByType<bottonPanel>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (bottonPanel panel in panels)
        {
            if (panel == null)
                continue;

            Transform existingSliderTransform = panel.transform.Find("WheelRadiusSlider");
            bool sliderWasMissing = existingSliderTransform == null;
            bool handleVisualWasMissing = true;
            if (existingSliderTransform != null)
            {
                Slider existingSlider = existingSliderTransform.GetComponent<Slider>();
                handleVisualWasMissing = existingSlider == null ||
                                         existingSlider.handleRect == null ||
                                         existingSlider.handleRect.Find("HandleVisual") == null;
            }

            if (!sliderWasMissing && !handleVisualWasMissing)
                continue;

            GameObject slider = panel.EnsureRadiusSlider();
            GameObject handleVisual = panel.EnsureHandleVisual();

            if (sliderWasMissing)
                Undo.RegisterCreatedObjectUndo(slider, "Create wheel radius slider");

            if (handleVisual == null)
                continue;

            EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
            Selection.activeGameObject = handleVisual;
        }
    }
}
#endif
