using NUnit.Framework;
using UnityEngine;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class NativeDesktopResolutionTests
    {
        [TestCase(FullScreenMode.FullScreenWindow, 1600, 900, 2560, 1440, 2560, 1440)]
        [TestCase(FullScreenMode.FullScreenWindow, 1600, 900, 3840, 2160, 3840, 2160)]
        [TestCase(FullScreenMode.FullScreenWindow, 2560, 1440, 2560, 1440, 2560, 1440)]
        [TestCase(FullScreenMode.FullScreenWindow, 1280, 720, 0, 0, 1280, 720)]
        [TestCase(FullScreenMode.Windowed, 1280, 720, 2560, 1440, 1280, 720)]
        [TestCase(FullScreenMode.ExclusiveFullScreen, 1280, 720, 2560, 1440, 1280, 720)]
        public void UsesNativePixelsOnlyForBorderlessFullscreen(FullScreenMode mode, int width, int height,
            int monitorWidth, int monitorHeight, int expectedWidth, int expectedHeight)
        {
            Assert.That(NativeDesktopResolution.TargetResolution(mode, new Vector2Int(width, height),
                new Vector2Int(monitorWidth, monitorHeight)), Is.EqualTo(new Vector2Int(expectedWidth, expectedHeight)));
        }
    }
}
