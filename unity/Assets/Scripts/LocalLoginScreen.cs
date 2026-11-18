using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using Agribot.VehicleConfiguration;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LocalLoginScreen : MonoBehaviour
{
    public const string SceneName = "Login";
    private const string Account = "admin";
    // This verifier is for a local startup gate, not a server-side security boundary.
    private const string PasswordDigest = "oQlz1a8t1Wv/AntwAWB4Msx5hIFMKlwZzOmaOxTvdvg=";
    private static readonly Color ErrorColor = new(.98f, .65f, .53f);
    private TMP_InputField account;
    private TMP_InputField password;
    private Text status;
    private Button login;
    private EventSystem events;
    private Image vehicleMark;
    private Texture2D vehicleTexture;
    private bool loading;

    private void Awake()
    {
        var camera = new GameObject("LoginCamera", typeof(Camera)).GetComponent<Camera>();
        camera.transform.SetParent(transform, false);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = GarageVisualStyle.Background;
        camera.cullingMask = 0;

        var controls = new GameObject("LoginEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        controls.transform.SetParent(transform, false);
        events = controls.GetComponent<EventSystem>();

        var overlay = new GameObject("LoginCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        overlay.transform.SetParent(transform, false);
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1000, 680);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        var backdrop = GarageVisualStyle.Panel(overlay.transform, "LoginBackground", GarageVisualStyle.Background);
        GarageVisualStyle.Rect(backdrop.transform, Vector2.zero, Vector2.one);
        var form = GarageVisualStyle.Panel(backdrop.transform, "LoginForm", GarageVisualStyle.Surface);
        var formRect = (RectTransform)form.transform;
        formRect.anchorMin = formRect.anchorMax = new Vector2(.5f, .5f);
        formRect.sizeDelta = new Vector2(424, 504);

        var accent = GarageVisualStyle.Panel(form.transform, "LoginAccent", GarageVisualStyle.Accent);
        Place(accent.transform, 0, 3, 0);
        var mark = new GameObject("StartupVehicle", typeof(RectTransform), typeof(Image));
        mark.transform.SetParent(form.transform, false);
        var markRect = (RectTransform)mark.transform;
        markRect.anchorMin = markRect.anchorMax = markRect.pivot = new Vector2(.5f, 1);
        markRect.sizeDelta = new Vector2(194, 121);
        vehicleMark = mark.GetComponent<Image>();
        vehicleTexture = GarageThumbnailLibrary.LoadTexture("Car4");
        vehicleMark.sprite = Sprite.Create(vehicleTexture, new Rect(0, 0, vehicleTexture.width, vehicleTexture.height),
            new Vector2(.5f, .5f));
        vehicleMark.preserveAspect = true;
        vehicleMark.raycastTarget = false;

        AddLabel(form.transform, "LoginTitle", "农机配置与算法仿真", 28, 130, 44).alignment = TextAnchor.MiddleCenter;
        AddLabel(form.transform, "AccountLabel", "账号", 18, 196, 24);
        account = AddField(form.transform, "LoginAccount", 224, TMP_InputField.ContentType.Standard);
        AddLabel(form.transform, "PasswordLabel", "密码", 18, 286, 24);
        password = AddField(form.transform, "LoginPassword", 314, TMP_InputField.ContentType.Password);
        status = AddLabel(form.transform, "LoginStatus", "", 16, 367, 26);

        var submit = GarageVisualStyle.Panel(form.transform, "LoginSubmit", GarageVisualStyle.Accent);
        Place(submit.transform, 414, 50);
        login = submit.AddComponent<Button>();
        login.targetGraphic = submit.GetComponent<Image>();
        GarageVisualStyle.SetButtonLabel(login, "登录");
        GarageVisualStyle.ButtonStyle(login, primary: true);
        login.onClick.AddListener(Submit);
        account.onSubmit.AddListener(_ => Focus(password));
        password.onSubmit.AddListener(_ => Submit());
    }

    private void Start()
    {
        Focus(account);
        StartCoroutine(AnimateVehicle());
    }

    private void OnDestroy()
    {
        if (vehicleMark != null) Destroy(vehicleMark.sprite);
        if (vehicleTexture != null) Destroy(vehicleTexture);
    }

    private IEnumerator AnimateVehicle()
    {
        const float duration = .65f;
        float start = Time.unscaledTime;
        float progress;
        do
        {
            progress = Mathf.Clamp01((Time.unscaledTime - start) / duration);
            float eased = 1f - (1f - progress) * (1f - progress);
            vehicleMark.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-42, 0, eased), -18);
            vehicleMark.color = new Color(1, 1, 1, Mathf.Lerp(.15f, 1, eased));
            if (progress < 1f) yield return null;
        } while (progress < 1f);
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (loading || keyboard == null || !keyboard.tabKey.wasPressedThisFrame) return;
        Focus(events.currentSelectedGameObject == account.gameObject ? password : account);
    }

    private void Focus(TMP_InputField field)
    {
        events.SetSelectedGameObject(field.gameObject);
        field.ActivateInputField();
    }

    private void Submit()
    {
        if (loading) return;
        bool emptyAccount = string.IsNullOrWhiteSpace(account.text);
        bool emptyPassword = string.IsNullOrEmpty(password.text);
        if (emptyAccount || emptyPassword)
        {
            status.color = ErrorColor;
            status.text = "请输入账号和密码。";
            Focus(emptyAccount ? account : password);
            return;
        }

        using var sha = SHA256.Create();
        var digest = sha.ComputeHash(Encoding.UTF8.GetBytes($"agribot-local-login:{Account}:{password.text}"));
        password.text = "";
        if (!string.Equals(account.text, Account, StringComparison.Ordinal) ||
            !CryptographicOperations.FixedTimeEquals(digest, Convert.FromBase64String(PasswordDigest)))
        {
            status.color = ErrorColor;
            status.text = "账号或密码错误。";
            Focus(password);
            return;
        }

        loading = true;
        account.interactable = password.interactable = login.interactable = false;
        status.color = GarageVisualStyle.Muted;
        status.text = "正在进入车库…";
        SceneManager.LoadSceneAsync("scene1");
    }

    private static TMP_InputField AddField(Transform parent, string name, int top, TMP_InputField.ContentType type)
    {
        var surface = GarageVisualStyle.Panel(parent, name, GarageVisualStyle.Raised);
        Place(surface.transform, top, 46);
        surface.SetActive(false);
        var input = surface.AddComponent<TMP_InputField>();
        surface.AddComponent<DesktopTextInputBridge>();
        input.contentType = type;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 128;
        input.targetGraphic = surface.GetComponent<Image>();
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(surface.transform, false);
        GarageVisualStyle.Rect(viewport.transform, Vector2.zero, Vector2.one);
        var rect = (RectTransform)viewport.transform;
        rect.offsetMin = new Vector2(14, 4);
        rect.offsetMax = new Vector2(-14, -4);
        input.textViewport = rect;
        var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(rect, false);
        GarageVisualStyle.Rect(text.transform, Vector2.zero, Vector2.one);
        var label = text.GetComponent<TextMeshProUGUI>();
        ChineseUiFont.Apply(label);
        label.fontSize = 20;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.color = GarageVisualStyle.Text;
        label.richText = label.raycastTarget = false;
        input.textComponent = label;
        surface.SetActive(true);
        return input;
    }

    private static Text AddLabel(Transform parent, string name, string value, int size, int top, int height)
    {
        var label = GarageVisualStyle.Label(parent, name, value, size, Vector2.zero, Vector2.one);
        Place(label.transform, top, height);
        label.verticalOverflow = VerticalWrapMode.Overflow;
        return label;
    }

    private static void Place(Transform item, int top, int height, int inset = 36)
    {
        var rect = (RectTransform)item;
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, -top - height);
        rect.offsetMax = new Vector2(-inset, -top);
    }
}
