using System.Collections.Generic;
using UnityEngine;

///创建装备数据库：数据库用于集中管理四件装备，后续背包系统只需要持有一个 EquipmentCatalog，不必分别保存四个字段。
namespace AgriculturalMachinery.BagSystem
{
    [CreateAssetMenu(
        fileName = "EquipmentCatalog",
        menuName = "Bag System/Equipment Catalog")]
    public sealed class EquipmentCatalog : ScriptableObject
    {
        [SerializeField]
        private List<EquipmentData> equipmentList = new();

        public IReadOnlyList<EquipmentData> EquipmentList =>
            equipmentList;

        public void AddRuntimeEquipment(EquipmentData equipment)
        {
            if (!TryGet(equipment.EquipmentType, out _)) equipmentList.Add(equipment);
        }

        public bool TryGet(
            EquipmentType equipmentType,
            out EquipmentData result)
        {
            foreach (EquipmentData equipment in equipmentList)
            {
                if (equipment == null)
                {
                    continue;
                }

                if (equipment.EquipmentType == equipmentType)
                {
                    result = equipment;
                    return true;
                }
            }

            result = null;
            return false;
        }

        public bool TryGet(string itemId, out EquipmentData result)
        {
            foreach (EquipmentData equipment in equipmentList)
            {
                if (equipment != null && equipment.ItemId == itemId)
                {
                    result = equipment;
                    return true;
                }
            }

            result = null;
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var typeSet = new HashSet<EquipmentType>();
            var idSet = new HashSet<string>();

            foreach (EquipmentData equipment in equipmentList)
            {
                if (equipment == null)
                {
                    continue;
                }

                if (!typeSet.Add(equipment.EquipmentType))
                {
                    Debug.LogError(
                        $"EquipmentCatalog 中存在重复类型：" +
                        $"{equipment.EquipmentType}",
                        this);
                }

                if (string.IsNullOrWhiteSpace(equipment.ItemId))
                {
                    continue;
                }

                if (!idSet.Add(equipment.ItemId))
                {
                    Debug.LogError(
                        $"EquipmentCatalog 中存在重复 ID：" +
                        $"{equipment.ItemId}",
                        this);
                }
            }
        }
#endif
    }
}
