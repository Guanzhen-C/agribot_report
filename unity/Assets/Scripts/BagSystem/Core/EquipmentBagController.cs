using System;
using System.Collections.Generic;
using RG.Tweening;
using UnityEngine;
using Agribot.VehicleConfiguration;

namespace AgriculturalMachinery.BagSystem
{
    [DisallowMultipleComponent]
    public sealed class EquipmentBagController : MonoBehaviour
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
        [SerializeField] private EquipmentSceneController sceneController;
        public bool IsSceneAnimating => sceneController != null && sceneController.IsAnimating;

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

            if (installed && FarmEquipmentUi.UnavailableReason(equipment.EquipmentType, FindVisibleCar()?.name) != null)
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

            if (installed && FarmEquipmentUi.IsMountedImplement(equipment.EquipmentType))
                foreach (var other in equipmentCatalog.EquipmentList)
                    if (other != equipment && FarmEquipmentUi.IsMountedImplement(other.EquipmentType) && IsInstalled(other))
                        UninstallEquipment(other);

            if (changed)
            {
                AssistantConversation.EquipmentChanged(equipment.EquipmentType.ToString(), equipment.DisplayName, installed);
                if (installed &&
                    (equipment.EquipmentType == EquipmentType.RTK || equipment.EquipmentType == EquipmentType.Hand))
                    FixCurrentCarCameraForRtk();

                InstallationChanged?.Invoke(equipment, installed);
            }

            return true;
        }

        public bool IsInstalled(EquipmentData equipment)
        {
            return equipment != null && installedItemIds.Contains(equipment.ItemId);
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
                return;

            // The scene controller is a fixed component on BagPanel. It resolves
            // the currently visible car's existing equipment children on demand.
            sceneController = GetComponent<EquipmentSceneController>();
            if (sceneController != null)
                return;

            sceneController = FindFirstObjectByType<EquipmentSceneController>(FindObjectsInactive.Include);
        }

        private static GameObject FindVisibleCar()
        {
            foreach (string carName in new[] { "Car0", "Car1", "Car2", "Car3", "Car4", "Car5" })
            {
                GameObject car = GameObject.Find(carName);
                if (car != null && car.activeInHierarchy)
                    return car;
            }

            return FarmGarageVehicle.Visible;
        }

        private static void FixCurrentCarCameraForRtk()
        {
            GameObject currentCar = FindVisibleCar();
            if (currentCar == null)
                return;

            cameraControl currentCamera =
                currentCar.GetComponentInChildren<cameraControl>(true);
            currentCamera?.FixCamera(currentCar.GetComponent<FarmGarageVehicle>() != null ? currentCar.GetComponent<FarmGarageVehicle>().PreviewRadius : 50f, -45f,
                currentCar.GetComponent<FarmGarageVehicle>() != null ? 20f : 6f);
        }

        private void ApplyInstallationStateToScene()
        {
            ResolveSceneController();
            if (sceneController == null || equipmentCatalog == null)
            {
                // The selection screen can open before a car has been selected.
                // Installation resolves the currently visible car again.
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
    /// 统一管理车辆上五种可安装装备的场景物体。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EquipmentSceneController : MonoBehaviour
    {
        public bool IsAnimating => equipmentTweens.Count != 0;
        [Header("场景装备")]
        [SerializeField] private GameObject radar;
        [SerializeField] private GameObject rtk;
        [SerializeField] private GameObject carCamera;
        [SerializeField] private GameObject imuBox;
        [SerializeField] private GameObject hand;

        private readonly Dictionary<EquipmentType, GameObject> targets = new();
        private readonly Dictionary<GameObject, Vector3> originalScales = new();
        private readonly Dictionary<GameObject, TweenHandle> equipmentTweens = new();
        private readonly HashSet<GameObject> hidingEquipment = new();
        private readonly Dictionary<Material, Color> originalColors = new();

        private void Awake()
        {
            RebuildMap();
        }

        public bool TrySetInstalled(EquipmentType equipmentType, bool installed)
        {
            EnsureMap();

            if (FarmEquipmentUi.UnavailableReason(equipmentType, FindVisibleCarTransform()?.name) != null)
            {
                return !installed;
            }

            if (!targets.TryGetValue(equipmentType, out GameObject target) || target == null)
            {
                Debug.LogError($"EquipmentSceneController 找不到 {equipmentType} 对应的场景物体。", this);
                return false;
            }

            if (target.activeSelf != installed || (installed && hidingEquipment.Contains(target)))
            {
                if (FarmEquipmentUi.IsMountedImplement(equipmentType))
                {
                    // A shared hitch changes atomically; no overlapping hide animation.
                    target.SetActive(installed);
                    FindVisibleCarTransform()?.GetComponent<FarmGarageVehicle>()?.RefreshAssemblyView();
                    return true;
                }
                if (installed)
                {
                    if (equipmentType == EquipmentType.Hand)
                        Agribot.VehicleConfiguration.VehicleManipulatorPreview.Attach(target.transform);
                    if (equipmentType == EquipmentType.RTK)
                        ShowEquipmentWithRtkRotation(target);
                    else if (equipmentType == EquipmentType.Hand)
                        ShowHandWithSquash(target);
                    else if (equipmentType == EquipmentType.RADAR)
                        ShowRadarWithWobble(target);
                    else if (equipmentType == EquipmentType.ImuBox)
                        ShowImuWithShake(target);
                    else
                        ShowEquipment(target);
                }
                else
                    HideEquipment(target);
            }

            return true;
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
            Transform currentCar = FindVisibleCarTransform();
            if (currentCar == null)
            {
                targets.Clear();
                return;
            }

            // Always resolve from the current visible car. Never retain a target
            // from another car, even when that other car is inactive.
            radar = Resolve(currentCar, "RADAR");
            rtk = Resolve(currentCar, "RTK");
            carCamera = Resolve(currentCar, "CarCamera");
            imuBox = Resolve(currentCar, "Imu_box");
            hand = Resolve(currentCar, "Hand");

            targets.Clear();
            Register(EquipmentType.RADAR, radar);
            Register(EquipmentType.RTK, rtk);
            Register(EquipmentType.CarCamera, carCamera);
            Register(EquipmentType.ImuBox, imuBox);
            Register(EquipmentType.Hand, hand);
            Register(EquipmentType.ChiselPlow, Resolve(currentCar, "ChiselPlow"));
            Register(EquipmentType.DiscHarrow, Resolve(currentCar, "DiscHarrow"));
            Register(EquipmentType.GrooveMaker, Resolve(currentCar, "GrooveMaker"));
            Register(EquipmentType.Cultivator, Resolve(currentCar, "Cultivator"));
            Register(EquipmentType.ChainCrusher, Resolve(currentCar, "ChainCrusher"));
            Register(EquipmentType.InterrowMill, Resolve(currentCar, "InterrowMill"));
            Register(EquipmentType.FrontCutter, Resolve(currentCar, "FrontCutter"));
            Register(EquipmentType.GarlicPlanter, Resolve(currentCar, "GarlicPlanter"));
            Register(EquipmentType.MulchPlanter, Resolve(currentCar, "MulchPlanter"));
            Register(EquipmentType.FarmTrailer, Resolve(currentCar, "FarmTrailer"));
            Register(EquipmentType.CombineHeader, Resolve(currentCar, "CombineHeader"));
        }

        private void EnsureMap()
        {
            // A different car can become visible while the bag remains in use.
            // Rebuilding is intentional so equipment operations never target an
            // inactive car's child object.
            RebuildMap();
        }

        private static GameObject Resolve(Transform root, string childName)
        {
            Transform child = FindChild(root, childName);
            return child != null ? child.gameObject : null;
        }

        private static Transform FindVisibleCarTransform()
        {
            foreach (string carName in new[] { "Car0", "Car1", "Car2", "Car3", "Car4", "Car5" })
            {
                GameObject car = GameObject.Find(carName);
                if (car != null && car.activeInHierarchy)
                    return car.transform;
            }

            return FarmGarageVehicle.Visible?.transform;
        }

        private void ShowEquipment(GameObject target)
        {
            CacheVisualState(target);
            KillEquipmentTween(target);
            RestoreVisualState(target);
            target.SetActive(true);
            Vector3 scale = originalScales[target];
            target.transform.localScale = Vector3.zero;
            equipmentTweens[target] = RTween.Scale(target.transform, scale, 0.45f)
                .SetEase(EaseType.OutCirc)
                .OnComplete(() => equipmentTweens.Remove(target));
        }

        private void ShowEquipmentWithRtkRotation(GameObject target)
        {
            CacheVisualState(target);
            KillEquipmentTween(target);
            RestoreVisualState(target);
            target.SetActive(true);

            Transform rtkTransform = target.transform;
            Quaternion initialRotation = rtkTransform.localRotation;
            RTween.Kill(rtkTransform);
            equipmentTweens[target] = RTween.Value(0f, 360f, 0.8f, angle =>
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
                        rtkTransform.localRotation = initialRotation;
                    equipmentTweens.Remove(target);
                });
        }

        private void ShowHandWithSquash(GameObject target)
        {
            CacheVisualState(target);
            KillEquipmentTween(target);
            target.SetActive(true);

            Transform handTransform = target.transform;
            Vector3 originalScale = originalScales[target];
            handTransform.localScale = new Vector3(
                originalScale.x * 1.3f,
                originalScale.y * 0.7f,
                originalScale.z);
            equipmentTweens[target] = RTween.Scale(handTransform, originalScale, 0.45f)
                .SetEase(EaseType.OutElastic)
                .OnComplete(() => equipmentTweens.Remove(target));
        }

        private void ShowRadarWithWobble(GameObject target)
        {
            CacheVisualState(target);
            KillEquipmentTween(target);
            target.SetActive(true);

            Transform radarTransform = target.transform;
            Quaternion mountRotation = radarTransform.localRotation;
            equipmentTweens[target] = RTweenEffects
                .WobbleEffect(radarTransform, 15f, 0.5f)
                .OnComplete(() =>
                {
                    if (radarTransform != null) radarTransform.localRotation = mountRotation;
                    equipmentTweens.Remove(target);
                });
        }

        private void ShowImuWithShake(GameObject target)
        {
            CacheVisualState(target);
            KillEquipmentTween(target);
            target.SetActive(true);

            Transform imuTransform = target.transform;
            Vector3 mountPosition = imuTransform.localPosition;
            equipmentTweens[target] = RTweenEffects
                .ShakeLocalPosition(imuTransform, 0.5f, 0.5f, 20, 90f, true)
                .OnComplete(() =>
                {
                    if (imuTransform != null) imuTransform.localPosition = mountPosition;
                    equipmentTweens.Remove(target);
                });
        }

        private void HideEquipment(GameObject target)
        {
            CacheVisualState(target);
            KillEquipmentTween(target);

            List<Material> materials = new();
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.materials)
                {
                    if (material != null && !materials.Contains(material))
                        materials.Add(material);
                }
            }

            if (materials.Count == 0)
            {
                target.SetActive(false);
                return;
            }

            hidingEquipment.Add(target);
            TweenHandle fade = RTween.Value(1f, 0f, 0.35f, alpha =>
            {
                foreach (Material material in materials)
                {
                    if (material == null)
                        continue;
                    Color color = material.color;
                    color.a = alpha;
                    material.color = color;
                }
            }).OnComplete(() =>
            {
                target.SetActive(false);
                RestoreVisualState(target);
                equipmentTweens.Remove(target);
                hidingEquipment.Remove(target);
            });

            equipmentTweens[target] = fade;
        }

        private void CacheVisualState(GameObject target)
        {
            if (!originalScales.ContainsKey(target))
                originalScales[target] = target.transform.localScale;

            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.materials)
                {
                    if (material != null && !originalColors.ContainsKey(material))
                        originalColors[material] = material.color;
                }
            }
        }

        private void RestoreVisualState(GameObject target)
        {
            if (originalScales.TryGetValue(target, out Vector3 scale))
                target.transform.localScale = scale;

            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.materials)
                {
                    if (material != null && originalColors.TryGetValue(material, out Color color))
                        material.color = color;
                }
            }
        }

        private void KillEquipmentTween(GameObject target)
        {
            if (equipmentTweens.TryGetValue(target, out TweenHandle tween))
                tween.Kill();
            equipmentTweens.Remove(target);
            hidingEquipment.Remove(target);
            if (target != null) RTween.Kill(target.transform);
        }

        private void OnDestroy()
        {
            foreach (GameObject target in new List<GameObject>(equipmentTweens.Keys))
                KillEquipmentTween(target);
        }

        private static Transform FindChild(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
                return null;
            if (root.name == childName)
                return root;

            foreach (Transform child in root)
            {
                Transform found = FindChild(child, childName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private void Register(EquipmentType type, GameObject target)
        {
            targets[type] = target;
            if (target != null)
            {
                // Equipment assets are visual models, not garage lights or render cameras.
                foreach (var camera in target.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (var light in target.GetComponentsInChildren<Light>(true)) light.enabled = false;
                foreach (var listener in target.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            }
            if (target == null && type != EquipmentType.Hand && !FarmEquipmentUi.IsMountedImplement(type))
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
    internal sealed class EquipmentBagSaveData
    {
        public int version = 2;
        public bool inventoryInitialized;
        public List<string> ownedItemIds = new();
        public List<string> installedItemIds = new();
    }
}
