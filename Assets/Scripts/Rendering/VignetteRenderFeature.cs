using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

/// <summary>
/// URP 렌더 그래프 표준 Blit 패턴으로 비네트 하드라이트 오버레이를 합성한다.
/// </summary>
public class VignetteRenderFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader vignetteShader;
    [SerializeField] private Texture vignetteTexture;

    public static float Opacity;

    private static readonly int VignetteTexId = Shader.PropertyToID("_VignetteTex");
    private static readonly int OpacityId     = Shader.PropertyToID("_Opacity");

    private VignettePass _pass;
    private Material _material;

    /// <inheritdoc/>
    public override void Create()
    {
        if (vignetteShader == null) return;
        _material = CoreUtils.CreateEngineMaterial(vignetteShader);
        if (vignetteTexture != null)
            _material.SetTexture(VignetteTexId, vignetteTexture);
        _pass = new VignettePass(_material);
    }

    /// <inheritdoc/>
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pass == null || Opacity <= 0f) return;
        _material.SetFloat(OpacityId, Opacity);
        renderer.EnqueuePass(_pass);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material);
    }

    class VignettePass : ScriptableRenderPass
    {
        private readonly Material _mat;

        class PassData
        {
            public TextureHandle src;
            public Material mat;
        }

        public VignettePass(Material mat)
        {
            _mat = mat;
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle src = resourceData.activeColorTexture;
            var desc = renderGraph.GetTextureDesc(src);
            desc.name = "VignetteHardLight";
            desc.clearBuffer = false;
            TextureHandle dst = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("VignetteHardLight", out var passData))
            {
                passData.src = src;
                passData.mat = _mat;
                builder.UseTexture(src);
                builder.SetRenderAttachment(dst, 0);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                    Blitter.BlitTexture(ctx.cmd, data.src, new Vector4(1, 1, 0, 0), data.mat, 0));
            }

            resourceData.cameraColor = dst;
        }
    }
}
