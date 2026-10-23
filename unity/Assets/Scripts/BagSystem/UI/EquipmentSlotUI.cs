using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace AgriculturalMachinery.BagSystem
{
    [DisallowMultipleComponent]
    public sealed class EquipmentSlotUI : MonoBehaviour, IPointerClickHandler
    {
        [Header("选中状态")]
        [SerializeField]
        private GameObject selectedState;
        public bool IsSelected { get; private set; }

        public event Action<EquipmentSlotUI> SlotClicked;

        // 添加点击方法：
        public void OnPointerClick(
     PointerEventData eventData)
        {
            if (eventData.button !=
                PointerEventData.InputButton.Left)
            {
                return;
            }

            if (currentEquipment == null)
            {
                return;
            }

            SlotClicked?.Invoke(this);
        }

        //添加选中状态控制方法：

        public void SetSelected(bool selected)
        {
            IsSelected = selected;

            if (selectedState != null)
            {
                selectedState.SetActive(selected);
            }
        }
       

        [Header("Slot 配置")]
        [SerializeField]
        private EquipmentSlotType slotType;

        [Header("UI 引用")]
        [SerializeField]
        private Image equipmentIcon;

        [Tooltip("Slot 为空时显示的对象，可以不设置")]
        [SerializeField]
        private GameObject emptyState;

        [Tooltip("Slot 中有装备时显示的对象，可以不设置")]
        [SerializeField]
        private GameObject occupiedState;

        private EquipmentData currentEquipment;

        public EquipmentSlotType SlotType => slotType;

        public void ConfigureSlot(EquipmentSlotType type)
        {
            slotType = type;
            Clear();
            SetSelected(false);
        }

        public EquipmentData CurrentEquipment =>
            currentEquipment;

        public bool IsEmpty =>
            currentEquipment == null;

        /// <summary>
        /// Slot 内容发生变化时触发。
        /// </summary>
        public event Action<EquipmentSlotUI, EquipmentData>
            EquipmentChanged;

        private void Awake()
        {
            RefreshVisual();
        }

        /// <summary>
        /// 判断这个 Slot 是否接受指定装备。
        /// </summary>
        public bool CanAccept(EquipmentData equipment)
        {
            if (equipment == null)
            {
                return false;
            }

            return equipment.TargetSlot == slotType;
        }

        /// <summary>
        /// 尝试将装备放进 Slot。
        /// </summary>
        public bool TryPlace(EquipmentData equipment)
        {
            if (!CanAccept(equipment))
            {
                return false;
            }

            currentEquipment = equipment;
            RefreshVisual();

            EquipmentChanged?.Invoke(
                this,
                currentEquipment);

            return true;
        }

        /// <summary>
        /// 取出当前装备，并清空 Slot。
        /// </summary>
        public EquipmentData Take()
        {
            EquipmentData removedEquipment =
                currentEquipment;

            currentEquipment = null;
            RefreshVisual();

            EquipmentChanged?.Invoke(this, null);

            return removedEquipment;
        }

        /// <summary>
        /// 清空 Slot。
        /// </summary>
        public void Clear()
        {
            if (currentEquipment == null)
            {
                return;
            }

            currentEquipment = null;
            RefreshVisual();

            EquipmentChanged?.Invoke(this, null);
        }

        private void RefreshVisual()
        {
            bool hasEquipment =
                currentEquipment != null;

            if (equipmentIcon != null)
            {
                Sprite icon = hasEquipment
                    ? currentEquipment.Icon
                    : null;

                equipmentIcon.sprite = icon;
                equipmentIcon.enabled = icon != null;
            }

            if (emptyState != null)
            {
                emptyState.SetActive(!hasEquipment);
            }

            if (occupiedState != null)
            {
                occupiedState.SetActive(hasEquipment);
            }

            
  if (!hasEquipment)
  {
      SetSelected(false);
  }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (equipmentIcon == null)
            {
                Debug.LogWarning(
                    $"{name} 尚未绑定 Equipment Icon。",
                    this);
            }
        }


#endif
    }
}
