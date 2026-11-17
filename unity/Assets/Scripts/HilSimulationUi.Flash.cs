using UnityEngine;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration
{
    public sealed partial class HilSimulationUi
    {
        private GameObject flashCanvas;
        private RectTransform flashPanel;
        private Image flashFill;
        private Sprite flashSprite;
        private Text flashStage, flashPercent, flashCancelLabel;
        private Button flashCancel;
        private bool flashCancelRequested;
        private string flashRun;

        private void CreateFlashProgress()
        {
            flashCanvas = new GameObject("HIL_FlashProgressCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(flashCanvas);
            var canvas = flashCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;
            var scaler = flashCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var shade = GarageVisualStyle.Panel(flashCanvas.transform, "Shade", new Color(0, 0, 0, .65f));
            GarageVisualStyle.Rect(shade.transform, Vector2.zero, Vector2.one);
            var panel = GarageVisualStyle.Panel(shade.transform, "HilFlashProgress", GarageVisualStyle.Surface);
            flashPanel = panel.GetComponent<RectTransform>();
            flashPanel.anchorMin = flashPanel.anchorMax = flashPanel.pivot = new Vector2(.5f, .5f);
            flashPanel.anchoredPosition = Vector2.zero;
            GarageVisualStyle.Label(panel.transform, "Title", "烧录6750固件", 23,
                new Vector2(.07f, .74f), new Vector2(.93f, .94f));
            flashStage = GarageVisualStyle.Label(panel.transform, "Stage", "", 18,
                new Vector2(.07f, .47f), new Vector2(.93f, .68f));
            flashStage.resizeTextForBestFit = true;
            flashStage.resizeTextMinSize = 13; flashStage.resizeTextMaxSize = 18;
            var track = GarageVisualStyle.Panel(panel.transform, "ProgressTrack", GarageVisualStyle.Raised);
            GarageVisualStyle.Rect(track.transform, new Vector2(.07f, .34f), new Vector2(.81f, .40f));
            flashFill = GarageVisualStyle.Panel(track.transform, "ProgressFill", GarageVisualStyle.Accent).GetComponent<Image>();
            flashSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width,
                Texture2D.whiteTexture.height), new Vector2(.5f, .5f));
            flashFill.sprite = flashSprite;
            flashFill.type = Image.Type.Filled; flashFill.fillMethod = Image.FillMethod.Horizontal;
            flashFill.fillOrigin = 0;
            GarageVisualStyle.Rect(flashFill.transform, Vector2.zero, Vector2.one);
            flashPercent = GarageVisualStyle.Label(panel.transform, "Percent", "", 16,
                new Vector2(.83f, .28f), new Vector2(.94f, .46f));
            flashPercent.alignment = TextAnchor.MiddleRight;
            var cancel = GarageVisualStyle.Panel(panel.transform, "CancelStartup", GarageVisualStyle.Raised);
            GarageVisualStyle.Rect(cancel.transform, new Vector2(.59f, .07f), new Vector2(.93f, .25f));
            flashCancel = cancel.AddComponent<Button>(); GarageVisualStyle.ButtonStyle(flashCancel);
            flashCancelLabel = GarageVisualStyle.Label(cancel.transform, "Label", "取消启动", 16, Vector2.zero, Vector2.one);
            flashCancelLabel.alignment = TextAnchor.MiddleCenter;
            flashCancel.onClick.AddListener(() =>
            {
                flashCancelRequested = true;
                Stop(null);
            });
        }

        private void RefreshFlashProgress(HilRunStatus status)
        {
            if (status.phase != "flashing")
            {
                if (flashCanvas != null) flashCanvas.SetActive(false);
                return;
            }
            if (flashCanvas == null) CreateFlashProgress();
            if (flashRun != status.run_id) { flashRun = status.run_id; flashCancelRequested = false; }
            flashCanvas.SetActive(true);
            float width = ((RectTransform)flashCanvas.transform).rect.width;
            flashPanel.sizeDelta = new Vector2(Mathf.Min(560, width - 32), 230);
            flashFill.fillAmount = Mathf.Clamp01(status.flash_progress);
            flashPercent.text = Mathf.RoundToInt(flashFill.fillAmount * 100) + "%";
            flashStage.text = string.IsNullOrEmpty(status.flash_stage) ? "正在连接6750烧录器" : status.flash_stage;
            flashCancel.interactable = !flashCancelRequested && !busy;
            flashCancelLabel.text = flashCancelRequested ? "等待校验完成" : "取消启动";
        }

        private void OnDestroy()
        {
            if (flashCanvas != null) Destroy(flashCanvas);
            if (flashSprite != null) Destroy(flashSprite);
        }
    }
}
