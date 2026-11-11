using System;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    public static class HilImplementLibrary
    {
        public static bool Supports(string id) => id == "hil_sprayer" || id == "hil_shredder";
        public static GameObject Preview(string id)
        {
            if (!Supports(id)) throw new ArgumentException("Unknown HIL implement", nameof(id));
            var manifest = Resources.Load<TextAsset>("HilImplements/" + id + "/manifest");
            if (manifest == null) throw new InvalidOperationException("缺少HIL农具模型：" + id);
            var record = JsonUtility.FromJson<FarmVehicleLibrary.AssetManifest>(manifest.text);
            if (record.schemaVersion != 1 || record.vehicleId != id)
                throw new InvalidOperationException("HIL农具模型目录无效。");
            return FarmVehicleLibrary.LoadPreview(record, "HilImplements/" + id + "/preview");
        }

        public static void EnsureMounts(Transform car)
        {
            if (car == null || car.name != "Car3") return;
            var reference = car.Find("ROS_base_link");
            if (reference == null) throw new InvalidOperationException("履带车缺少ROS基准坐标。");
            foreach (var item in new[] { ("HilSprayer", "hil_sprayer"), ("HilShredder", "hil_shredder") })
            {
                if (reference.Find(item.Item1) != null) continue;
                var mount = new GameObject(item.Item1);
                mount.SetActive(false);
                mount.transform.SetParent(reference, false);
                mount.AddComponent<HilImplementPreview>().Configure(item.Item2);
            }
        }
    }

    public sealed class HilImplementPreview : MonoBehaviour
    {
        private string identifier;
        public void Configure(string id) => identifier = id;
        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(identifier))
                HilImplementLibrary.Preview(identifier).transform.SetParent(transform, false);
        }
        private void OnDisable()
        {
            for (int i = transform.childCount - 1; i >= 0; --i)
            {
                var child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }
    }
}
