using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class BackBtn : MonoBehaviour
{
    private Button backButton;
    private bool isLoadingScene;

    private void Awake()
    {
        backButton = GetComponent<Button>();
        GarageVisualStyle.VehicleBackButton(backButton);
    }

    private void OnEnable()
    {
        if (backButton == null)
            backButton = GetComponent<Button>();

        if (backButton == null)
        {
            Debug.LogError("BackBtn 需要挂在带有 Button 组件的 Back_btn 对象上。", this);
            return;
        }

        backButton.onClick.RemoveListener(ReturnToScene1);
        backButton.onClick.AddListener(ReturnToScene1);
        // Car0's full-screen configuration panels otherwise cover this sibling.
        transform.SetAsLastSibling();
    }

    private void OnDisable()
    {
        if (backButton != null)
            backButton.onClick.RemoveListener(ReturnToScene1);
    }

    public void ReturnToScene1()
    {
        if (isLoadingScene)
            return;

        const string scenePath = "Assets/Scenes/scene1.unity";
        int buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);
        if (buildIndex < 0)
        {
            Debug.LogError($"无法返回 scene1：场景未加入 Build Settings。请添加 {scenePath}", this);
            return;
        }

        if (SceneManager.GetActiveScene().path == scenePath)
            return;

        isLoadingScene = true;
        Time.timeScale = 1f;
        backButton.interactable = false;
        backButton.GetComponentInChildren<Text>().text = "正在返回…";
        StartCoroutine(LoadGarage(buildIndex));
    }

    private IEnumerator LoadGarage(int buildIndex)
    {
        // Render the click acknowledgement before scene deserialization/Awake work.
        yield return null;
        yield return SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
    }
}
