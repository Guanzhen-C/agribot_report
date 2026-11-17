using System;
using System.Linq;
using AgriculturalMachinery.BagSystem;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    public static class SimulationLaunchRequirements
    {
        private static readonly EquipmentSlotType[] Slots = {
            EquipmentSlotType.Radar, EquipmentSlotType.RTK, EquipmentSlotType.Camera, EquipmentSlotType.Imu };
        private static readonly string[] Objects = { "RADAR", "RTK", "CarCamera", "Imu_box" };
        private static readonly string[] Labels = { "雷达", "RTK", "相机", "IMU" };

        public static bool Validate(out string message)
        {
            string name = FarmEquipmentUi.CurrentCarName();
            var car = string.IsNullOrEmpty(name) ? null : GameObject.Find(name);
            if (car == null) { message = "请先选择车辆。"; return false; }
            var bag = UnityEngine.Object.FindObjectsByType<EquipmentBagController>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).FirstOrDefault();
            var legacy = bag == null ? UnityEngine.Object.FindObjectsByType<EquipmentBagController_copy>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault() : null;
            var parts = car.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < Slots.Length; index++)
            {
                bool installed = bag != null ? bag.IsInstalled(bag.GetEquipment(Slots[index])) :
                    legacy != null && legacy.IsInstalled(legacy.GetEquipment(Slots[index]));
                var visual = parts.FirstOrDefault(part => part.name.Trim() == Objects[index]);
                if (!installed || visual == null || !visual.gameObject.activeInHierarchy)
                { message = "请先安装" + Labels[index] + "，再启动仿真。"; return false; }
            }
            if (bag != null && bag.IsSceneAnimating)
            { message = "请等待传感器安装动画结束。"; return false; }
            message = string.Empty;
            return true;
        }

        public static string[] InstalledSensors()
        {
            if (!Validate(out string message)) throw new InvalidOperationException(message);
            return Slots.Select(slot => slot.ToString()).ToArray();
        }
    }
}
