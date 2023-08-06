using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace Volumetric
{
    public class VolumetricLight : ScriptableRendererFeature
    {
        class VolumetricLightPass : ScriptableRenderPass
        {
            RenderTargetHandle occluderMapRTHandle = RenderTargetHandle.CameraTarget;
            RenderTargetHandle depthMapRTHandle = RenderTargetHandle.CameraTarget;

            Material occluderMaterial;
            Material radialBlurMaterial;
            Material depthMaterial;

            bool useDepthTextureApproach;
            float resolutionScale;
            float intensity;
            float blurWidth;
            Color tint;

            List<ShaderTagId> shaderTagIds = new List<ShaderTagId>();  //UniversalForward, UniversalForwardOnly, LightweightForward, SRPDefaultUnlit

            string profilerTag;

            string occluderMapName = "_OccluderMap";
            string depthMapName = "_DepthMap";

            int centerID;
            int intensityID;
            int blurWidthID;
            int tintID;

            FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
            RenderTargetIdentifier cameraColorTargetIdent;

            ScriptableRenderer renderer;

            public void SetCameraColorTarget(ScriptableRenderer renderer)
            {
                this.renderer = renderer;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor cameraTextureDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                cameraTextureDescriptor.depthBufferBits = 0;
                cameraTextureDescriptor.width = Mathf.RoundToInt(cameraTextureDescriptor.width * resolutionScale);
                cameraTextureDescriptor.height = Mathf.RoundToInt(cameraTextureDescriptor.height * resolutionScale);
                
                cmd.GetTemporaryRT(occluderMapRTHandle.id, cameraTextureDescriptor, FilterMode.Bilinear);

                RenderTextureDescriptor depthMapDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                depthMapDescriptor.depthBufferBits = 0;
                depthMapDescriptor.width = renderingData.cameraData.cameraTargetDescriptor.width;
                depthMapDescriptor.height = renderingData.cameraData.cameraTargetDescriptor.height;

                cmd.GetTemporaryRT(depthMapRTHandle.id, depthMapDescriptor, FilterMode.Bilinear);

                ConfigureTarget(occluderMapRTHandle.Identifier());
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (!occluderMaterial || !radialBlurMaterial)
                {
                    return;
                }

                if (RenderSettings.sun == null || !RenderSettings.sun.enabled)
                {
                    return;
                }

                CommandBuffer cmd = CommandBufferPool.Get();
                cmd.Clear();

                using (new ProfilingScope(cmd, new ProfilingSampler(profilerTag)))
                {

                    cameraColorTargetIdent = renderer.cameraColorTarget;

                    Camera camera = renderingData.cameraData.camera;
                    context.DrawSkybox(camera);

                    DrawingSettings drawSettings = CreateDrawingSettings(shaderTagIds, ref renderingData, SortingCriteria.CommonOpaque);
                    drawSettings.overrideMaterial = occluderMaterial;

                    if (!useDepthTextureApproach)
                    {
                        context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);
                    }

                    Vector3 sunDirectionWorldSpace = RenderSettings.sun.transform.forward;

                    Vector3 cameraPositionWorldSpace = camera.transform.position;
                    Vector3 sunPositionWorldSpace = cameraPositionWorldSpace + sunDirectionWorldSpace;
                    Vector3 sunPositionViewportSpace = camera.WorldToViewportPoint(sunPositionWorldSpace);
                    sunPositionViewportSpace = sunPositionViewportSpace.normalized;

                    radialBlurMaterial.SetVector(centerID, new Vector4(sunPositionViewportSpace.x, sunPositionViewportSpace.y, 0, 0));
                    radialBlurMaterial.SetFloat(intensityID, intensity);
                    radialBlurMaterial.SetFloat(blurWidthID, blurWidth);
                    radialBlurMaterial.SetColor(tintID, tint);


                    if(useDepthTextureApproach)
                    {
                        Blit(cmd, occluderMapRTHandle.Identifier(), depthMapRTHandle.Identifier(), depthMaterial);
                        Blit(cmd, depthMapRTHandle.Identifier(), cameraColorTargetIdent, radialBlurMaterial);
                    }
                    else
                    {
                        Blit(cmd, occluderMapRTHandle.Identifier(), cameraColorTargetIdent, radialBlurMaterial);
                    }

                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                cmd.ReleaseTemporaryRT(occluderMapRTHandle.id);
                cmd.ReleaseTemporaryRT(depthMapRTHandle.id);
            }


            public VolumetricLightPass(VolumetricLightSettings settings)
            {
                occluderMapRTHandle.Init(occluderMapName);
                depthMapRTHandle.Init(depthMapName);

                useDepthTextureApproach = settings.useDepthTextureApproach;
                occluderMaterial = settings.occluderMaterial;
                radialBlurMaterial = settings.radialBlurMaterial;
                depthMaterial = settings.depthMaterial;


                resolutionScale = settings.resolutionScale;
                intensity = settings.intensity;
                blurWidth = settings.blurWidth;
                tint = settings.lightTint;
                renderPassEvent = settings.renderPassEvent;

                if (settings.shaderTags != null && settings.shaderTags.Count > 0)
                {
                    for (int i = 0; i < settings.shaderTags.Count; i++)
                    {
                        shaderTagIds.Add(new ShaderTagId(settings.shaderTags[i]));
                    }
                }

                profilerTag = settings.profilerTag;

                centerID = Shader.PropertyToID("_Center");
                intensityID = Shader.PropertyToID("_Intensity");
                blurWidthID = Shader.PropertyToID("_BlurWidth");
                tintID = Shader.PropertyToID("_Tint");
            }
        }

        VolumetricLightPass m_ScriptablePass;

        public VolumetricLightSettings settings;

        public override void Create()
        {
            m_ScriptablePass = new VolumetricLightPass(settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            m_ScriptablePass.SetCameraColorTarget(renderer);
            renderer.EnqueuePass(m_ScriptablePass);
        }
    }
}
