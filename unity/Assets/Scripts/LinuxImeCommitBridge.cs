using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

internal static class LinuxImeCommitBridge
{
    private const string Library = "agribot_linux_ime";
    private static readonly byte[] buffer = new byte[65536];
    private static bool checkedLibrary, available;

    [DllImport(Library)] private static extern int agribot_ime_available();
    [DllImport(Library)] private static extern void agribot_ime_set_active(int active);
    [DllImport(Library)] private static extern int agribot_ime_pop([Out] byte[] output, int capacity);

    internal static bool Available
    {
        get
        {
            if (checkedLibrary) return available;
            checkedLibrary = true;
            if (Application.platform != RuntimePlatform.LinuxPlayer) return false;
            // The shim is already loaded in this process. Do not preload it into
            // Python, ROS, Gazebo or any other child process launched by the App.
            string preload = Environment.GetEnvironmentVariable("LD_PRELOAD");
            if (!string.IsNullOrEmpty(preload))
            {
                string remaining = string.Join(":", preload.Split(new[] { ':', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(path => Path.GetFileName(path) != "libagribot_linux_ime.so"));
                Environment.SetEnvironmentVariable("LD_PRELOAD", string.IsNullOrEmpty(remaining) ? null : remaining);
            }
            try { available = agribot_ime_available() != 0; }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            if (Environment.GetEnvironmentVariable("AGRIBOT_IME_DIAGNOSTICS") == "1")
                Debug.Log("AGRIBOT_IME_BRIDGE ready=" + available + " childPreload=" +
                    (Environment.GetEnvironmentVariable("LD_PRELOAD") ?? "").Contains("libagribot_linux_ime.so"));
            return available;
        }
    }

    internal static void SetActive(bool active)
    {
        if (Available) agribot_ime_set_active(active ? 1 : 0);
    }

    internal static string Pop()
    {
        if (!Available) return null;
        int length = agribot_ime_pop(buffer, buffer.Length);
        return length > 0 ? Encoding.UTF8.GetString(buffer, 0, length) : null;
    }
}
