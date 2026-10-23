using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AgriculturalMachinery.BagSystem
{
    [DisallowMultipleComponent]
    public sealed class EquipmentDetailsUI : MonoBehaviour
    {
        [Header("装备详情")]
        [SerializeField] private Image equipmentIcon;
        [SerializeField] private TMP_Text equipmentName;
        [SerializeField] private TMP_Text equipmentDescription;

        [Header("操作按钮")]
        [SerializeField] private Button installButton;
        [SerializeField] private Button uninstallButton;
        [SerializeField] private Font actionButtonFont;

        [Header("显示状态")]
        [SerializeField] private GameObject contentRoot;
        [SerializeField] private GameObject emptyState;

        private EquipmentBagController bagController;
        private EquipmentData currentEquipment;

        public EquipmentData CurrentEquipment => currentEquipment;
        public Button InstallButton => installButton;
        public Button UninstallButton => uninstallButton;

        private void Awake()
        {
            ResolveBagController();
            EnsureActionButtons();
            if (equipmentIcon != null) equipmentIcon.raycastTarget = false;
            if (equipmentName != null) equipmentName.raycastTarget = false;
            if (equipmentDescription != null) equipmentDescription.raycastTarget = false;
            installButton.onClick.AddListener(InstallCurrentEquipment);
            uninstallButton.onClick.AddListener(UninstallCurrentEquipment);
            Clear();
        }

        private void OnEnable()
        {
            ResolveBagController();
            if (currentEquipment != null)
            {
                RefreshActionButtons();
            }
        }

        private void OnDestroy()
        {
            if (bagController != null)
            {
                bagController.InstallationChanged -= HandleInstallationChanged;
            }
        }

        public void Show(EquipmentData equipment)
        {
            if (equipment == null)
            {
                Clear();
                return;
            }

            ResolveBagController();
            currentEquipment = equipment;

            if (equipmentIcon != null)
            {
                equipmentIcon.sprite = equipment.Icon;
                equipmentIcon.enabled = equipment.Icon != null;
                FitIconInsideDetails(equipmentIcon, transform as RectTransform);
            }

            if (equipmentName != null)
            {
                equipmentName.text = GetDisplayName(equipment);
                ChineseUiFont.Apply(equipmentName);
            }

            if (equipmentDescription != null)
            {
                equipmentDescription.text = string.Empty;
                equipmentDescription.gameObject.SetActive(false);
            }

            if (contentRoot != null)
            {
                contentRoot.SetActive(true);
            }

            if (emptyState != null)
            {
                emptyState.SetActive(false);
            }

            RefreshActionButtons();
        }

        internal static void FitIconInsideDetails(Image equipmentIcon, RectTransform detailsRect)
        {
            RectTransform iconRect = equipmentIcon != null
                ? equipmentIcon.rectTransform
                : null;
            if (iconRect == null || detailsRect == null)
            {
                return;
            }

            // Start from the normal enlarged size, then clamp it to the
            // EquipmentDetails rectangle so the icon never overflows it.
            iconRect.localScale = Vector3.one * 1.25f;
            Vector2 iconSize = iconRect.rect.size;
            Vector2 detailsSize = detailsRect.rect.size;

            if (iconSize.x <= 0f || iconSize.y <= 0f ||
                detailsSize.x <= 0f || detailsSize.y <= 0f)
            {
                iconRect.localScale = Vector3.one;
                return;
            }

            const float padding = 8f;
            float maxScaleX = Mathf.Max(0f, detailsSize.x - padding) / iconSize.x;
            float maxScaleY = Mathf.Max(0f, detailsSize.y - padding) / iconSize.y;
            float scale = Mathf.Min(1.25f, maxScaleX, maxScaleY);
            iconRect.localScale = Vector3.one * Mathf.Max(0f, scale);
        }

        public void Clear()
        {
            currentEquipment = null;

            if (equipmentIcon != null)
            {
                equipmentIcon.sprite = null;
                equipmentIcon.enabled = false;
                equipmentIcon.rectTransform.localScale = Vector3.one;
            }

            if (equipmentName != null)
            {
                equipmentName.text = string.Empty;
            }

            if (equipmentDescription != null)
            {
                equipmentDescription.text = string.Empty;
                equipmentDescription.gameObject.SetActive(false);
            }

            if (installButton != null)
            {
                installButton.interactable = false;
                installButton.gameObject.SetActive(false);
            }

            if (uninstallButton != null)
            {
                uninstallButton.interactable = false;
                uninstallButton.gameObject.SetActive(false);
            }

            if (emptyState != null)
            {
                emptyState.SetActive(true);
            }

            if (contentRoot != null && contentRoot != gameObject)
            {
                contentRoot.SetActive(false);
            }
        }

        private void InstallCurrentEquipment()
        {
            if (bagController != null && bagController.InstallEquipment(currentEquipment))
            {
                RefreshActionButtons();
            }
        }

        private void UninstallCurrentEquipment()
        {
            if (bagController != null && bagController.UninstallEquipment(currentEquipment))
            {
                RefreshActionButtons();
            }
        }

        private void RefreshActionButtons()
        {
            bool canOperate = bagController != null && currentEquipment != null;
            string unavailable = currentEquipment == null ? null : FarmEquipmentUi.UnavailableReason(
                currentEquipment.EquipmentType, FarmEquipmentUi.CurrentCarName());
            bool installed = canOperate && bagController.IsInstalled(currentEquipment);
            installButton.gameObject.SetActive(canOperate && !installed);
            uninstallButton.gameObject.SetActive(canOperate && installed);
            installButton.interactable = canOperate && !installed && unavailable == null;
            uninstallButton.interactable = canOperate && installed;
            if (equipmentDescription != null)
            {
                equipmentDescription.text = string.Empty;
                equipmentDescription.gameObject.SetActive(false);
            }
        }

        internal static string GetDisplayName(EquipmentData equipment)
        {
            return equipment.EquipmentType switch
            {
                EquipmentType.RADAR => "雷达",
                EquipmentType.RTK => "RTK",
                EquipmentType.CarCamera => "工业相机",
                EquipmentType.ImuBox => "imu",
                EquipmentType.Hand => "机械臂",
                _ => equipment.DisplayName
            };
        }

        private void HandleInstallationChanged(EquipmentData equipment, bool installed)
        {
            if (currentEquipment != null && equipment != null &&
                currentEquipment.ItemId == equipment.ItemId)
            {
                RefreshActionButtons();
            }
        }

        private void ResolveBagController()
        {
            EquipmentBagController resolved = GetComponentInParent<EquipmentBagController>(true);
            if (resolved == bagController)
            {
                return;
            }

            if (bagController != null)
            {
                bagController.InstallationChanged -= HandleInstallationChanged;
            }

            bagController = resolved;
            if (bagController != null)
            {
                bagController.InstallationChanged += HandleInstallationChanged;
            }
        }

        private void EnsureActionButtons()
        {
            if (installButton != null && uninstallButton != null)
            {
                SetButtonLabel(installButton, "安装");
                SetButtonLabel(uninstallButton, "卸下");
                return;
            }

            Transform existing = transform.Find("ActionButtons");
            RectTransform actionRoot = existing as RectTransform;
            if (actionRoot == null)
            {
                GameObject root = new("ActionButtons", typeof(RectTransform));
                root.layer = gameObject.layer;
                actionRoot = root.GetComponent<RectTransform>();
                actionRoot.SetParent(transform, false);
                actionRoot.anchorMin = new Vector2(0f, 0f);
                actionRoot.anchorMax = new Vector2(1f, 0f);
                actionRoot.pivot = new Vector2(0.5f, 0.5f);
                actionRoot.anchoredPosition = new Vector2(0f, 8f);
                actionRoot.sizeDelta = new Vector2(-44f, 18f);
            }

            if (installButton == null)
            {
                installButton = CreateButton(actionRoot, "InstallButton", "安装", true);
            }

            if (uninstallButton == null)
            {
                uninstallButton = CreateButton(actionRoot, "UninstallButton", "卸下", false);
            }
        }

        private Button CreateButton(RectTransform parent, string objectName, string label, bool left)
        {
            Transform found = parent.Find(objectName);
            if (found != null && found.TryGetComponent(out Button existingButton))
            {
                SetButtonLabel(existingButton, label);
                return existingButton;
            }

            GameObject buttonObject = new(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.layer = gameObject.layer;

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(left ? 0f : 0.5f, 0f);
            rect.anchorMax = new Vector2(left ? 0.5f : 1f, 1f);
            rect.offsetMin = new Vector2(left ? 0f : 4f, 0f);
            rect.offsetMax = new Vector2(left ? -4f : 0f, 0f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = left
                ? new Color(0.18f, 0.55f, 0.27f, 0.95f)
                : new Color(0.65f, 0.22f, 0.20f, 0.95f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            GameObject labelObject = new(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            labelObject.layer = gameObject.layer;

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Text text = labelObject.GetComponent<Text>();
            text.text = label;
            text.font = actionButtonFont != null
                ? actionButtonFont
                : ChineseUiFont.LegacyFont;
            text.fontSize = 12;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return button;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = label;
                ChineseUiFont.Apply(text);
                return;
            }

            Text legacyText = button.GetComponentInChildren<Text>(true);
            if (legacyText != null)
            {
                legacyText.text = label;
                ChineseUiFont.Apply(legacyText);
            }
        }
    }
}
