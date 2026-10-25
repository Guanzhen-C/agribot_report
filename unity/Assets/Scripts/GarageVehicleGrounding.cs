using System.Linq;
using UnityEngine;

// Presentation only: move the whole assembly, preserving its local geometry and TF.
[DefaultExecutionOrder(-50)]
public sealed class GarageVehicleGrounding : MonoBehaviour
{
    private Renderer[] bodyRenderers;
    private Renderer[] floorRenderers;
    public bool Suspended { get; set; }

    public static GarageVehicleGrounding Attach(GameObject vehicle)
    {
        var grounding = vehicle.GetComponent<GarageVehicleGrounding>() ??
            vehicle.AddComponent<GarageVehicleGrounding>();
        var garage = GameObject.Find("Garage1");
        var floor = garage.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Floor");
        grounding.floorRenderers = floor.GetComponentsInChildren<Renderer>(true);
        var farm = vehicle.GetComponent<FarmGarageVehicle>();
        var body = farm != null ? vehicle.transform.Find(vehicle.name.ToLowerInvariant()) : vehicle.transform;
        // Capture only the bare vehicle. Later tools, supports and sensor animations
        // must never define its wheel/track contact plane.
        grounding.bodyRenderers = body.GetComponentsInChildren<MeshRenderer>(true)
            .Where(renderer => renderer.enabled && renderer.GetComponentInParent<Canvas>(true) == null &&
                !renderer.GetComponentsInParent<Transform>(true).Any(part =>
                    part.name is "RADAR" or "RTK" or "CarCamera" or "Imu_box" or "Hand"))
            .Cast<Renderer>().ToArray();
        return grounding;
    }

    public void AlignToFloor()
    {
        if (Suspended || bodyRenderers == null || bodyRenderers.Length == 0) return;
        float bottom = float.PositiveInfinity;
        foreach (var renderer in bodyRenderers)
            if (renderer != null && renderer.enabled) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
        float floor = float.NegativeInfinity;
        foreach (var renderer in floorRenderers)
            if (renderer != null) floor = Mathf.Max(floor, renderer.bounds.max.y);
        if (!float.IsFinite(bottom) || !float.IsFinite(floor)) return;
        float correction = floor - bottom;
        if (Mathf.Abs(correction) > .00001f) transform.position += Vector3.up * correction;
    }

    // RTween changes scale in Update; align before Cinemachine's LateUpdate.
    private void LateUpdate() => AlignToFloor();
}
