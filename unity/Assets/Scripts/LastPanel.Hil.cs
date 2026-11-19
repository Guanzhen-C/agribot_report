using Agribot.VehicleConfiguration;
using UnityEngine;
using UnityEngine.UI;

public partial class LastPanel
{
    private Button launchHilButton, cvtButton, powershiftButton, setupStopButton;
    private GameObject transmissionControls;
    private Text hilStatus;
    private string hilTransmission = "cvt_auto";

    private HilProfile ReadyHilProfile()
    {
        var profile = HilSimulationLauncher.Match(AssistantConversation.State.car,
            AssistantConversation.State.implement, FarmScenarioCatalog.SelectedId, hilTransmission);
        return profile != null && profile.algorithm == selectedAlgorithm?.Id ? profile : null;
    }

    private void RefreshLaunchAvailability()
    {
        var status = HilSimulationLauncher.ReadStatus();
        var scene = FarmScenarioCatalog.Selected;
        bool configured = CanLaunchSetup(scene, status);
        SetLaunchAvailability(launchSelectedAlgorithmButton, configured && CanLaunchOrdinary(scene));
        SetLaunchAvailability(launchHilButton, configured && ReadyHilProfile() != null);
        bool stopping = RosSimulationHandoffUi.IsStopPending || HilSimulationUi.IsStopPending;
        if (setupStopButton != null)
        {
            setupStopButton.interactable = !stopping;
            setupStopButton.GetComponentInChildren<Text>().text = stopping ? "正在停止..." : "停止仿真";
        }
        if (transmissionControls != null)
            transmissionControls.SetActive(AssistantConversation.State.car == "Car4");
        if (cvtButton != null)
        {
            cvtButton.GetComponent<Image>().color = hilTransmission == "cvt_auto" ? GarageVisualStyle.Accent : GarageVisualStyle.Raised;
            cvtButton.GetComponentInChildren<Text>().color = hilTransmission == "cvt_auto" ? Color.black : Color.white;
        }
        if (powershiftButton != null)
        {
            powershiftButton.GetComponent<Image>().color = hilTransmission == "powershift_auto" ? GarageVisualStyle.Accent : GarageVisualStyle.Raised;
            powershiftButton.GetComponentInChildren<Text>().color = hilTransmission == "powershift_auto" ? Color.black : Color.white;
        }
        if (hilStatus != null)
        {
            string summary = (status.message ?? "").Split('\n')[0];
            var recording = HilRecordingLauncher.ReadStatus(status.run_id);
            if (!string.IsNullOrEmpty(recording.message)) summary += " · 记录：" + recording.message;
            hilStatus.text = summary.Length > 120 ? summary.Substring(0, 117) + "..." : summary;
        }
    }

    private static void SetLaunchAvailability(Button button, bool available)
    {
        if (button == null) return;
        button.interactable = available;
        button.image.color = available ? GarageVisualStyle.Accent : GarageVisualStyle.Raised;
        button.GetComponentInChildren<Text>(true).color = available ? GarageVisualStyle.Background : GarageVisualStyle.Muted;
    }

    private void StartSelectedHil()
    {
        if (!CanLaunchSetup(FarmScenarioCatalog.Selected, HilSimulationLauncher.ReadStatus())) return;
        var profile = ReadyHilProfile();
        if (profile == null || !ValidateLaunchEquipment()) return;
        // HIL uses its versioned, verified recipe. Ordinary simulation continues
        // to use the independently editable algorithm components on this page.
        AssistantConversation.RecordUi("HIL方案：" + profile.name + "；算法：" + profile.algorithm + "；路线：" + profile.route);
        HilSimulationUi.StartProfile(profile, selectedAlgorithm.Id, SimulationLaunchRequirements.InstalledSensors(), Notify);
    }
}
