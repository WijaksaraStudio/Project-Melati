using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CRT_VHS_FisheyeFeature : ScriptableRendererFeature
{
    class CustomPass : ScriptableRenderPass
    {
        private Material material;
        private RTHandle tempColor;

        public CustomPass(Material mat)
        {
            material = mat;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // buat RTHandle sementara
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            RenderingUtils.ReAllocateIfNeeded(ref tempColor, desc, name: "_TempCRT");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("CRT_VHS_Fisheye");

            // ambil handle camera
            var source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            // Blit ke RT sementara, apply material
            Blitter.BlitCameraTexture(cmd, source, tempColor, material, 0);

            // Blit balik ke kamera
            Blitter.BlitCameraTexture(cmd, tempColor, source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // gak perlu release manual karena RTHandle auto cleanup, 
            // tapi kalau mau paksa bisa begini:
            // tempColor?.Release();
        }
    }

    [System.Serializable]
    public class Settings
    {
        public Material material;
    }

    public Settings settings = new Settings();
    private CustomPass customPass;

    public override void Create()
    {
        if (settings.material != null)
        {
            customPass = new CustomPass(settings.material)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents
            };
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (customPass != null)
        {
            renderer.EnqueuePass(customPass);
        }
    }
}
