using UnityEngine;

namespace AgriculturalMachinery.BagSystem
{
    [DisallowMultipleComponent]
    public sealed class EquipmentSelectionController_copy :
        MonoBehaviour
    {
        [SerializeField]
        private EquipmentSlotUI[] slots;

        [SerializeField]
        private EquipmentDetailsUI_copy detailsUI;

        private EquipmentSlotUI selectedSlot;

        public void AddSlot(EquipmentSlotUI slot)
        {
            var list = new System.Collections.Generic.List<EquipmentSlotUI>(slots ?? System.Array.Empty<EquipmentSlotUI>());
            if (list.Contains(slot)) return;
            list.Add(slot);
            slots = list.ToArray();
            if (isActiveAndEnabled)
            {
                slot.SlotClicked += HandleSlotClicked;
                slot.EquipmentChanged += HandleEquipmentChanged;
            }
        }

        public EquipmentSlotUI SelectedSlot =>
            selectedSlot;

        public EquipmentData SelectedEquipment =>
            selectedSlot != null
                ? selectedSlot.CurrentEquipment
                : null;

        private void Awake()
        {
            ResolveDetailsUI();

            if (detailsUI != null)
            {
                detailsUI.Clear();
            }
        }

        private void OnEnable()
        {
            ResolveDetailsUI();

            if (slots == null)
            {
                return;
            }

            foreach (EquipmentSlotUI slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }

                slot.SlotClicked -= HandleSlotClicked;
                slot.EquipmentChanged -= HandleEquipmentChanged;
                slot.SlotClicked += HandleSlotClicked;
                slot.EquipmentChanged +=
                    HandleEquipmentChanged;
            }
        }

        private void OnDisable()
        {
            if (slots == null)
            {
                return;
            }

            foreach (EquipmentSlotUI slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }

                slot.SlotClicked -= HandleSlotClicked;
                slot.EquipmentChanged -=
                    HandleEquipmentChanged;
            }
        }

        private void ResolveDetailsUI()
        {
            if (detailsUI == null)
            {
                detailsUI = GetComponentInChildren<EquipmentDetailsUI_copy>(true);
            }
        }

        private void HandleSlotClicked(
            EquipmentSlotUI slot)
        {
            SelectSlot(slot);
        }

        private void HandleEquipmentChanged(
            EquipmentSlotUI slot,
            EquipmentData equipment)
        {
            if (slot != selectedSlot)
            {
                return;
            }

            if (equipment == null)
            {
                ClearSelection();
                return;
            }

            if (detailsUI != null)
            {
                detailsUI.Show(equipment);
            }
        }

        public void SelectSlot(EquipmentSlotUI slot)
        {
            if (slot == null || slot.IsEmpty)
            {
                return;
            }

            if (selectedSlot != null)
            {
                selectedSlot.SetSelected(false);
            }

            selectedSlot = slot;
            selectedSlot.SetSelected(true);

            if (detailsUI != null)
            {
                detailsUI.Show(
                    selectedSlot.CurrentEquipment);
            }
        }

        public void ClearSelection()
        {
            if (selectedSlot != null)
            {
                selectedSlot.SetSelected(false);
            }

            selectedSlot = null;

            if (detailsUI != null)
            {
                detailsUI.Clear();
            }
        }
    }
}
