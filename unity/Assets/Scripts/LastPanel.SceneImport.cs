using System;
using System.Collections;
using System.IO;
using System.Threading;
using Agribot.VehicleConfiguration;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class LastPanel
{
    private RectTransform sceneGrid;
    private ScrollRect sceneScroll;
    private GameObject sceneImportDialog;
    private TMP_InputField sceneImportName;
    private Text sceneImportFile, sceneImportStatus;
    private Image sceneImportFill;
    private Button sceneImportChoose, sceneImportStart;
    private CancellationTokenSource sceneImportRequest;
    private string sceneImportArchive;
    private bool sceneImportBusy;
    private GameObject sceneDeleteDialog;
    private Text sceneDeleteTitle, sceneDeleteStatus;
    private Button sceneDeleteConfirm, sceneDeleteCancel;
    private FarmScenario sceneToDelete;
    private bool sceneDeleteBusy;

    private void BuildSceneGrid()
    {
        if (sceneGrid == null)
        {
            var viewport = GarageVisualStyle.Panel(scenePage.transform, "SceneViewport", GarageVisualStyle.Background);
            GarageVisualStyle.Rect(viewport.transform, Vector2.zero, new Vector2(1, .89f));
            viewport.AddComponent<RectMask2D>();
            sceneScroll = viewport.AddComponent<ScrollRect>();
            sceneScroll.viewport = viewport.GetComponent<RectTransform>();
            sceneScroll.horizontal = false; sceneScroll.vertical = true; sceneScroll.scrollSensitivity = 35;
            sceneScroll.movementType = ScrollRect.MovementType.Clamped;
            var content = new GameObject("SceneGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            sceneGrid = content.GetComponent<RectTransform>();
            sceneGrid.anchorMin = new Vector2(0, 1); sceneGrid.anchorMax = Vector2.one;
            sceneGrid.pivot = new Vector2(.5f, 1); sceneGrid.sizeDelta = Vector2.zero;
            sceneScroll.content = sceneGrid;
            var grid = content.GetComponent<GridLayoutGroup>();
            grid.spacing = new Vector2(16, 16); grid.padding = new RectOffset(0, 2, 4, 4);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            content.AddComponent<SceneGridSizer>();
        }
        foreach (Transform child in sceneGrid) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        foreach (var image in setupImages) if (image != null) Destroy(image);
        setupImages.Clear(); sceneButtons.Clear();
        foreach (var scene in FarmScenarioCatalog.Scenarios)
        {
            var card = GarageVisualStyle.Panel(sceneGrid, "Scenario_" + scene.id, GarageVisualStyle.Surface);
            var button = card.AddComponent<Button>(); GarageVisualStyle.ButtonStyle(button);
            byte[] image = null;
            if (scene.imported)
            {
                try { if (File.Exists(scene.previewFile)) image = File.ReadAllBytes(scene.previewFile); } catch (IOException) { }
            }
            else image = Resources.Load<TextAsset>(GarageThumbnailLibrary.ResourceFolder + "/Scenario_" + scene.id)?.bytes;
            if (image != null)
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                setupImages.Add(texture);
                if (texture.LoadImage(image))
                {
                    var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
                    setupImages.Add(sprite);
                    var frame = GarageVisualStyle.Panel(card.transform, "ScenePreviewFrame", Color.black);
                    GarageVisualStyle.Rect(frame.transform, new Vector2(.015f, .34f), new Vector2(.985f, .98f));
                    frame.AddComponent<RectMask2D>();
                    var preview = GarageVisualStyle.Panel(frame.transform, "ScenePreview", Color.white).GetComponent<Image>();
                    GarageVisualStyle.Rect(preview.transform, Vector2.zero, Vector2.one);
                    preview.sprite = sprite; preview.raycastTarget = false;
                    var fit = preview.gameObject.AddComponent<AspectRatioFitter>();
                    fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                    fit.aspectRatio = (float)texture.width / texture.height;
                }
            }
            var title = GarageVisualStyle.Label(card.transform, "SceneName", scene.name, 18,
                new Vector2(.045f, .14f), new Vector2(.96f, .34f));
            title.resizeTextForBestFit = true; title.resizeTextMinSize = 12; title.resizeTextMaxSize = 18;
            GarageVisualStyle.Label(card.transform, "SceneTasks", (scene.imported ? "用户导入 / " : "内置默认 / ") +
                string.Join(" / ", scene.tasks ?? Array.Empty<string>()), 12,
                new Vector2(.045f, .015f), new Vector2(.96f, .14f), GarageVisualStyle.Muted);
            sceneButtons.Add((scene, button));
            button.onClick.AddListener(() => { FarmScenarioCatalog.SelectedId = scene.id; RefreshScenes(); });
            if (scene.imported) AddSceneDeleteButton(card.transform, scene);
        }
    }

    private void AddSceneDeleteButton(Transform card, FarmScenario scene)
    {
        var surface = GarageVisualStyle.Panel(card, "DeleteScene", GarageVisualStyle.Raised);
        var rect = surface.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-8, -8); rect.sizeDelta = new Vector2(36, 36);
        var label = GarageVisualStyle.Label(surface.transform, "Label", "×", 26, Vector2.zero, Vector2.one);
        label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
        var button = surface.AddComponent<Button>(); GarageVisualStyle.ButtonStyle(button);
        button.onClick.AddListener(() => OpenSceneDelete(scene));
        var tooltip = GarageVisualStyle.Panel(surface.transform, "Tooltip", GarageVisualStyle.Raised);
        var tipRect = tooltip.GetComponent<RectTransform>();
        tipRect.anchorMin = tipRect.anchorMax = new Vector2(1, 0); tipRect.pivot = Vector2.one;
        tipRect.anchoredPosition = new Vector2(0, -4); tipRect.sizeDelta = new Vector2(100, 28);
        tooltip.GetComponent<Image>().raycastTarget = false;
        var tipLabel = GarageVisualStyle.Label(tooltip.transform, "Text", "删除导入场景", 13, Vector2.zero, Vector2.one);
        tipLabel.alignment = TextAnchor.MiddleCenter; tipLabel.raycastTarget = false;
        surface.AddComponent<SceneDeleteTooltip>().tip = tooltip; tooltip.SetActive(false);
    }

    private void OpenSceneDelete(FarmScenario scene)
    {
        if (!scene.imported || sceneImportBusy || sceneDeleteBusy) return;
        if (sceneDeleteDialog == null)
        {
            sceneDeleteDialog = GarageVisualStyle.Panel(algorithmDialog.transform, "SceneDeleteDialog", new Color(.015f, .02f, .025f, .94f));
            GarageVisualStyle.Rect(sceneDeleteDialog.transform, Vector2.zero, Vector2.one);
            var body = GarageVisualStyle.Panel(sceneDeleteDialog.transform, "DeleteBody", GarageVisualStyle.Surface);
            GarageVisualStyle.Rect(body.transform, new Vector2(.27f, .32f), new Vector2(.73f, .69f));
            sceneDeleteTitle = GarageVisualStyle.Label(body.transform, "Title", "", 24,
                new Vector2(.06f, .66f), new Vector2(.94f, .94f));
            sceneDeleteTitle.resizeTextForBestFit = true; sceneDeleteTitle.resizeTextMinSize = 15; sceneDeleteTitle.resizeTextMaxSize = 24;
            GarageVisualStyle.Label(body.transform, "Message", "同时删除转换缓存，保留原始 ZIP。共用缓存将在最后一个场景删除时清理。", 17,
                new Vector2(.06f, .38f), new Vector2(.94f, .65f), GarageVisualStyle.Muted);
            sceneDeleteStatus = GarageVisualStyle.Label(body.transform, "Status", "", 15,
                new Vector2(.06f, .22f), new Vector2(.94f, .36f), GarageVisualStyle.Muted);
            sceneDeleteConfirm = SetupButton(body.transform, "ConfirmSceneDelete", "删除", .73f, .045f, .94f, .20f, true);
            sceneDeleteCancel = SetupButton(body.transform, "CancelSceneDelete", "取消", .49f, .045f, .70f, .20f);
            sceneDeleteConfirm.onClick.AddListener(() => StartCoroutine(DeleteSelectedScene()));
            sceneDeleteCancel.onClick.AddListener(() => { if (!sceneDeleteBusy) sceneDeleteDialog.SetActive(false); });
        }
        sceneToDelete = scene;
        sceneDeleteTitle.text = "删除场景“" + scene.name + "”？"; sceneDeleteStatus.text = "";
        sceneDeleteDialog.SetActive(true); sceneDeleteDialog.transform.SetAsLastSibling();
    }

    private IEnumerator DeleteSelectedScene()
    {
        if (sceneDeleteBusy || sceneToDelete == null || sceneImportBusy) yield break;
        sceneDeleteBusy = true; sceneDeleteConfirm.interactable = sceneDeleteCancel.interactable = false;
        sceneDeleteStatus.text = "正在删除…";
        RefreshLaunchAvailability();
        var task = ScannedSceneImporter.Delete(sceneToDelete.id, CancellationToken.None);
        while (!task.IsCompleted) yield return null;
        if (this == null) yield break;
        sceneDeleteBusy = false; sceneDeleteConfirm.interactable = sceneDeleteCancel.interactable = true;
        RefreshLaunchAvailability();
        if (task.IsFaulted)
        {
            FarmScenarioCatalog.RefreshImportedScenes();
            if (!Array.Exists(FarmScenarioCatalog.Scenarios, scene => scene.id == sceneToDelete.id))
            {
                FarmScenarioCatalog.ImportedSceneDeleted(sceneToDelete);
                BuildSceneGrid(); RefreshScenes();
            }
            sceneDeleteStatus.text = task.Exception.GetBaseException().Message; yield break;
        }
        FarmScenarioCatalog.ImportedSceneDeleted(task.Result);
        BuildSceneGrid(); RefreshScenes(); sceneDeleteDialog.SetActive(false); sceneToDelete = null;
    }

    private void OpenSceneImport()
    {
        if (sceneDeleteBusy) return;
        if (sceneImportDialog == null) BuildSceneImportDialog();
        if (sceneImportBusy) { sceneImportDialog.SetActive(true); sceneImportDialog.transform.SetAsLastSibling(); return; }
        sceneImportRequest?.Dispose(); sceneImportRequest = new CancellationTokenSource();
        sceneImportArchive = ""; sceneImportName.text = "";
        sceneImportFile.text = "未选择文件"; sceneImportStatus.text = "";
        SetImportProgress(0); SetSceneImportBusy(false);
        sceneImportDialog.SetActive(true); sceneImportDialog.transform.SetAsLastSibling();
        AssistantConversation.RecordUi("打开场景导入", state => state.page = "scene_import");
    }

    private void BuildSceneImportDialog()
    {
        sceneImportDialog = GarageVisualStyle.Panel(algorithmDialog.transform, "SceneImportDialog", new Color(.015f, .02f, .025f, .94f));
        GarageVisualStyle.Rect(sceneImportDialog.transform, Vector2.zero, Vector2.one);
        var body = GarageVisualStyle.Panel(sceneImportDialog.transform, "ImportBody", GarageVisualStyle.Surface);
        GarageVisualStyle.Rect(body.transform, new Vector2(.22f, .25f), new Vector2(.78f, .77f));
        GarageVisualStyle.Label(body.transform, "Title", "导入场景", 25, new Vector2(.05f, .83f), new Vector2(.95f, .98f));
        GarageVisualStyle.Label(body.transform, "NameLabel", "场景名称", 16, new Vector2(.05f, .70f), new Vector2(.95f, .79f));
        sceneImportName = CreateSceneNameInput(body.transform);
        sceneImportFile = GarageVisualStyle.Label(body.transform, "ArchivePath", "未选择文件", 14,
            new Vector2(.05f, .34f), new Vector2(.70f, .47f), GarageVisualStyle.Muted);
        sceneImportFile.resizeTextForBestFit = true; sceneImportFile.resizeTextMinSize = 11; sceneImportFile.resizeTextMaxSize = 14;
        sceneImportChoose = SetupButton(body.transform, "ChooseArchive", "选择 ZIP", .73f, .35f, .95f, .47f);
        sceneImportChoose.onClick.AddListener(() => StartCoroutine(PickSceneArchive()));
        var bar = GarageVisualStyle.Panel(body.transform, "ProgressTrack", GarageVisualStyle.Raised);
        GarageVisualStyle.Rect(bar.transform, new Vector2(.05f, .275f), new Vector2(.95f, .29f));
        sceneImportFill = GarageVisualStyle.Panel(bar.transform, "ProgressFill", GarageVisualStyle.Accent).GetComponent<Image>();
        GarageVisualStyle.Rect(sceneImportFill.transform, Vector2.zero, Vector2.one);
        SetImportProgress(0);
        sceneImportStatus = GarageVisualStyle.Label(body.transform, "Status", "", 14,
            new Vector2(.05f, .14f), new Vector2(.95f, .26f), GarageVisualStyle.Muted);
        sceneImportStatus.resizeTextForBestFit = true; sceneImportStatus.resizeTextMinSize = 11; sceneImportStatus.resizeTextMaxSize = 14;
        var cancel = SetupButton(body.transform, "CancelImport", "取消", .49f, .025f, .70f, .13f);
        cancel.onClick.AddListener(CancelSceneImport);
        sceneImportStart = SetupButton(body.transform, "StartImport", "导入", .73f, .025f, .95f, .13f, true);
        sceneImportStart.onClick.AddListener(() => StartCoroutine(ImportSelectedScene()));
    }

    private TMP_InputField CreateSceneNameInput(Transform parent)
    {
        var surface = GarageVisualStyle.Panel(parent, "SceneNameInput", GarageVisualStyle.Raised);
        surface.SetActive(false);
        GarageVisualStyle.Rect(surface.transform, new Vector2(.05f, .52f), new Vector2(.95f, .69f));
        var input = surface.AddComponent<TMP_InputField>();
        surface.AddComponent<DesktopTextInputBridge>(); input.characterLimit = 48;
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(surface.transform, false);
        GarageVisualStyle.Rect(viewport.transform, Vector2.zero, Vector2.one);
        var rect = viewport.GetComponent<RectTransform>(); rect.offsetMin = new Vector2(12, 5); rect.offsetMax = new Vector2(-12, -5);
        var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(viewport.transform, false); GarageVisualStyle.Rect(text.transform, Vector2.zero, Vector2.one);
        var label = text.GetComponent<TextMeshProUGUI>(); ChineseUiFont.Apply(label);
        label.fontSize = 19; label.color = GarageVisualStyle.Text; label.richText = false;
        label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = false;
        input.textViewport = rect; input.textComponent = label;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.onValueChanged.AddListener(_ => RefreshSceneImportButton());
        surface.SetActive(true); return input;
    }

    private IEnumerator PickSceneArchive()
    {
        if (sceneImportBusy) yield break;
        sceneImportChoose.interactable = false;
        var task = ScannedSceneImporter.PickArchive(sceneImportRequest.Token);
        while (!task.IsCompleted) yield return null;
        if (this == null) yield break;
        sceneImportChoose.interactable = true;
        if (task.IsCanceled) yield break;
        if (task.IsFaulted) sceneImportStatus.text = task.Exception.GetBaseException().Message;
        else if (!string.IsNullOrEmpty(task.Result))
        {
            sceneImportArchive = task.Result; sceneImportFile.text = Path.GetFileName(task.Result);
            if (string.IsNullOrWhiteSpace(sceneImportName.text))
                sceneImportName.text = Path.GetFileNameWithoutExtension(task.Result);
        }
        RefreshSceneImportButton();
    }

    private IEnumerator ImportSelectedScene()
    {
        if (sceneImportBusy || !File.Exists(sceneImportArchive) || string.IsNullOrWhiteSpace(sceneImportName.text)) yield break;
        SetSceneImportBusy(true);
        var progress = new Progress<SceneImportProgress>(value =>
        {
            if (this == null || sceneImportStatus == null) return;
            sceneImportStatus.text = value.stage + "  " + Mathf.RoundToInt(value.progress * 100) + "%";
            SetImportProgress(value.progress);
        });
        var task = ScannedSceneImporter.Import(sceneImportArchive, sceneImportName.text.Trim(), progress, sceneImportRequest.Token);
        while (!task.IsCompleted) yield return null;
        if (this == null) yield break;
        SetSceneImportBusy(false);
        if (task.IsCanceled) { sceneImportStatus.text = "已取消"; yield break; }
        if (task.IsFaulted) { sceneImportStatus.text = task.Exception.GetBaseException().Message; yield break; }
        FarmScenarioCatalog.RefreshImportedScenes();
        FarmScenarioCatalog.SelectedId = task.Result.id;
        BuildSceneGrid(); RefreshScenes();
        sceneImportDialog.SetActive(false);
        Canvas.ForceUpdateCanvases(); sceneScroll.verticalNormalizedPosition = 0;
        AssistantConversation.SceneImported(task.Result);
    }

    private void CancelSceneImport()
    {
        sceneImportRequest?.Cancel();
        if (sceneImportDialog != null) sceneImportDialog.SetActive(false);
    }

    private void SetSceneImportBusy(bool value)
    {
        sceneImportBusy = value;
        sceneImportName.interactable = !value; sceneImportChoose.interactable = !value;
        RefreshSceneImportButton(); RefreshLaunchAvailability();
    }

    private void RefreshSceneImportButton()
    {
        if (sceneImportStart != null)
            sceneImportStart.interactable = !sceneImportBusy && File.Exists(sceneImportArchive) && !string.IsNullOrWhiteSpace(sceneImportName.text);
    }

    private void SetImportProgress(float value)
    {
        sceneImportFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
        sceneImportFill.rectTransform.offsetMin = sceneImportFill.rectTransform.offsetMax = Vector2.zero;
    }
}

public sealed class SceneDeleteTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject tip;
    public void OnPointerEnter(PointerEventData _) { if (tip != null) tip.SetActive(true); }
    public void OnPointerExit(PointerEventData _) { if (tip != null) tip.SetActive(false); }
    private void OnDisable() { if (tip != null) tip.SetActive(false); }
}

public sealed class SceneGridSizer : MonoBehaviour
{
    private float lastWidth = -1;
    private void LateUpdate()
    {
        var grid = GetComponent<GridLayoutGroup>();
        float width = ((RectTransform)transform.parent).rect.width;
        if (width <= 0 || Mathf.Abs(width - lastWidth) < .5f) return;
        lastWidth = width;
        grid.constraintCount = width < 680 ? 2 : 3;
        float cell = (width - grid.padding.horizontal - grid.spacing.x * (grid.constraintCount - 1)) / grid.constraintCount;
        grid.cellSize = new Vector2(cell, Mathf.Max(140, Mathf.Min(210, cell * .54f)));
    }
}
