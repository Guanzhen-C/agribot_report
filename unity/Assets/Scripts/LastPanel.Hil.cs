using Agribot.VehicleConfiguration;
using UnityEngine;
using UnityEngine.UI;

public partial class LastPanel
{
    private Button launchHilButton, cvtButton, powershiftButton;
    private GameObject transmissionControls;
    private Text hilStatus;
    private string hilTransmission = "cvt_auto";

    private HilProfile SelectedHilProfile() => HilSimulationLauncher.Match(AssistantConversation.State.car,
        AssistantConversation.State.implement, FarmScenarioCatalog.SelectedId, hilTransmission);

    private void RefreshHilAvailability()
    {
        var profile = SelectedHilProfile();
        var status = HilSimulationLauncher.ReadStatus();
        if (launchHilButton != null)
            launchHilButton.interactable = profile != null && !status.Active && !HilSimulationUi.IsBusy &&
                !sceneImportBusy && !sceneDeleteBusy;
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
            hilStatus.text = summary.Length > 100 ? summary.Substring(0, 97) + "..." : summary;
        }
    }

    private void StartSelectedHil()
    {
        var profile = SelectedHilProfile();
        if (profile == null) return;
        if (selectedAlgorithm != null && selectedAlgorithm.Id != profile.algorithm)
        {
            Notify("当前HIL使用已验证的默认方案：" + RosSimulationLauncher.FindAlgorithm(profile.algorithm).DisplayName +
                "。当前算法选择未被修改，请确认方案后再启动。");
            return;
        }
        // HIL uses its versioned, verified recipe. Ordinary simulation continues
        // to use the independently editable algorithm components on this page.
        AssistantConversation.RecordUi("HIL方案：" + profile.name + "；算法：" + profile.algorithm + "；路线：" + profile.route);
        HilSimulationUi.StartProfile(profile, Notify);
    }
}
