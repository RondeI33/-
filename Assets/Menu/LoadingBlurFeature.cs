using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class LoadingBlurFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader shader;
    private Material material;
    private LoadingBlurPass blurPass;

    public static float BlurAmount { get; set; } = 0f;
    public static float PixelSize { get; set; } = 1f;

    public override void Create()
    {
        if (shader == null) return;
        material = new Material(shader);
        blurPass = new LoadingBlurPass(material);
        blurPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (blurPass == null) return;
        if (renderingData.cameraData.cameraType == CameraType.Game)
            renderer.EnqueuePass(blurPass);
    }

    protected override void Dispose(bool disposing)
    {
        if (Application.isPlaying)
            Destroy(material);
        else
            DestroyImmediate(material);
    }

    private class LoadingBlurPass : ScriptableRenderPass
    {
        private Material material;
        private static readonly int blurAmountId = Shader.PropertyToID("_BlurAmount");
        private static readonly int pixelSizeId = Shader.PropertyToID("_PixelSize");

        public LoadingBlurPass(Material material)
        {
            this.material = material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (BlurAmount <= 0.001f && PixelSize <= 1.01f) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle src = resourceData.activeColorTexture;
            var desc = src.GetDescriptor(renderGraph);
            desc.name = "_LoadingBlurTex";
            desc.depthBufferBits = 0;
            var dst = renderGraph.CreateTexture(desc);

            if (!src.IsValid() || !dst.IsValid()) return;

            material.SetFloat(blurAmountId, BlurAmount);
            material.SetFloat(pixelSizeId, PixelSize);

            RenderGraphUtils.BlitMaterialParameters paraV = new(src, dst, material, 0);
            renderGraph.AddBlitPass(paraV, "LoadingBlurVertical");

            RenderGraphUtils.BlitMaterialParameters paraH = new(dst, src, material, 1);
            renderGraph.AddBlitPass(paraH, "LoadingBlurHorizontal");
        }
    }
}