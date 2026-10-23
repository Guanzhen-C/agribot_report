using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;  

public class MouseButtonDrag : MonoBehaviour
{
    public Transform target;
    private CinemachineInputAxisController inputAxisController;
  

    void Start()
    {
        if (target == null)
        {
            target = GameObject.Find("AgriculturalMachine2")?.transform;
        }

        inputAxisController = GetComponent<CinemachineInputAxisController>();
        if (inputAxisController == null)
        {
            Debug.LogError("未找到 CinemachineInputAxisController 组件！");
            return;
        }
        // 初始时禁用
        inputAxisController.enabled = false;

        // 找到鼠标滚轮
        //actionAsset.FindAction("Mouse Zoom");
        //scrollRaw = Input.GetAxisRaw("Mouse ScrollWheel");

        //inputProvider.FindAction("Mouse Zoom").Enable();
       
    }

    void Update()
    {
        if (inputAxisController == null) return;

        //print(scrollRaw);
        // 4. 根据左键状态切换组件启用
        bool mousePressed = Input.GetMouseButton(0);
        bool touchPressed = Touchscreen.current != null &&
                            Touchscreen.current.primaryTouch.press.isPressed;

        if (mousePressed || touchPressed)
        {
            inputAxisController.enabled = true;

        }
        else
        {
            inputAxisController.enabled = false;
        }

       

    }
}
