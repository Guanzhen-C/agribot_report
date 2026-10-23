using System.Collections.Generic;
using System.Linq;
using Agribot.VehicleConfiguration;
using UnityEngine;
using UnityEngine.UI;

namespace AgriculturalMachinery.BagSystem
{
    // Extend the existing bag, including its selection/details/install buttons.
    public sealed class FarmEquipmentUi : MonoBehaviour
    {
        private readonly List<Object> owned = new();

        public EquipmentSlotUI[] Extend(ref EquipmentCatalog catalog, EquipmentSlotUI template)
        {
            if (template == null || catalog == null || !FarmVehicleLibrary.IsInstalled("compact_tractor"))
                return System.Array.Empty<EquipmentSlotUI>();
            catalog = Instantiate(catalog);
            owned.Add(catalog);
            var slots = new List<EquipmentSlotUI>();
            foreach (var item in new[] {
                (EquipmentType.ChiselPlow, "compact_chisel_plow", "凿式犁"),
                (EquipmentType.DiscHarrow, "mounted_disc_harrow", "圆盘耙"),
                (EquipmentType.GrooveMaker, "groove_maker", "六行开沟机"),
                (EquipmentType.Cultivator, "mounted_cultivator", "悬挂式中耕机"),
                (EquipmentType.ChainCrusher, "forest_chain_crusher", "悬挂式林地粉碎机"),
                (EquipmentType.InterrowMill, "inter_row_milling_cutter", "四行铣土机"),
                (EquipmentType.FrontCutter, "front_sickle_bar", "前置割草刀杆"),
                (EquipmentType.GarlicPlanter, "garlic_planter", "大蒜播种机"),
                (EquipmentType.MulchPlanter, "mulch_planter", "灌水覆膜播种机"),
                (EquipmentType.FarmTrailer, "farm_trailer", "双轴农用拖车"),
                (EquipmentType.CombineHeader, "combine_header", "谷物收割割台") })
            {
                var texture = GarageThumbnailLibrary.LoadTexture(item.Item2);
                owned.Add(texture);
                Sprite sprite = texture == null ? null : Sprite.Create(texture,
                    new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
                if (sprite != null) owned.Add(sprite);
                var data = EquipmentData.CreateImplement(item.Item2, item.Item3, item.Item1, sprite);
                owned.Add(data);
                catalog.AddRuntimeEquipment(data);
                var slot = Instantiate(template, template.transform.parent);
                slot.name = item.Item1 + "_slot";
                slot.ConfigureSlot(data.TargetSlot);
                slots.Add(slot);
                GetComponent<EquipmentSelectionController>()?.AddSlot(slot);
                GetComponent<EquipmentSelectionController_copy>()?.AddSlot(slot);
            }
            var grid = template.transform.parent.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 3;
                var fitter = grid.GetComponent<ContentSizeFitter>() ?? grid.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                LayoutRebuilder.MarkLayoutForRebuild(grid.transform as RectTransform);
            }
            var scroll = template.GetComponentInParent<ScrollRect>(true);
            if (scroll != null)
            {
                scroll.vertical = true;
                scroll.horizontal = false;
                var scrollbar = scroll.verticalScrollbar;
                if (scrollbar != null)
                {
                    // The original two-row panel hid these graphics; extra implements need a visible scroll affordance.
                    scrollbar.gameObject.SetActive(true);
                    scrollbar.interactable = true;
                    scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                    foreach (var graphic in scrollbar.GetComponentsInChildren<Graphic>(true))
                        graphic.enabled = true;
                    var rect = (RectTransform)scrollbar.transform;
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 14f);
                    // Keep the bar inside the authored panel's clipping area.
                    rect.anchoredPosition = new Vector2(-2f, rect.anchoredPosition.y);
                    scroll.viewport.offsetMax = new Vector2(-22f, scroll.viewport.offsetMax.y);
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)scroll.transform);
                    if (grid != null)
                    {
                        float width = (scroll.viewport.rect.width - grid.padding.horizontal - 2f * grid.spacing.x) / 3f;
                        grid.cellSize = new Vector2(Mathf.Min(grid.cellSize.x, width), grid.cellSize.y);
                    }
                }
            }
            return slots.ToArray();
        }

        public static bool IsMountedImplement(EquipmentType type) =>
            type == EquipmentType.ChiselPlow || type == EquipmentType.DiscHarrow ||
            type == EquipmentType.GrooveMaker || type == EquipmentType.Cultivator ||
            type == EquipmentType.ChainCrusher || type == EquipmentType.InterrowMill ||
            type == EquipmentType.FrontCutter || type == EquipmentType.GarlicPlanter ||
            type == EquipmentType.MulchPlanter || type == EquipmentType.FarmTrailer ||
            type == EquipmentType.CombineHeader;

        public static string UnavailableReason(EquipmentType type, string car)
        {
            if (type == EquipmentType.Hand && car != "Car0" && car != "Car3")
                return "机械臂适配阿克曼车和履带车";
            if (IsMountedImplement(type))
            {
                var host = FarmVehicleLibrary.Presets.FirstOrDefault(p => p.garageName == car);
                var mounting = host == null ? null : FarmVehicleLibrary.Presets.FirstOrDefault(p =>
                    p.baseVehicleId == host.id && p.implementObjectName == type.ToString());
                if (mounting == null) return "该车型尚无此机具的已检查挂载配置";
                if (!FarmVehicleLibrary.HasAssetFiles(mounting.id)) return "该机具的本地配置资源未安装完整";
            }
            return null;
        }

        public static string CurrentCarName()
        {
            foreach (var name in new[] { "Car0", "Car1", "Car2", "Car3", "Car4", "Car5" })
                if (GameObject.Find(name) != null) return name;
            return FarmGarageVehicle.Visible?.name;
        }

        private void OnDestroy()
        {
            foreach (var item in owned) if (item != null) Destroy(item);
        }
    }
}
