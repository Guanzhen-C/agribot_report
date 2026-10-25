using System.Collections;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    public sealed class NativeDesktopResolution : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (Application.isEditor || Application.isBatchMode ||
                Application.platform is not (RuntimePlatform.LinuxPlayer or
                    RuntimePlatform.WindowsPlayer or RuntimePlatform.OSXPlayer)) return;
            var root = new GameObject("NativeDesktopResolution");
            DontDestroyOnLoad(root);
            root.AddComponent<NativeDesktopResolution>();
        }

        public static Vector2Int TargetResolution(FullScreenMode mode, Vector2Int current, Vector2Int monitor) =>
            mode == FullScreenMode.FullScreenWindow && monitor.x > 0 && monitor.y > 0 ? monitor : current;

        private IEnumerator Start()
        {
            var display = Screen.mainWindowDisplayInfo;
            var current = new Vector2Int(Screen.width, Screen.height);
            var monitor = new Vector2Int(display.width, display.height);
            var target = TargetResolution(Screen.fullScreenMode, current, monitor);
            // A native-sized borderless window can still stretch an old render
            // resolution retained in PlayerPrefs. Set the framebuffer explicitly.
            if (target != current)
            {
                Debug.Log($"UI_NATIVE_RESOLUTION previous={current.x}x{current.y} target={target.x}x{target.y}");
                Screen.SetResolution(target.x, target.y, Screen.fullScreenMode);
            }
            yield return null;
            yield return null;
            Debug.Log($"UI_FRAMEBUFFER render={Screen.width}x{Screen.height} monitor={monitor.x}x{monitor.y} mode={Screen.fullScreenMode}");
            Destroy(gameObject);
        }
    }
}
