using Agribot.VehicleConfiguration;
using UnityEngine;

/// 这个脚本只保存装备的静态信息
namespace AgriculturalMachinery.BagSystem
{
    [CreateAssetMenu(
        fileName = "NewEquipment",
        menuName = "Bag System/Equipment Data")]
    public sealed class EquipmentData : ScriptableObject
    {
        [Header("唯一标识")]
        [SerializeField]
        private string itemId;

        [SerializeField]
        private EquipmentType equipmentType;

        [Header("装备信息")]
        [SerializeField]
        private string displayName;

        [SerializeField]
        private Sprite icon;

        [SerializeField]
        [TextArea(2, 5)]
        private string description;

        [Header("装备位置")]
        [SerializeField]
        private EquipmentSlotType targetSlot;

        [System.NonSerialized] private Sprite runtimeIcon;
        [System.NonSerialized] private Texture2D runtimeIconTexture;

        public string ItemId => itemId;
        public EquipmentType EquipmentType => equipmentType;
        public string DisplayName => displayName;
        public Sprite Icon
        {
            get
            {
                if (!Application.isPlaying ||
                    (equipmentType != EquipmentType.RADAR && equipmentType != EquipmentType.RTK &&
                     equipmentType != EquipmentType.CarCamera && equipmentType != EquipmentType.ImuBox))
                    return icon;
                if (runtimeIcon != null) return runtimeIcon;
                // Match the vehicle thumbnails: decode source PNGs, bypassing corrupt imported channels.
                runtimeIconTexture = GarageThumbnailLibrary.LoadTexture("Equipment_" + equipmentType);
                runtimeIcon = Sprite.Create(runtimeIconTexture,
                    new Rect(0, 0, runtimeIconTexture.width, runtimeIconTexture.height),
                    new Vector2(.5f, .5f), icon != null ? icon.pixelsPerUnit : 100f);
                runtimeIcon.name = "Equipment_" + equipmentType;
                return runtimeIcon;
            }
        }
        public string Description => description;
        public EquipmentSlotType TargetSlot => targetSlot;

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                if (runtimeIcon != null) Destroy(runtimeIcon);
                if (runtimeIconTexture != null) Destroy(runtimeIconTexture);
            }
            else
            {
                if (runtimeIcon != null) DestroyImmediate(runtimeIcon);
                if (runtimeIconTexture != null) DestroyImmediate(runtimeIconTexture);
            }
            runtimeIcon = null;
            runtimeIconTexture = null;
        }

        public static EquipmentData CreateImplement(string id, string label, EquipmentType type, Sprite sprite)
        {
            var data = CreateInstance<EquipmentData>();
            data.itemId = id;
            data.displayName = label;
            data.equipmentType = type;
            data.targetSlot = (EquipmentSlotType)(int)type;
            data.icon = sprite;
            return data;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                Debug.LogWarning(
                    $"{name} 尚未设置 Item ID。",
                    this);
            }
        }
#endif
    }
}
