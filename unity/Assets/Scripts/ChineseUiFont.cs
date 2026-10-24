using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Uses the bundled Noto CJK font for every runtime UI label. No network font
/// download is required in the editor, WebGL build, or Android wrapper.
/// </summary>
public static class ChineseUiFont
{
    private const string FontNamePrefix = "NotoSansCJKsc";
    private static Font legacyFont;
    private static TMP_FontAsset tmpFont;
    private static bool warnedAboutFallback;

    public static Font LegacyFont
    {
        get
        {
            if (legacyFont == null)
                legacyFont = FindBundledFont();
            return legacyFont;
        }
    }

    public static TMP_FontAsset TmpFont
    {
        get
        {
            if (tmpFont == null && LegacyFont != null)
            {
                tmpFont = TMP_FontAsset.CreateFontAsset(LegacyFont);
                if (tmpFont != null)
                    tmpFont.name = "NotoSansCJKsc Runtime SDF";
            }
            return tmpFont;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyToLoadedScene()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        foreach (Text text in Resources.FindObjectsOfTypeAll<Text>())
        {
            if (IsLoadedSceneObject(text.gameObject))
                Apply(text);
        }

        foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (IsLoadedSceneObject(text.gameObject))
                Apply(text);
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyToLoadedScene();

    public static void Apply(Text text)
    {
        if (text != null && LegacyFont != null)
            text.font = LegacyFont;
    }

    public static void Apply(TMP_Text text)
    {
        if (text != null && TmpFont != null)
            text.font = TmpFont;
    }

    private static Font FindBundledFont()
    {
        var bundled = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
        if (bundled != null) return bundled;
        foreach (Font font in Resources.FindObjectsOfTypeAll<Font>())
        {
            if (font != null && font.name.StartsWith(FontNamePrefix))
                return font;
        }

        foreach (Text text in Resources.FindObjectsOfTypeAll<Text>())
        {
            if (text != null && text.font != null &&
                text.font.HasCharacter('中'))
            {
                return text.font;
            }
        }

        Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (!warnedAboutFallback)
        {
            Debug.LogWarning(
                "未找到项目内 NotoSansCJKsc 字体，暂时使用系统回退字体。");
            warnedAboutFallback = true;
        }
        return fallback;
    }

    private static bool IsLoadedSceneObject(GameObject target)
    {
        return target != null &&
               target.scene.IsValid() &&
               target.scene.isLoaded;
    }
}
