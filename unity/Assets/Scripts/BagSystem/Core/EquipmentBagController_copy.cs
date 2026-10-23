using System;
using System.Collections.Generic;
using RG.Tweening;
using UnityEngine;

namespace AgriculturalMachinery.BagSystem
{
    [DisallowMultipleComponent]
    public sealed class EquipmentBagController_copy : MonoBehaviour
    {
        [Header("装备数据库")]
        [SerializeField] private EquipmentCatalog equipmentCatalog;

        [Header("固定装备 Slot")]
        [SerializeField] private EquipmentSlotUI radarSlot;
        [SerializeField] private EquipmentSlotUI rtkSlot;
        [SerializeField] private EquipmentSlotUI cameraSlot;
        [SerializeField] private EquipmentSlotUI imuSlot;
        [SerializeField] private EquipmentSlotUI handSlot;
        private EquipmentSlotUI[] implementSlots = Array.Empty<EquipmentSlotUI>();

        [Header("场景联动")]
        [SerializeField] private EquipmentSceneController_copy sceneController;

        [Header("初始化")]
        [SerializeField] private bool initializeOnStart = true;

        private readonly Dictionary<EquipmentSlotType, EquipmentSlotUI> slotMap = new();
        private readonly HashSet<string> ownedItemIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> installedItemIds = new(StringComparer.Ordinal);
        private bool isInitialized;

        public bool IsInitialized => isInitialized;

        public event Action<EquipmentData> EquipmentAdded;
        public event Action<EquipmentData> EquipmentRemoved;
        public event Action<EquipmentData, bool> InstallationChanged;

        private void Awake()
        {
            implementSlots = gameObject.AddComponent<FarmEquipmentUi>().Extend(ref equipmentCatalog, handSlot);
            BuildSlotMap();
            ResolveSceneController();
        }

        private void OnEnable()
        {
            if (initializeOnStart && !isInitialized)
            {
                InitializeBag();
            }
        }

        public void InitializeBag()
        {
            if (equipmentCatalog == null)
            {
                Debug.LogError("EquipmentBagController 没有绑定 EquipmentCatalog。", this);
                return;
            }

            BuildSlotMap();
            if (slotMap.Count != 5 + implementSlots.Length)
            {
                Debug.LogError("背包 Slot 映射不完整，初始化失败。", this);
                return;
            }

            ResolveSceneController();
            isInitialized = false;
            SetCatalogAsInitialInventory();
            InitializeBagFromCurrentState();
        }

        public bool TryAddEquipment(EquipmentData equipment)
        {
            if (!PlaceInSlot(equipment))
            {
                return false;
            }

            bool newlyOwned = ownedItemIds.Add(equipment.ItemId);
            if (newlyOwned)
            {
                EquipmentAdded?.Invoke(equipment);
            }

            return true;
        }

        public EquipmentData RemoveEquipment(EquipmentSlotType slotType)
        {
            if (!slotMap.TryGetValue(slotType, out EquipmentSlotUI targetSlot))
            {
                return null;
            }

            EquipmentData equipment = targetSlot.CurrentEquipment;
            if (equipment == null)
            {
                return null;
            }

            SetInstallationState(equipment, false);
            EquipmentData removed = targetSlot.Take();
            ownedItemIds.Remove(equipment.ItemId);
            installedItemIds.Remove(equipment.ItemId);
            EquipmentRemoved?.Invoke(removed);
            return removed;
        }

        public bool InstallEquipment(EquipmentData equipment)
        {
            return SetInstallationState(equipment, true);
        }

        public bool UninstallEquipment(EquipmentData equipment)
        {
            return SetInstallationState(equipment, false);
        }

        public void UninstallAllEquipment()
        {
            if (equipmentCatalog == null)
            {
                return;
            }

            foreach (EquipmentData equipment in equipmentCatalog.EquipmentList)
            {
                if (equipment != null && IsInstalled(equipment))
                {
                    UninstallEquipment(equipment);
                }
            }
        }

        public bool SetInstallationState(EquipmentData equipment, bool installed)
        {
            if (equipment == null || !ownedItemIds.Contains(equipment.ItemId))
            {
                return false;
            }

            ResolveSceneController();
            if (sceneController == null ||
                !sceneController.TrySetInstalled(equipment.EquipmentType, installed))
            {
                return false;
            }

            bool changed = installed
                ? installedItemIds.Add(equipment.ItemId)
                : installedItemIds.Remove(equipment.ItemId);

            if (changed)
            {
                if (installed &&
                    (equipment.EquipmentType == EquipmentType.RTK || equipment.EquipmentType == EquipmentType.Hand))
                    FindFirstObjectByType<OrbitCameraController>().LockToFixedView(-90, 28, 30);
                InstallationChanged?.Invoke(equipment, installed);
            }

            return true;
        }

        public bool IsInstalled(EquipmentData equipment)
        {
            if (equipment == null)
            {
                return false;
            }

            ResolveSceneController();
            return sceneController != null
                ? sceneController.IsInstalled(equipment.EquipmentType)
                : installedItemIds.Contains(equipment.ItemId);
        }

        public bool IsOwned(EquipmentData equipment)
        {
            return equipment != null && ownedItemIds.Contains(equipment.ItemId);
        }

        public EquipmentSlotUI GetSlot(EquipmentSlotType slotType)
        {
            slotMap.TryGetValue(slotType, out EquipmentSlotUI slot);
            return slot;
        }

        public EquipmentData GetEquipment(EquipmentSlotType slotType)
        {
            EquipmentSlotUI slot = GetSlot(slotType);
            return slot != null ? slot.CurrentEquipment : null;
        }

        public void ClearAllSlots()
        {
            ClearSlotVisuals();
            ownedItemIds.Clear();
            installedItemIds.Clear();
            ApplyInstallationStateToScene();
            isInitialized = false;
        }

        public void ResetToCatalogDefaults()
        {
            SetCatalogAsInitialInventory();
            InitializeBagFromCurrentState();
        }

        private void InitializeBagFromCurrentState()
        {
            ClearSlotVisuals();
            foreach (EquipmentData equipment in equipmentCatalog.EquipmentList)
            {
                if (equipment != null && ownedItemIds.Contains(equipment.ItemId))
                {
                    PlaceInSlot(equipment);
                }
            }
            ApplyInstallationStateToScene();
            isInitialized = true;
        }

        private void BuildSlotMap()
        {
            slotMap.Clear();
            RegisterSlot(radarSlot);
            RegisterSlot(rtkSlot);
            RegisterSlot(cameraSlot);
            RegisterSlot(imuSlot);
            RegisterSlot(handSlot);
            foreach (var slot in implementSlots) RegisterSlot(slot);
        }

        private void RegisterSlot(EquipmentSlotUI slot)
        {
            if (slot == null)
            {
                Debug.LogError("背包系统存在未绑定的 EquipmentSlotUI。", this);
                return;
            }

            if (!slotMap.TryAdd(slot.SlotType, slot))
            {
                Debug.LogError($"存在重复的 Slot 类型：{slot.SlotType}", slot);
            }
        }

        private bool PlaceInSlot(EquipmentData equipment)
        {
            if (equipment == null || string.IsNullOrWhiteSpace(equipment.ItemId))
            {
                return false;
            }

            if (!slotMap.TryGetValue(equipment.TargetSlot, out EquipmentSlotUI targetSlot))
            {
                Debug.LogError($"找不到类型为 {equipment.TargetSlot} 的 Slot。", this);
                return false;
            }

            if (!targetSlot.IsEmpty)
            {
                return targetSlot.CurrentEquipment == equipment;
            }

            return targetSlot.TryPlace(equipment);
        }

        private void ClearSlotVisuals()
        {
            foreach (EquipmentSlotUI slot in slotMap.Values)
            {
                slot.Clear();
            }
        }

        private void ResolveSceneController()
        {
            if (sceneController != null)
            {
                return;
            }

            sceneController = FindFirstObjectByType<EquipmentSceneController_copy>(FindObjectsInactive.Include);
            if (sceneController == null)
            {
                GameObject root = GameObject.Find("EquipmentSceneController");
                if (root != null)
                {
                    sceneController = root.GetComponent<EquipmentSceneController_copy>();
                    if (sceneController == null && Application.isPlaying)
                    {
                        sceneController = root.AddComponent<EquipmentSceneController_copy>();
                    }
                }
            }
        }

        private void ApplyInstallationStateToScene()
        {
            ResolveSceneController();
            if (sceneController == null || equipmentCatalog == null)
            {
                Debug.LogError("没有找到场景中的 EquipmentSceneController 组件。", this);
                return;
            }

            foreach (EquipmentData equipment in equipmentCatalog.EquipmentList)
            {
                if (equipment != null)
                {
                    sceneController.TrySetInstalled(
                        equipment.EquipmentType,
                        installedItemIds.Contains(equipment.ItemId));
                }
            }
        }

        private void SetCatalogAsInitialInventory()
        {
            ownedItemIds.Clear();
            installedItemIds.Clear();

            foreach (EquipmentData equipment in equipmentCatalog.EquipmentList)
            {
                if (equipment != null && !string.IsNullOrWhiteSpace(equipment.ItemId))
                {
                    ownedItemIds.Add(equipment.ItemId);
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSlot(radarSlot, EquipmentSlotType.Radar, nameof(radarSlot));
            ValidateSlot(rtkSlot, EquipmentSlotType.RTK, nameof(rtkSlot));
            ValidateSlot(cameraSlot, EquipmentSlotType.Camera, nameof(cameraSlot));
            ValidateSlot(imuSlot, EquipmentSlotType.Imu, nameof(imuSlot));
            ValidateSlot(handSlot, EquipmentSlotType.Hand, nameof(handSlot));
        }

        private static void ValidateSlot(
            EquipmentSlotUI slot,
            EquipmentSlotType expectedType,
            string fieldName)
        {
            if (slot != null && slot.SlotType != expectedType)
            {
                Debug.LogError(
                    $"{fieldName} 应绑定 {expectedType} 类型，当前绑定的是 {slot.SlotType}。",
                    slot);
            }
        }
#endif
    }

    /// <summary>
    /// 统一管理车辆上四种可安装装备的场景物体。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EquipmentSceneController_copy : MonoBehaviour
    {
        [Header("场景装备")]
        [SerializeField] private GameObject radar;
        [SerializeField] private GameObject rtk;
        [SerializeField] private GameObject carCamera;
        [SerializeField] private GameObject imuBox;
        [SerializeField] private GameObject hand;

        private readonly Dictionary<EquipmentType, GameObject> targets = new();

        private void Awake()
        {
            RebuildMap();
        }

        public bool TrySetInstalled(EquipmentType equipmentType, bool installed)
        {
            if (FarmEquipmentUi.IsMountedImplement(equipmentType)) return !installed;
            EnsureMap();
            if (!targets.TryGetValue(equipmentType, out GameObject target) || target == null)
            {
                Debug.LogError($"EquipmentSceneController 找不到 {equipmentType} 对应的场景物体。", this);
                return false;
            }

            if (target.activeSelf != installed)
            {
                target.SetActive(installed);
            }
            if (installed && equipmentType == EquipmentType.Hand)
                Agribot.VehicleConfiguration.VehicleManipulatorPreview.Attach(target.transform);

            if (installed && equipmentType == EquipmentType.RTK)
            {
                Transform rtkTransform = target.transform;
                Quaternion initialRotation = rtkTransform.localRotation;
                RTween.Kill(rtkTransform);
                RTween.Value(0f, 360f, 0.8f, angle =>
                {
                    if (rtkTransform != null)
                    {
                        rtkTransform.localRotation =
                            initialRotation * Quaternion.Euler(0f, angle, 0f);
                    }
                })
                    .SetEase(EaseType.InOutQuad)
                    .OnComplete(() =>
                    {
                        if (rtkTransform != null)
                        {
                            rtkTransform.localRotation = initialRotation;
                        }
                    });
            }
            else if (installed && equipmentType == EquipmentType.RADAR)
            {
                Transform radarTransform = target.transform;
                RTween.Kill(radarTransform);
                Quaternion mountRotation = radarTransform.localRotation;
                RTweenEffects.WobbleEffect(radarTransform, 15f, 0.5f)
                    .OnComplete(() =>
                    {
                        if (radarTransform != null) radarTransform.localRotation = mountRotation;
                    });
            }
            else if (installed && equipmentType == EquipmentType.CarCamera)
            {
                Transform cameraTransform = target.transform;
                RTween.Kill(cameraTransform);
                Vector3 originalScale = cameraTransform.localScale;
                cameraTransform.localScale = Vector3.zero;
                RTween.Scale(cameraTransform, originalScale * 1.2f, 0.3f)
                    .SetEase(EaseType.OutBack)
                    .OnComplete(() =>
                    {
                        if (cameraTransform != null)
                        {
                            RTween.Scale(cameraTransform, originalScale, 0.2f)
                                .SetEase(EaseType.OutQuad);
                        }
                    });
            }
            else if (installed && equipmentType == EquipmentType.ImuBox)
            {
                Transform imuTransform = target.transform;
                RTween.Kill(imuTransform);
                RTweenEffects.ShakeLocalPosition(
                    imuTransform,
                    0.5f,
                    0.5f,
                    20,
                    90f,
                    true);
            }
            else if (installed && equipmentType == EquipmentType.Hand)
            {
                ShowHandWithSquash(target);
            }

            return true;
        }

        private void ShowHandWithSquash(GameObject target)
        {
            Transform handTransform = target.transform;
            RTween.Kill(handTransform);

            Vector3 originalScale = handTransform.localScale;
            Vector3 squashScale = new Vector3(
                originalScale.x * 1.3f,
                originalScale.y * 0.7f,
                originalScale.z);
            RTween.Scale(handTransform, squashScale, 0.2f)
                .SetEase(EaseType.OutQuad)
                .OnComplete(() =>
                {
                    if (handTransform != null)
                    {
                        RTween.Scale(handTransform, originalScale, 0.3f)
                            .SetEase(EaseType.OutElastic);
                    }
                });
        }

        public bool IsInstalled(EquipmentType equipmentType)
        {
            EnsureMap();
            return targets.TryGetValue(equipmentType, out GameObject target) &&
                   target != null && target.activeSelf;
        }

        public bool HasTarget(EquipmentType equipmentType)
        {
            EnsureMap();
            return targets.TryGetValue(equipmentType, out GameObject target) && target != null;
        }

        public void RebuildMap()
        {
            radar = Resolve(radar, "RADAR");
            rtk = Resolve(rtk, "RTK");
            carCamera = Resolve(carCamera, "CarCamera");
            imuBox = Resolve(imuBox, "Imu_box");
            hand = Resolve(hand, "Hand");

            targets.Clear();
            Register(EquipmentType.RADAR, radar);
            Register(EquipmentType.RTK, rtk);
            Register(EquipmentType.CarCamera, carCamera);
            Register(EquipmentType.ImuBox, imuBox);
            Register(EquipmentType.Hand, hand);
        }

        private void EnsureMap()
        {
            if (targets.Count != 5)
            {
                RebuildMap();
            }
        }

        private GameObject Resolve(GameObject current, string childName)
        {
            if (current != null)
            {
                return current;
            }

            Transform child = transform.Find(childName);
            return child != null ? child.gameObject : null;
        }

        private void Register(EquipmentType type, GameObject target)
        {
            targets[type] = target;
            if (target == null)
            {
                Debug.LogError($"请在 {name} 下绑定或创建场景装备：{type}。", this);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildMap();
        }
#endif
    }

    [Serializable]
    internal sealed class EquipmentBagSaveData_copy
    {
        public int version = 2;
        public bool inventoryInitialized;
        public List<string> ownedItemIds = new();
        public List<string> installedItemIds = new();
    }
}
