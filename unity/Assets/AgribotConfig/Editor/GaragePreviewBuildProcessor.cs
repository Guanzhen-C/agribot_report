using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace Agribot.VehicleConfiguration.Editor
{
    // Garage walls must follow the runtime bay size, not a world-space static batch.
    public sealed class GaragePreviewBuildProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != "Garage1") continue;
                foreach (var part in root.GetComponentsInChildren<UnityEngine.Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(part.gameObject,
                        GameObjectUtility.GetStaticEditorFlags(part.gameObject) & ~StaticEditorFlags.BatchingStatic);
            }
        }
    }
}
