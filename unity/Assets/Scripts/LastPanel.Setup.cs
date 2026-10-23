using System;
using System.Collections.Generic;
using Agribot.VehicleConfiguration;
using UnityEngine;
using UnityEngine.UI;

public partial class LastPanel
{
    private GameObject scenePage, algorithmPage;
    private Button sceneTab, algorithmTab;
    private Text currentScene;
    private readonly List<UnityEngine.Object> setupImages = new();

    private void BuildModernAlgorithmDialog()
    {
        if (algorithmDialog != null || ExportRos_btn == null) return;
        componentOptions.Clear(); sceneButtons.Clear();
        algorithmDialog = CreateImageObject(transform, "ROS_Algorithm_Selection", new Color(.02f, .03f, .035f, .94f));
        StretchRect(algorithmDialog.GetComponent<RectTransform>());
        var panel = GarageVisualStyle.Panel(algorithmDialog.transform, "SimulationSetup", GarageVisualStyle.Background);
        GarageVisualStyle.Rect(panel.transform, new Vector2(.035f, .045f), new Vector2(.965f, .955f));
        GarageVisualStyle.Label(panel.transform, "SetupBrand", "AGRIBOT  /  仿真实验室", 27,
            new Vector2(.025f, .90f), new Vector2(.67f, .99f));
        var close = SetupButton(panel.transform, "Close", "关闭", .89f, .92f, .975f, .975f);
        close.onClick.AddListener(CloseSetup);
        sceneTab = SetupButton(panel.transform, "SceneTab", "01  作业场景", .025f, .75f, .155f, .83f);
        algorithmTab = SetupButton(panel.transform, "AlgorithmTab", "02  算法配置", .025f, .64f, .155f, .72f);
        sceneTab.onClick.AddListener(() => ShowSetupTab(false));
        algorithmTab.onClick.AddListener(() => ShowSetupTab(true));
        GarageVisualStyle.Label(panel.transform, "SetupSection", "SIMULATION", 13,
            new Vector2(.025f, .845f), new Vector2(.155f, .88f), GarageVisualStyle.Muted);

        scenePage = GarageVisualStyle.Panel(panel.transform, "ScenePage", GarageVisualStyle.Background);
        GarageVisualStyle.Rect(scenePage.transform, new Vector2(.185f, .14f), new Vector2(.98f, .87f));
        GarageVisualStyle.Label(scenePage.transform, "SceneHeading", "选择作业场景", 25,
            new Vector2(.0f, .91f), new Vector2(.65f, 1f));
        var import = SetupButton(scenePage.transform, "ImportScene", "导入场景", .77f, .92f, .995f, .995f);
        import.onClick.AddListener(OpenSceneImport);
        BuildSceneGrid();

        algorithmPage = GarageVisualStyle.Panel(panel.transform, "AlgorithmPage", GarageVisualStyle.Background);
        GarageVisualStyle.Rect(algorithmPage.transform, new Vector2(.185f, .14f), new Vector2(.98f, .87f));
        var list = GarageVisualStyle.Panel(algorithmPage.transform, "AlgorithmListPanel", GarageVisualStyle.Surface);
        GarageVisualStyle.Rect(list.transform, Vector2.zero, new Vector2(.56f, 1f));
        GarageVisualStyle.Label(list.transform, "ComponentHeading", "算法组件", 23,
            new Vector2(.035f, .91f), new Vector2(.9f, .99f));
        CreateComponentSelector(list.transform, "几何感知", RosSimulationLauncher.Perceptions, .735f);
        CreateComponentSelector(list.transform, "视觉感知", RosSimulationLauncher.VisualPerceptions, .555f);
        CreateComponentSelector(list.transform, "定位", RosSimulationLauncher.Localizations, .375f);
        CreateComponentSelector(list.transform, "规划", RosSimulationLauncher.Planners, .195f);
        CreateComponentSelector(list.transform, "控制", RosSimulationLauncher.Controllers, .015f);
        var details = GarageVisualStyle.Panel(algorithmPage.transform, "AlgorithmDetails", GarageVisualStyle.Surface);
        GarageVisualStyle.Rect(details.transform, new Vector2(.585f, 0), Vector2.one);
        GarageVisualStyle.Label(details.transform, "RecipeHeading", "当前运行方案", 22,
            new Vector2(.06f, .88f), new Vector2(.94f, .98f), GarageVisualStyle.Accent);
        algorithmDetailTitle = GarageVisualStyle.Label(details.transform, "SelectedTitle", "", 22,
            new Vector2(.06f, .74f), new Vector2(.94f, .87f));
        algorithmDetailTitle.resizeTextForBestFit = true; algorithmDetailTitle.resizeTextMinSize = 15; algorithmDetailTitle.resizeTextMaxSize = 22;
        algorithmDetailId = GarageVisualStyle.Label(details.transform, "SelectedId", "", 12, Vector2.zero, Vector2.zero);
        algorithmDetailId.gameObject.SetActive(false);
        algorithmDetailDescription = GarageVisualStyle.Label(details.transform, "Description", "", 14, Vector2.zero, Vector2.zero);
        algorithmDetailDescription.gameObject.SetActive(false);
        algorithmPerceptionValue = CreateDetailRow(details.transform, "Perception", "几何感知", .63f);
        algorithmVisualPerceptionValue = CreateDetailRow(details.transform, "VisualPerception", "视觉感知", .52f);
        algorithmLocalizationValue = CreateDetailRow(details.transform, "Localization", "定位", .41f);
        algorithmPlannerValue = CreateDetailRow(details.transform, "Planner", "规划", .30f);
        algorithmControllerValue = CreateDetailRow(details.transform, "Controller", "控制", .19f);
        algorithmRouteValue = CreateDetailRow(details.transform, "Route", "路线", .08f);

        var footer = GarageVisualStyle.Panel(panel.transform, "Footer", GarageVisualStyle.Surface);
        GarageVisualStyle.Rect(footer.transform, Vector2.zero, new Vector2(1, .105f));
        currentScene = GarageVisualStyle.Label(footer.transform, "CurrentScene", "", 18,
            new Vector2(.025f, .1f), new Vector2(.49f, .9f), GarageVisualStyle.Muted);
        var next = SetupButton(footer.transform, "SetupNext", "选择算法", .52f, .2f, .67f, .8f);
        next.onClick.AddListener(() => ShowSetupTab(true));
        var cancel = SetupButton(footer.transform, "Cancel", "取消", .69f, .2f, .79f, .8f);
        cancel.onClick.AddListener(CloseSetup);
        launchSelectedAlgorithmButton = SetupButton(footer.transform, "Launch", "启动仿真", .81f, .2f, .975f, .8f, true);
        launchSelectedAlgorithmButton.onClick.AddListener(() => { if (selectedAlgorithm != null) StartSelectedAlgorithm(selectedAlgorithm.Id); });
        ClearAlgorithmSelection();
        algorithmDialog.SetActive(false);
        RefreshScenes(); ShowSetupTab(false);
    }

    private Button SetupButton(Transform parent, string name, string label, float x, float y, float right, float top, bool primary = false)
    {
        var obj = GarageVisualStyle.Panel(parent, name, primary ? GarageVisualStyle.Accent : GarageVisualStyle.Raised);
        GarageVisualStyle.Rect(obj.transform, new Vector2(x, y), new Vector2(right, top));
        var text = GarageVisualStyle.Label(obj.transform, "Label", label, 17, Vector2.zero, Vector2.one);
        text.alignment = TextAnchor.MiddleCenter;
        var button = obj.AddComponent<Button>(); GarageVisualStyle.ButtonStyle(button, primary); return button;
    }
    private void RefreshScenes()
    {
        if (currentScene != null) currentScene.text = "作业场景  /  未选择";
        foreach (var entry in sceneButtons)
        {
            bool selected = entry.scene.id == FarmScenarioCatalog.SelectedId;
            entry.button.GetComponent<Image>().color = selected ? new Color(.28f, .35f, .15f) : GarageVisualStyle.Surface;
            if (selected && currentScene != null) currentScene.text = "作业场景  /  " + entry.scene.name;
        }
        RefreshLaunchAvailability();
    }
    private void ShowSetupTab(bool algorithms)
    {
        if (scenePage == null) return;
        if (algorithmDialog.activeInHierarchy)
            AssistantConversation.RecordUi(algorithms ? "打开算法配置页" : "打开场景选择页",
                state => state.page = algorithms ? "algorithms" : "scenes");
        scenePage.SetActive(!algorithms); algorithmPage.SetActive(algorithms);
        sceneTab.GetComponent<Image>().color = algorithms ? GarageVisualStyle.Raised : new Color(.28f, .35f, .15f);
        algorithmTab.GetComponent<Image>().color = algorithms ? new Color(.28f, .35f, .15f) : GarageVisualStyle.Raised;
        RefreshScenes();
    }

    private void CloseSetup()
    {
        CancelSceneImport();
        if (sceneDeleteDialog != null && !sceneDeleteBusy) sceneDeleteDialog.SetActive(false);
        algorithmDialog.SetActive(false);
        AssistantConversation.RecordUi("关闭仿真配置页", state => state.page = "vehicle");
    }
}
