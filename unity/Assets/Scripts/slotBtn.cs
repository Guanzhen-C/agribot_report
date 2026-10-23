using System;
using RG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class slotBtn : MonoBehaviour
{
    [SerializeField] private slotController controller;
    [SerializeField] private int slotId;
    private OrbitCameraController orbitcontroller;
    private bottonPanel bottonPanel;

    private FixPanel fixPanel;
    private WheelDistancePanel wheelDistancePanel;
    private AxisPanel axisPanel;
    private GameObject bagPanel; 
    private BombPanel bombPanel;
    [SerializeField] private CarChange carChange;
    [SerializeField] private Transform CameraTarget2;

    private static readonly Color[] RainbowColors =
    {
        Color.red,
        new Color(1f, 0.5f, 0f),
        Color.yellow,
        Color.green,
        Color.cyan,
        Color.blue,
        Color.magenta
    };



    private GameObject selected_img;
    private int clickStep = 0;
    private Transform diedie;
    private bool selected;

    public string slotName => "Slot_" + slotId;
    private string noteName => "note_" + slotId;


    private void Awake()
    {
        if (CameraTarget2 == null)
            CameraTarget2 = GameObject.Find("CameraTarget2").transform;

        if (carChange==null)
            carChange = GameObject.Find("Car0").GetComponentInChildren<CarChange>(true);
        // 控制器绑定
        if (controller == null)
        {
            controller = gameObject.GetComponentInParent<slotController>();
            //Debug.Log("已绑定 slotController。", this);
        }
        // 自由相机绑定
        if (orbitcontroller == null)
        {
            orbitcontroller = GameObject.Find("FreeLook Camera").GetComponent<OrbitCameraController>();
        }
        // 修理面板绑定
        if (fixPanel == null)
        {
            GameObject fixPanelObject = GameObject.Find("fixPanel");
            if (fixPanelObject != null)
                fixPanel = fixPanelObject.GetComponent<FixPanel>();
        }
        // 轮胎大小变化面板绑定  // 前后轮距面板绑定
        if (bottonPanel == null || wheelDistancePanel == null || axisPanel == null || bagPanel == null)
        {
            GameObject fixPanelCanvas = GameObject.Find("FixPanelCanvas");
            if (fixPanelCanvas != null)
            {
                // 面板初始为隐藏状态，因此必须包含未激活的子对象。
                bottonPanel = fixPanelCanvas.GetComponentInChildren<bottonPanel>(true);
                wheelDistancePanel = fixPanelCanvas.GetComponentInChildren<WheelDistancePanel>(true);
                axisPanel = fixPanelCanvas.GetComponentInChildren<AxisPanel>(true);
                bombPanel = fixPanelCanvas.GetComponentInChildren<BombPanel>(true);

                Transform[] panelTransforms = fixPanelCanvas.GetComponentsInChildren<Transform>(true);
                foreach (Transform panelTransform in panelTransforms)
                {
                    if (panelTransform.name == "BagPanel")
                    {
                        bagPanel = panelTransform.gameObject;
                        break;
                    }
                }

                // 兼容场景中已经创建 AxisPanel 对象、但尚未挂载脚本的情况。
                if (axisPanel == null)
                {
                    Transform[] childTransforms = fixPanelCanvas.GetComponentsInChildren<Transform>(true);
                    foreach (Transform childTransform in childTransforms)
                    {
                        if (childTransform.name != "AxisPanel")
                            continue;

                        axisPanel = childTransform.gameObject.AddComponent<AxisPanel>();
                        break;
                    }
                }

                if (axisPanel != null && bottonPanel != null)
                    axisPanel.BindEscButton(bottonPanel);
            }
        }

        
        // 先确定根对象
        diedie = transform.parent.parent.parent.parent;
        selected = false; // 初始时未选中
        selected_img = transform.GetChild(1).GetComponent<Image>().gameObject;
    }

    // 设置本脚本是否为选中状态
    public void SetSelected(bool value)
    {

        // 选中时
        Debug.Log(clickStep);
        selected = value;
        selected_img.SetActive(selected);
        diedie.GetChild(2).gameObject.SetActive(false); // 空白介绍关闭
        diedie.GetChild(slotId + 2).gameObject.SetActive(selected); // 选中按钮的介绍界面
   
        // 取消选中时，到这里结束
        if (!value) { clickStep = 0; return; }

        // 进一步判断是第几次点击
        if (clickStep == 0) { clickStep = 1; }
        else if (clickStep == 1)
        {
            clickStep = 0;
            fixPanel.Hide();
            var host = GetComponentInParent<Car0GarageVehicle>();
            if (host != null && slotId == 4) { host.CloseRepair(); host.OpenEquipment(); return; }
            if (host != null && slotId == 5) { host.CloseRepair(); host.OpenExplodedView(); return; }
            if (slotId == 1)    // 更改轮胎轮毂长度
            {
                orbitcontroller.LockToFixedView(-180, 39, 18);
                bottonPanel.ShowEscButton();

                if (axisPanel != null)
                {
                    axisPanel.ShowBounceIn();
                }
                else
                {
                    Debug.LogError("未在 FixPanelCanvas 下找到 AxisPanel。", this);
                }
            }

            else if (slotId == 2)   // 更改前后轮之间的距离
            {
                orbitcontroller.LockToFixedView(90, 85, 22);
                bottonPanel.ShowEscButton();
                wheelDistancePanel.Show();
            }

            else if (slotId == 3)  // 更改轮胎大小
            {
                orbitcontroller.LockToFixedView(-90, 8.5f, 17);
                // 进入固定视角后，调出 bottonPanel

                if (bottonPanel != null)
                {
                    bottonPanel.Show();
                                       
                }
                else
                {
                    Debug.LogError("未在 FixPanelCanvas 下找到 WheelChangePanel。", this);
                }
            }

            else if (slotId == 4)
            {
                bottonPanel.ShowEscButton();
                orbitcontroller.LockToFixedView(-90, 39, 16);

                // 打开BagPanel
                // ShowBagPanelWithRainbow(); // Rainbow 显现效果暂时停用
                ShowBagPanelWithWaveScale();
                //carChange.ReturnAll();
                orbitcontroller.ChangeCameraTarget(CameraTarget2);

            }
            else if (slotId == 5)
            {
                // 爆炸图
                bottonPanel.ShowEscButton();
                //orbitcontroller.LockToFixedView(-90, 39, 16);
                bombPanel.gameObject.SetActive(true);
                orbitcontroller.LockToFixedView(-70, 42, 30);

            }


        }
    }

    public void OnClick()
    {
        controller.SelectSlot(this);
    }

    private void ShowBagPanelWithWaveScale()
    {
        if (bagPanel == null)
        {
            Transform[] sceneTransforms = FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (Transform sceneTransform in sceneTransforms)
            {
                if (sceneTransform.name == "BagPanel")
                {
                    bagPanel = sceneTransform.gameObject;
                    break;
                }
            }
        }

        if (bagPanel == null)
        {
            Debug.LogError("未在 FixPanelCanvas 下找到 BagPanel。", this);
            return;
        }

        bagPanel.SetActive(true);

        Button[] buttons = bagPanel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Transform element = buttons[i].transform;
            Vector3 originalScale = element.localScale;
            float offset = (Mathf.PI * 2f / buttons.Length) * i;

            RTween.Kill(element);
            RTween.Value(0f, Mathf.PI * 2f, 0.8f, value =>
                {
                    if (element != null)
                    {
                        float scale = 1f + Mathf.Sin(value + offset) * 0.3f;
                        element.localScale = originalScale * scale;
                    }
                })
                .SetEase(EaseType.Linear)
                .OnComplete(() =>
                {
                    if (element != null)
                        element.localScale = originalScale;
                });
        }
    }

    private void ShowBagPanelWithRainbow()
    {
        if (bagPanel == null)
        {
            Transform[] sceneTransforms = FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (Transform sceneTransform in sceneTransforms)
            {
                if (sceneTransform.name == "BagPanel")
                {
                    bagPanel = sceneTransform.gameObject;
                    break;
                }
            }
        }

        if (bagPanel == null)
        {
            Debug.LogError("未在 FixPanelCanvas 下找到 BagPanel。", this);
            return;
        }

        bagPanel.SetActive(true);

        Image[] images = bagPanel.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            Color originalColor = image.color;

            RTween.Kill(image);
            image.color = RainbowColors[0];

            int delayIndex = i;
            RTween.DelayedCall(delayIndex * 0.05f, () =>
                PlayRainbow(image, originalColor, 1));
        }
    }

    private void PlayRainbow(Image image, Color originalColor, int colorIndex)
    {
        if (image == null)
            return;

        Color targetColor = colorIndex < RainbowColors.Length
            ? RainbowColors[colorIndex]
            : originalColor;

        RTween.Color(image, targetColor, 0.18f)
            .SetEase(EaseType.InOutQuad)
            .OnComplete(() =>
            {
                int nextColorIndex = colorIndex + 1;
                if (nextColorIndex <= RainbowColors.Length)
                    PlayRainbow(image, originalColor, nextColorIndex);
            });
    }




}
