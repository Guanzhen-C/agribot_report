using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Agribot.VehicleConfiguration;
using Unity.Cinemachine;
using UnityEngine;

public sealed class FarmGarageVehicle : MonoBehaviour
{
    [Serializable] private sealed class Implement { public string frameId; public PoseData pose; }
    [Serializable] private sealed class ImplementConfig { public Implement[] implements; }
    private readonly Dictionary<string, Transform> implements = new();
    private GameObject cameraBounds;
    private Vector3? trailerOriginalPosition;
    private Quaternion trailerOriginalRotation;
    private Vector3 trailerOriginalScale;
    private float trailerGroundY;
    private Transform garage;
    private Vector3 garagePosition, garageScale, garageAnchor;
    private Vector3 originalViewTarget, originalEquipmentTarget;
    private float mountedPreviewRadius;
    public FarmVehiclePreset Preset { get; private set; }
    public Transform ViewTarget { get; private set; }
    public Transform EquipmentTarget { get; private set; }
    private float DefaultPreviewRadius => Preset.id == "tracked_harvester" ? (SelectedPreset == "harvester_header" ? 18f : 12f) : SelectedPreset == "cab_trailer" ? 12f :
        Preset.id == "unity_car1" || Preset.id == "unity_car2" ? 9f :
        Preset.id == "unity_car4" || Preset.id == "unity_car5" ? 7f :
        Preset.id == "ride_on_mower" ? 3.8f : Preset.id == "compact_tractor" ? 5.2f : 6.5f;
    public float PreviewRadius => Mathf.Max(DefaultPreviewRadius, mountedPreviewRadius);
    public string SelectedPreset => implements.FirstOrDefault(item => item.Value.gameObject.activeSelf).Key ?? Preset.id;
    public static GameObject Visible => FindObjectsByType<FarmGarageVehicle>(FindObjectsSortMode.None)
        .FirstOrDefault(vehicle => vehicle.gameObject.activeInHierarchy)?.gameObject;

    public VehicleConfigurationExportResult Export() => FarmVehicleLibrary.Export(SelectedPreset);

    private void SetGarageSize(float factor)
    {
        if (garage == null) return;
        // Enlarge the existing bay, never shrink the metric vehicle/implement.
        garage.localScale = garageScale * factor;
        garage.position = garageAnchor + (garagePosition - garageAnchor) * factor;
        var camera = GetComponentInChildren<cameraControl>(true);
        if (camera != null && cameraBounds != null) ConfineCameraToGarage(camera);
    }

    private void OnEnable()
    {
        SetGarageSize(RequiredGarageScale());
    }

    private float RequiredGarageScale()
    {
        float authored = Preset?.id == "tracked_harvester" ? 3f :
            Preset?.id.StartsWith("unity_car", StringComparison.Ordinal) == true ? 1.6f :
            SelectedPreset == "cab_trailer" ? 1.6f : 1f;
        if (mountedPreviewRadius == 0 || garage == null) return authored;
        var walls = garage.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Walls");
        var room = BoundsOf(walls);
        float currentFactor = garage.localScale.x / garageScale.x;
        float originalWidth = Mathf.Min(room.size.x, room.size.z) / currentFactor;
        return Mathf.Max(authored, (2.4f * PreviewRadius + 1f) / originalWidth);
    }

    private void OnDisable()
    {
        SetGarageSize(1f);
    }

    public void RefreshAssemblyView()
    {
        bool rigidMount = SelectedPreset != Preset.id && SelectedPreset != "cab_trailer";
        if (Preset.id == "tracked_harvester" || Preset.id.StartsWith("unity_car", StringComparison.Ordinal) ||
            rigidMount || mountedPreviewRadius > 0)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            ViewTarget.localPosition = transform.InverseTransformPoint(bounds.center);
            EquipmentTarget.localPosition = ViewTarget.localPosition;
            var view = GetComponentInChildren<cameraControl>(true);
            float fov = view.GetComponent<CinemachineCamera>().Lens.FieldOfView;
            mountedPreviewRadius = rigidMount && Preset.id != "tracked_harvester"
                ? bounds.extents.magnitude * 1.1f / Mathf.Sin(fov * .5f * Mathf.Deg2Rad) : 0;
            SetGarageSize(RequiredGarageScale());
            view.ConfigurePreviewRange(3f, Mathf.Max(24f, PreviewRadius * 1.5f), PreviewRadius);
            view.FixCamera(PreviewRadius, -45f, 25f);
            return;
        }
        bool mounted = SelectedPreset == "cab_trailer";
        if (!mounted && !trailerOriginalPosition.HasValue) return;
        var camera = GetComponentInChildren<cameraControl>(true);
        var lens = camera.GetComponent<CinemachineCamera>().Lens;
        if (mounted)
        {
            if (!trailerOriginalPosition.HasValue)
            {
                trailerOriginalPosition = transform.position;
                trailerOriginalRotation = transform.rotation;
                trailerOriginalScale = transform.localScale;
                var rawRenderers = GetComponentsInChildren<Renderer>();
                var rawBounds = rawRenderers[0].bounds;
                foreach (var renderer in rawRenderers.Skip(1)) rawBounds.Encapsulate(renderer.bounds);
                trailerGroundY = rawBounds.min.y;
                originalViewTarget = ViewTarget.localPosition;
                originalEquipmentTarget = EquipmentTarget.localPosition;
            }
            SetGarageSize(1.6f);
            transform.localScale = trailerOriginalScale;
            transform.rotation = Quaternion.AngleAxis(45f, Vector3.up)*trailerOriginalRotation;
            var renderers = GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var walls = GameObject.Find("Garage1").GetComponentsInChildren<Transform>(true).Single(t => t.name == "Walls");
            var room = BoundsOf(walls);
            var shift = new Vector3(room.center.x-bounds.center.x, trailerGroundY-bounds.min.y, room.center.z-bounds.center.z);
            transform.position += shift;
            var target = transform.InverseTransformPoint(bounds.center+shift);
            ViewTarget.localPosition = target;
            EquipmentTarget.localPosition = target;
            camera.ConfigurePreviewRange(3.5f, 18f, PreviewRadius);
            camera.FixCamera(PreviewRadius, -110f, 30f);
            lens.FieldOfView = 90f;
            Debug.Log($"TRAILER_VIEW room={room} assembly={bounds} shift={shift}");
        }
        else
        {
            transform.position = trailerOriginalPosition.Value;
            transform.rotation = trailerOriginalRotation;
            transform.localScale = trailerOriginalScale;
            ViewTarget.localPosition = originalViewTarget;
            EquipmentTarget.localPosition = originalEquipmentTarget;
            trailerOriginalPosition = null;
            SetGarageSize(1f);
            camera.ConfigurePreviewRange(3.5f, 7.5f, 6.5f);
            camera.FixCamera(6.5f, -45f, 20f);
            lens.FieldOfView = 65f;
        }
        camera.GetComponent<CinemachineCamera>().Lens = lens;
    }

    public static GameObject Create(GameObject cameraTemplate, FarmVehiclePreset preset)
    {
        if (!FarmVehicleLibrary.IsInstalled(preset.id)) return null;
        var car = new GameObject(preset.garageName);
        car.SetActive(false);
        var instance = car.AddComponent<FarmGarageVehicle>();
        instance.Preset = preset;
        instance.garage = GameObject.Find("Garage1").transform;
        instance.garagePosition = instance.garage.position;
        instance.garageScale = instance.garage.localScale;
        var garageParts = instance.garage.GetComponentsInChildren<Transform>(true);
        var roomBounds = BoundsOf(garageParts.Single(t => t.name == "Walls"));
        instance.garageAnchor = new Vector3(roomBounds.center.x,
            BoundsOf(garageParts.Single(t => t.name == "Floor")).max.y, roomBounds.center.z);
        var config = FarmVehicleLibrary.Configuration(preset.id);
        var preview = FarmVehicleLibrary.CreatePreview(preset.id);
        preview.name = preset.garageName.ToLowerInvariant();
        preview.transform.SetParent(car.transform, false);
        foreach (var sensor in config.sensors.Where(s => s.id != "rtk_secondary"))
        {
            string name = sensor.id switch { "imu" => "Imu_box", "lidar" => "RADAR",
                "right_camera" => "CarCamera", _ => "RTK" };
            var group = Group(preview.transform, car.transform, name, sensor.frameId, sensor.pose);
            group.gameObject.SetActive(false);
        }
        foreach (var variant in FarmVehicleLibrary.Presets.Where(p => p.baseVehicleId == preset.id &&
                     !string.IsNullOrEmpty(p.implementObjectName) && FarmVehicleLibrary.HasAssetFiles(p.id)))
            instance.implements.Add(variant.id, AddImplement(car.transform, variant.id, variant.implementObjectName));

        var old = cameraTemplate.GetComponentsInChildren<Transform>(true);
        Vector3 rear = (old.First(t => t.name == "RLcenter").position + old.First(t => t.name == "RRcenter").position) / 2;
        Vector3 front = (old.First(t => t.name == "FLcenter").position + old.First(t => t.name == "FRcenter").position) / 2;
        var wheel = old.First(t => t.name == "RLwheel").GetComponentsInChildren<Renderer>(true);
        float ground = wheel.Min(r => r.bounds.min.y);
        car.transform.SetPositionAndRotation(new Vector3(rear.x, ground + (float)config.geometry.wheelRadiusM, rear.z),
            Quaternion.LookRotation(Vector3.ProjectOnPlane(front - rear, Vector3.up).normalized, Vector3.up));
        if (preset.id != "compact_tractor")
        {
            var walls = GameObject.Find("Garage1").GetComponentsInChildren<Transform>(true).Single(t => t.name == "Walls");
            var room = BoundsOf(walls);
            var offset = car.transform.rotation * new Vector3(0, 0,
                (float)(config.geometry.bodyExtents.frontM - config.geometry.bodyExtents.rearM) / 2);
            car.transform.position = new Vector3(room.center.x - offset.x, car.transform.position.y, room.center.z - offset.z);
        }
        string suffix = preset.garageName.Substring(3);
        var target = new GameObject("target" + suffix).transform;
        target.SetParent(car.transform, false);
        target.localPosition = preset.id == "tracked_harvester" ? new Vector3(0, 1.8f,
            (float)(config.geometry.bodyExtents.frontM - config.geometry.bodyExtents.rearM) / 2) :
            preset.id == "ride_on_mower" ? new Vector3(0, .35f, .5f) :
            preset.id == "compact_tractor" ? car.transform.InverseTransformPoint(BoundsOf(preview.transform).center) :
            new Vector3(0, .65f, (float)(config.geometry.bodyExtents.frontM - config.geometry.bodyExtents.rearM) / 2);
        instance.ViewTarget = target;
        var bagTarget = new GameObject("target" + suffix + suffix).transform;
        bagTarget.SetParent(car.transform, false);
        bagTarget.localPosition = target.localPosition;
        instance.EquipmentTarget = bagTarget;
        var originalCamera = cameraTemplate.GetComponentInChildren<cameraControl>(true);
        var camera = Instantiate(originalCamera.gameObject, car.transform).GetComponent<cameraControl>();
        camera.name = "FreeLook Camera_" + preset.garageName;
        if (preset.id == "tracked_harvester") camera.ConfigurePreviewRange(6f, 24f, instance.PreviewRadius);
        else if (preset.id.StartsWith("unity_car", StringComparison.Ordinal)) camera.ConfigurePreviewRange(3f, 14f, instance.PreviewRadius);
        else if (preset.id == "ride_on_mower") camera.ConfigurePreviewRange(2f, 4.2f, instance.PreviewRadius);
        else if (preset.id == "compact_tractor") camera.ConfigurePreviewRange(2.5f, 6f, instance.PreviewRadius);
        else camera.ConfigurePreviewRange(3.5f, 7.5f, instance.PreviewRadius);
        var lens = camera.GetComponent<CinemachineCamera>().Lens;
        lens.FieldOfView = preset.id == "tracked_harvester" ? 60f : preset.id == "cab_tractor" ? 65f : 45f;
        camera.GetComponent<CinemachineCamera>().Lens = lens;
        camera.ChangeCameraTarget(target);
        instance.ConfineCameraToGarage(camera);
        return car;
    }

    private void ConfineCameraToGarage(cameraControl camera)
    {
        var garage = GameObject.Find("Garage1");
        var parts = garage.GetComponentsInChildren<Transform>(true);
        Bounds walls = BoundsOf(parts.Single(t => t.name == "Walls"));
        Bounds floor = BoundsOf(parts.Single(t => t.name == "Floor"));
        Bounds ceiling = BoundsOf(parts.Single(t => t.name == "Ceiling"));
        const float clearance = .4f;
        var bounds = new Bounds();
        bounds.SetMinMax(new Vector3(walls.min.x + clearance, floor.max.y + clearance, walls.min.z + clearance),
            new Vector3(walls.max.x - clearance, ceiling.min.y - clearance, walls.max.z - clearance));

        // Keep the boundary independent of the vehicle's selection-scale animation.
        if (cameraBounds == null)
        {
            cameraBounds = new GameObject("FarmPreviewCameraBounds");
            cameraBounds.layer = LayerMask.NameToLayer("Ignore Raycast");
            cameraBounds.AddComponent<BoxCollider>().isTrigger = true;
        }
        cameraBounds.transform.position = bounds.center;
        var volume = cameraBounds.GetComponent<BoxCollider>();
        volume.size = bounds.size;
        volume.isTrigger = true;
        var confiner = camera.GetComponent<CinemachineConfiner3D>() ?? camera.gameObject.AddComponent<CinemachineConfiner3D>();
        confiner.BoundingVolume = volume;
        confiner.SlowingDistance = .3f;
    }

    private static Bounds BoundsOf(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private void OnDestroy()
    {
        if (cameraBounds != null) Destroy(cameraBounds);
    }

    private static Transform AddImplement(Transform car, string preset, string name)
    {
        var config = JsonUtility.FromJson<ImplementConfig>(File.ReadAllText(
            Path.Combine(FarmVehicleLibrary.AssetDirectory(preset), "vehicle_config.json")));
        var implement = config.implements.Single();
        var group = new GameObject(name).transform;
        group.SetParent(car, false);
        RosFluConverter.SetLocalPoseFromRos(group, Vector(implement.pose.xyz), Vector(implement.pose.rpy));
        group.gameObject.SetActive(false);
        group.gameObject.AddComponent<FarmImplementPreview>().Configure(preset, implement.frameId);
        return group;
    }

    private static Transform Group(Transform source, Transform parent, string name, string frame, PoseData pose, bool includeChildren = false)
    {
        var group = new GameObject(name).transform;
        group.SetParent(parent, false);
        RosFluConverter.SetLocalPoseFromRos(group, Vector(pose.xyz), Vector(pose.rpy));
        // AFM vertices already use base_link coordinates. Preserve geometry while
        // placing the animation/install pivot at the actual fixed mount frame.
        foreach (var part in source.Cast<Transform>().Where(t => t.name == frame ||
                     includeChildren && t.name.StartsWith(frame + "_", StringComparison.Ordinal)).ToArray())
            part.SetParent(group, true);
        if (group.childCount == 0) throw new InvalidDataException("缺少安装模型：" + frame);
        return group;
    }

    private static Vector3 Vector(double[] xyz) => new Vector3((float)xyz[0], (float)xyz[1], (float)xyz[2]);
}

// Keep unloaded mounting placeholders in the existing equipment hierarchy.
// Expanded tractor/tool libraries must not instantiate every mesh at startup.
public sealed class FarmImplementPreview : MonoBehaviour
{
    private string preset, frame;
    public void Configure(string presetId, string frameId) { preset = presetId; frame = frameId; }

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(preset)) return;
        var preview = FarmVehicleLibrary.CreatePreview(preset, link =>
            link == frame || link.StartsWith(frame + "_", StringComparison.Ordinal));
        preview.transform.SetParent(transform, false);
        // AFM vertices are in base_link. Cancel only the local mount transform,
        // without relying on the vehicle's animated world scale being nonzero.
        var inverse = Quaternion.Inverse(transform.localRotation);
        preview.transform.localPosition = inverse * -transform.localPosition;
        preview.transform.localRotation = inverse;
        if (preview.transform.childCount == 0) throw new InvalidDataException("缺少安装模型：" + frame);
    }

    private void OnDisable()
    {
        foreach (var child in transform.Cast<Transform>().ToArray())
        {
            child.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }
}
