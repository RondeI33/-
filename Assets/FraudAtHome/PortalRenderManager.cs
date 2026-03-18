using UnityEngine;
using UnityEngine.Rendering;

public class PortalRenderManager : MonoBehaviour
{
    Portal[] portals;
    bool isRendering;

    void Awake()
    {
        portals = FindObjectsByType<Portal>(FindObjectsSortMode.None);
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (isRendering) return;
        if (camera != Camera.main) return;

        isRendering = true;

        for (int i = 0; i < portals.Length; i++)
            portals[i].PrePortalRender();

        for (int i = 0; i < portals.Length; i++)
            portals[i].Render();

        for (int i = 0; i < portals.Length; i++)
            portals[i].PostPortalRender();

        isRendering = false;
    }

    public void RefreshPortals()
    {
        portals = FindObjectsByType<Portal>(FindObjectsSortMode.None);
    }
}