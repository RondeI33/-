using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
public class AcidBuffRendererFeature : ScriptableRendererFeature
{
    public static AcidBuffRendererFeature Instance;
    public Shader acidShader;
    Material acidMaterial;
    AcidBuffPass acidPass;
    bool enabled = true;
    public override void Create()
    {
        Instance = this;
        if (acidShader != null)
            acidMaterial = CoreUtils.CreateEngineMaterial(acidShader);
        if (acidMaterial != null)
            acidPass = new AcidBuffPass(acidMaterial);
    }
    public void SetEnabled(bool value)
    {
        enabled = value;
    }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (enabled && renderingData.cameraData.cameraType == CameraType.Game && acidMaterial != null)
            renderer.EnqueuePass(acidPass);
    }
    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(acidMaterial);
    }
    class AcidBuffPass : ScriptableRenderPass
    {
        Material material;
        public AcidBuffPass(Material material)
        {
            this.material = material;
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        }
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null) return;
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid()) return;
            TextureDesc desc = renderGraph.GetTextureDesc(source);
            desc.name = "_AcidTempTexture";
            TextureHandle destination = renderGraph.CreateTexture(desc);
            RenderGraphUtils.BlitMaterialParameters blitParams =
                new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0);
            renderGraph.AddBlitPass(blitParams, passName: "AcidBuff Blit");
            renderGraph.AddBlitPass(destination, source, Vector2.one, Vector2.zero, passName: "AcidBuff Copy Back");
        }
    }
}