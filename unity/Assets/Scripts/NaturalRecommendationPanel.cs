using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Agribot.VehicleConfiguration;
using AgriculturalMachinery.BagSystem;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public sealed class NaturalRecommendationPanel : MonoBehaviour
{
    private GameObject dialog;
    private TMP_InputField input;
    private Text status;
    private Transform results;
    private Button submit;
    private CancellationTokenSource request;
    private bool busy;
    private readonly Color ink = new(.92f, .94f, .94f);
    private readonly Color green = new(.75f, .88f, .27f);
    private Text heading;
    private Button assistantEntry;
    private Button newConversation;
    private ScrollRect conversationScroll;
    private readonly List<UnityEngine.Object> previewImages = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() => Ensure(null);

    private void Awake() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        if (dialog != null) Close();
    }

    public static NaturalRecommendationPanel Ensure(Transform canvas)
    {
        var existing = FindObjectsByType<NaturalRecommendationPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (existing != null) return existing;
        var overlay = new GameObject("RecommendationCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        overlay.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.GetComponent<Canvas>().sortingOrder = 100;
        DontDestroyOnLoad(overlay);
        var scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.matchWidthOrHeight = .5f;
        var root = new GameObject("NaturalRecommendation", typeof(RectTransform));
        root.transform.SetParent(overlay.transform, false);
        Rect(root.transform, Vector2.zero, Vector2.one);
        var panel = root.AddComponent<NaturalRecommendationPanel>();
        panel.assistantEntry = panel.Button(root.transform, "GlobalAssistant", "AI 助手", new Color(.17f, .21f, .23f));
        var entryRect = (RectTransform)panel.assistantEntry.transform;
        entryRect.anchorMin = entryRect.anchorMax = new Vector2(1f, 0f);
        entryRect.pivot = new Vector2(1f, 0f); entryRect.anchoredPosition = new Vector2(-26f, 14f);
        entryRect.sizeDelta = new Vector2(130f, 42f);
        panel.assistantEntry.onClick.AddListener(panel.Open);
        return panel;
    }

    public static void AddEntry(Transform parent, Transform canvas, bool garage)
    {
        Ensure(canvas);
    }

    public void Open()
    {
        if (dialog == null) Build();
        RefreshState();
        RenderConversation();
        dialog.SetActive(true);
        transform.SetAsLastSibling();
        assistantEntry.gameObject.SetActive(false);
        input.ActivateInputField();
    }

    public void OpenMode(string selected)
    {
        Open();
    }

    private void Build()
    {
        dialog = Surface(transform, "RecommendationDialog", new Color(.02f, .04f, .04f, .9f));
        Rect(dialog.transform, Vector2.zero, Vector2.one);
        var body = Surface(dialog.transform, "RecommendationBody", new Color(.065f, .08f, .09f));
        Rect(body.transform, new Vector2(.09f, .06f), new Vector2(.91f, .94f));
        heading = Label(body.transform, "Title", "AI 助手", 28, new Vector2(.035f, .9f), new Vector2(.65f, .975f), ink, true);
        newConversation = Button(body.transform, "NewConversation", "新对话", new Color(.20f, .25f, .26f));
        Rect(newConversation.transform, new Vector2(.71f, .915f), new Vector2(.84f, .975f));
        newConversation.onClick.AddListener(StartNewConversation);
        var close = Button(body.transform, "CloseRecommendation", "关闭", new Color(.29f, .34f, .34f));
        Rect(close.transform, new Vector2(.86f, .915f), new Vector2(.97f, .975f));
        close.onClick.AddListener(Close);
        var field = Surface(body.transform, "RecommendationInput", new Color(.13f, .16f, .17f));
        field.SetActive(false);
        Rect(field.transform, new Vector2(.035f, .09f), new Vector2(.79f, .23f));
        input = field.AddComponent<AssistantInputField>();
        field.AddComponent<DesktopTextInputBridge>();
        input.characterLimit = 2000;
        input.lineType = TMP_InputField.LineType.MultiLineNewline;
        input.onSubmit.AddListener(_ => SubmitQuestion());
        var viewport = new GameObject("InputViewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(field.transform, false);
        Rect(viewport.transform, Vector2.zero, Vector2.one);
        ((RectTransform)viewport.transform).offsetMin = new Vector2(16, 12);
        ((RectTransform)viewport.transform).offsetMax = new Vector2(-16, -12);
        TMP_Text InputText(string name, string value, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(viewport.transform, false); Rect(obj.transform, Vector2.zero, Vector2.one);
            var label = obj.GetComponent<TextMeshProUGUI>(); ChineseUiFont.Apply(label);
            label.fontSize = 19; label.color = color; label.text = value; label.raycastTarget = false;
            label.richText = false; return label;
        }
        var text = InputText("Text", "", ink);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        var placeholder = InputText("Placeholder", "我想在葡萄园使用小型履带车，搭配机械臂。", new Color(.55f, .61f, .62f));
        input.textViewport = (RectTransform)viewport.transform;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.targetGraphic = field.GetComponent<Image>();
        field.SetActive(true);
        submit = Button(body.transform, "RequestRecommendation", "发送", green);
        Rect(submit.transform, new Vector2(.81f, .12f), new Vector2(.97f, .21f));
        submit.onClick.AddListener(SubmitQuestion);
        status = Label(body.transform, "RecommendationStatus", "", 16,
            new Vector2(.035f, .025f), new Vector2(.97f, .08f), ink);

        var scrollObject = Surface(body.transform, "RecommendationScroll", new Color(.09f, .11f, .12f));
        Rect(scrollObject.transform, new Vector2(.035f, .26f), new Vector2(.97f, .89f));
        var resultViewport = Surface(scrollObject.transform, "Viewport", new Color(.09f, .11f, .12f));
        Rect(resultViewport.transform, Vector2.zero, Vector2.one);
        resultViewport.AddComponent<RectMask2D>();
        var content = new GameObject("RecommendationResults", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(resultViewport.transform, false);
        var cr = (RectTransform)content.transform;
        cr.anchorMin = new Vector2(0, 1); cr.anchorMax = Vector2.one; cr.pivot = new Vector2(.5f, 1);
        cr.sizeDelta = Vector2.zero;
        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10); layout.spacing = 10;
        layout.childControlHeight = true; layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = scrollObject.AddComponent<ScrollRect>();
        scroll.viewport = (RectTransform)resultViewport.transform; scroll.content = cr;
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28;
        conversationScroll = scroll;
        results = content.transform;
    }

    private void RenderConversation()
    {
        foreach (var image in previewImages) Destroy(image);
        previewImages.Clear();
        foreach (Transform child in results) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        var messages = AssistantConversation.Entries.Where(entry => entry.kind == "chat").TakeLast(80).ToArray();
        if (messages.Length == 0)
            FlowText(results, "AssistantGreeting", "有什么需要我帮忙的？", 24, green, true);
        foreach (var entry in messages)
        {
            if (entry.role == "user")
                FlowText(results, "UserMessage", "你：" + entry.content, 19, green);
            else if (entry.response != null) Present(entry.response);
        }
        Canvas.ForceUpdateCanvases();
        conversationScroll.verticalNormalizedPosition = 0f;
        if (busy) status.text = "正在思考…";
    }

    private void StartNewConversation()
    {
        if (busy) return;
        RefreshState();
        AssistantConversation.NewConversation();
        input.text = "";
        status.text = "";
        RenderConversation();
        input.ActivateInputField();
    }

    private void SubmitQuestion()
    {
        if (!busy) StartCoroutine(Query());
    }

    private IEnumerator Query()
    {
        if (busy) yield break;
        if (string.IsNullOrWhiteSpace(input.text)) { status.text = "请先输入任务需求。"; yield break; }
        request?.Dispose(); request = new CancellationTokenSource();
        SetBusy(true); status.text = "正在思考…";
        string question = input.text;
        RefreshState();
        var context = AssistantConversation.Snapshot();
        var history = AssistantConversation.History();
        AssistantConversation.User(question);
        input.text = "";
        RenderConversation();
        var task = NaturalRecommendationClient.Query(question, request.Token, "assistant", context, history, AssistantConversation.Memory);
        yield return Await(task);
        SetBusy(false);
        if (task.IsCanceled) { status.text = "已取消。"; yield break; }
        if (task.IsFaulted) { status.text = task.Exception.GetBaseException().Message; yield break; }
        AssistantConversation.Assistant(task.Result);
        RenderConversation();
        input.ActivateInputField();
    }

    public void Present(NaturalRecommendationResponse response)
    {
        if (dialog == null) Build();
        var rows = response.recommendations ?? Array.Empty<NaturalRecommendation>();
        status.text = rows.Length > 0 ? $"为你准备了 {rows.Length} 个方案" : "助手回复";
        if (!string.IsNullOrWhiteSpace(response.answer)) FlowText(results, "AssistantAnswer", response.answer, 18, ink);
        if (!string.IsNullOrWhiteSpace(response.clarification)) FlowText(results, "Clarification", response.clarification, 18, ink);
        foreach (var item in rows)
        {
            var card = Surface(results, "Recommendation_" + item.car, new Color(.14f, .17f, .18f));
            var stack = card.AddComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset(18, 18, 14, 14); stack.spacing = 8;
            stack.childControlHeight = true; stack.childControlWidth = true; stack.childForceExpandHeight = false;
            var profile = RosSimulationLauncher.FindAlgorithm(item.algorithm);
            if (item.action != "algorithm")
            {
                var texture = GarageThumbnailLibrary.LoadTexture(item.car);
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
                previewImages.Add(texture); previewImages.Add(sprite);
                var preview = Surface(card.transform, "VehiclePreview", Color.white);
                preview.GetComponent<Image>().sprite = sprite; preview.GetComponent<Image>().preserveAspect = true;
                preview.GetComponent<Image>().raycastTarget = false;
                preview.AddComponent<LayoutElement>().preferredHeight = 140;
            }
            FlowText(card.transform, "Vehicle", item.car + " · " + item.vehicleName + " + " + item.implementName, 22, ink, true);
            string recipe = item.action == "algorithm" ? item.sceneName + "   |   " + profile.Localization + " / " + profile.Planner + " / " + profile.Controller :
                item.sceneName + (item.geometry == null ? "" : $"   |   车长 {item.geometry.lengthM:F2} m · 转弯半径 {item.geometry.minTurningRadiusM:F2} m");
            FlowText(card.transform, "Recipe", recipe,
                17, green);
            FlowText(card.transform, "Reason", item.reason ?? "", 17, ink);
            var apply = Button(card.transform, "ApplyRecommendation", item.action == "algorithm" ? "采用这套算法" : "选择这套装配", green);
            apply.gameObject.AddComponent<LayoutElement>().preferredHeight = 42;
            apply.onClick.AddListener(() => StartCoroutine(ValidateAndApply(item)));
        }
    }

    private IEnumerator ValidateAndApply(NaturalRecommendation item)
    {
        if (busy) yield break;
        if (item.action == "algorithm" && !MatchesCurrentAssembly(item))
        { status.text = "当前装配或场景已变化，请重新推荐算法。"; yield break; }
        SetBusy(true); status.text = "正在复核当前配置版本…";
        request?.Dispose(); request = new CancellationTokenSource();
        var task = NaturalRecommendationClient.Revalidate(item, request.Token);
        yield return Await(task);
        if (task.IsFaulted || task.IsCanceled)
        {
            SetBusy(false); status.text = task.Exception?.GetBaseException().Message ?? "已取消。";
            yield break;
        }
        var current = task.Result;
        if (item.action == "algorithm")
        {
            if (!MatchesCurrentAssembly(current))
            { SetBusy(false); status.text = "当前装配或场景已变化，请重新推荐算法。"; yield break; }
            FindObjectsByType<LastPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).First().ApplyRecommendation(current);
            AssistantConversation.RecordUi("用户已点击应用算法方案：" + current.car + " / " + current.algorithm);
            Close(); SetBusy(false); yield break;
        }
        yield return ApplyConfiguration(current);
    }

    private IEnumerator ApplyConfiguration(NaturalRecommendation item)
    {
        SetBusy(true); status.text = "正在应用车辆与装备配置…";
        var main = FindObjectsByType<MainPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        string error = null;
        try
        {
            if (item.car != "Car0" && item.car != "Car3")
            {
                string path = Path.Combine(FarmVehicleLibrary.AssetDirectory(item.preset), "vehicle_config.json");
                using var digest = SHA256.Create();
                string hash = BitConverter.ToString(digest.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
                if (hash != item.vehicleConfigSha256)
                    throw new InvalidOperationException("App 中的车辆资源与确认版本不一致，请同步资源并重新构建。");
            }
            if (main == null) throw new InvalidOperationException("未找到共用车库。");
            main.SelectRecommendedVehicle(item.car);
        }
        catch (Exception e) { error = e.Message; }
        if (error != null) { SetBusy(false); status.text = error; yield break; }
        yield return new WaitForSeconds(1f);
        main.OpenRecommendedEquipment();
        yield return new WaitForSeconds(.6f);
        try
        {
            var bag = FindObjectsByType<EquipmentBagController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (bag == null) throw new InvalidOperationException("当前场景缺少装备控制器。");
            if (!bag.IsInitialized) bag.InitializeBag();
            bag.UninstallAllEquipment();
            var slots = new List<EquipmentSlotType> { EquipmentSlotType.Radar, EquipmentSlotType.Camera, EquipmentSlotType.Imu, EquipmentSlotType.RTK };
            if (item.implement != "none") slots.Add(ToolSlot(item.implement));
            foreach (var slot in slots)
            {
                var equipment = bag.GetEquipment(slot);
                bool installed = bag.InstallEquipment(equipment);
                if (!installed) throw new InvalidOperationException("无法安装推荐装备：" + slot);
            }
        }
        catch (Exception e) { error = e.Message; }
        if (error != null) { SetBusy(false); status.text = error; yield break; }
        float deadline = Time.realtimeSinceStartup + 15f;
        while (Time.realtimeSinceStartup < deadline)
        {
            var bag = FindObjectsByType<EquipmentBagController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (bag == null || !bag.IsSceneAnimating) break;
            yield return null;
        }
        yield return new WaitForSeconds(.3f);
        var selector = FindObjectsByType<LastPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        try
        {
            if (selector == null) throw new InvalidOperationException("未找到仿真方案面板。");
            FindObjectsByType<BagPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault()?.Hide();
            selector.ApplyRecommendation(item);
            AssistantConversation.RecordUi("用户已点击应用装配方案：" + item.car + " / " + item.implementName + " / " + item.sceneName + " / " + item.algorithm);
            Close();
        }
        catch (Exception e) { status.text = e.Message; }
        SetBusy(false);
    }

    public static EquipmentSlotType ToolSlot(string id) => id switch
    {
        "lite6" => EquipmentSlotType.Hand, "compact_chisel_plow" => EquipmentSlotType.ChiselPlow,
        "mounted_disc_harrow" => EquipmentSlotType.DiscHarrow, "groove_maker" => EquipmentSlotType.GrooveMaker,
        "mounted_cultivator" => EquipmentSlotType.Cultivator, "forest_chain_crusher" => EquipmentSlotType.ChainCrusher,
        "inter_row_milling_cutter" => EquipmentSlotType.InterrowMill, "front_sickle_bar" => EquipmentSlotType.FrontCutter,
        "garlic_planter" => EquipmentSlotType.GarlicPlanter, "mulch_planter" => EquipmentSlotType.MulchPlanter,
        "farm_trailer" => EquipmentSlotType.FarmTrailer, "combine_header" => EquipmentSlotType.CombineHeader,
        _ => throw new InvalidOperationException("未知机具配置：" + id)
    };

    private static IEnumerator Await(Task task) { while (!task.IsCompleted) yield return null; }
    private void SetBusy(bool value)
    {
        busy = value;
        if (submit != null) submit.interactable = !value;
        if (newConversation != null) newConversation.interactable = !value;
        if (input != null) input.interactable = !value;
        if (results != null) foreach (var button in results.GetComponentsInChildren<Button>()) button.interactable = !value;
    }
    private void Close() { if (dialog != null) dialog.SetActive(false); assistantEntry.gameObject.SetActive(true); }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        request?.Cancel(); request?.Dispose();
        foreach (var image in previewImages) if (image != null) Destroy(image);
    }

    private GameObject Surface(Transform parent, string name, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false); obj.GetComponent<Image>().color = color; return obj;
    }
    private Text Label(Transform parent, string name, string value, int size, Vector2 min, Vector2 max, Color color, bool bold = false)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Text)); obj.transform.SetParent(parent, false);
        Rect(obj.transform, min, max);
        var text = obj.GetComponent<Text>(); ChineseUiFont.Apply(text); text.text = value; text.fontSize = size;
        text.color = color; text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.alignment = TextAnchor.MiddleLeft; text.supportRichText = false; text.raycastTarget = false;
        return text;
    }
    private void FlowText(Transform parent, string name, string value, int size, Color color, bool bold = false)
    {
        var text = Label(parent, name, value, size, Vector2.zero, Vector2.one, color, bold);
        text.verticalOverflow = VerticalWrapMode.Overflow;
    }
    private Button Button(Transform parent, string name, string title, Color color)
    {
        var obj = Surface(parent, name, color); var button = obj.AddComponent<Button>();
        var label = Label(obj.transform, "Label", title, 18, Vector2.zero, Vector2.one, color == green ? new Color(.07f, .09f, .06f) : Color.white);
        label.alignment = TextAnchor.MiddleCenter; return button;
    }

    private static NaturalRecommendationClient.Context CurrentContext()
    {
        string car = AssistantConversation.State.car;
        string tool = "none";
        var farm = FarmGarageVehicle.Visible?.GetComponent<FarmGarageVehicle>();
        if (farm != null) tool = FarmVehicleLibrary.Presets.FirstOrDefault(p => p.id == farm.SelectedPreset)?.implementId ?? "none";
        else
        {
            var bag = FindObjectsByType<EquipmentBagController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (bag != null && bag.IsInstalled(bag.GetEquipment(EquipmentSlotType.Hand))) tool = "lite6";
        }
        var context = AssistantConversation.Snapshot();
        context.car = car; context.scene = FarmScenarioCatalog.SelectedId; context.implement = tool;
        context.sceneName = FarmScenarioCatalog.Selected?.name ?? "";
        context.sceneImported = FarmScenarioCatalog.Selected?.imported ?? false;
        var controller = FindObjectsByType<EquipmentBagController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        context.sensors = new[] { EquipmentSlotType.Radar, EquipmentSlotType.Camera, EquipmentSlotType.Imu, EquipmentSlotType.RTK }
            .Where(s => controller != null && controller.IsInstalled(controller.GetEquipment(s))).Select(s => s.ToString()).ToArray();
        context.simulation = RosSimulationLauncher.ReadManagedStatus();
        return context;
    }

    private static void RefreshState()
    {
        var actual = CurrentContext();
        var state = AssistantConversation.State;
        state.car = actual.car; state.scene = actual.scene; state.implement = actual.implement;
        state.sceneName = actual.sceneName; state.sceneImported = actual.sceneImported;
        state.sensors = actual.sensors; state.simulation = actual.simulation;
    }

    private static bool MatchesCurrentAssembly(NaturalRecommendation item)
    {
        var context = CurrentContext();
        return item.car == context.car && item.scene == context.scene && item.implement == context.implement;
    }
    private static void Rect(Transform target, Vector2 min, Vector2 max)
    {
        var rect = (RectTransform)target; rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
