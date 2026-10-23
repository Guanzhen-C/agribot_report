using UnityEngine;

public class ShowWheelCenter : MonoBehaviour
{
    [ContextMenu("定位到轮胎中心")]
    private void LocateCenter()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning("未找到轮胎 Renderer。", this);
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        GameObject marker = new GameObject("WheelCenter");
        marker.transform.SetParent(transform, true);
        marker.transform.position = bounds.center;

        Debug.Log("轮胎世界中心：" + bounds.center, marker);
    }
}
