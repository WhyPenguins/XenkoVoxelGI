// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Xenko.Core;
using Xenko.Core.Annotations;
using Xenko.Core.Collections;
using Xenko.Core.Diagnostics;
using Xenko.Core.Mathematics;
using Xenko.Core.Storage;
using Xenko.Graphics;
using Xenko.Rendering.Images;
using Xenko.Rendering.Lights;
using Xenko.Rendering.Shadows;
using Xenko.Rendering.SubsurfaceScattering;
using Xenko.VirtualReality;

namespace Xenko.Rendering.Compositing
{
	/// <summary>
	/// Renders your game. It should use current <see cref="RenderContext.RenderView"/> and <see cref="CameraComponentRendererExtensions.GetCurrentCamera"/>.
	/// </summary>
	[Display("Forward renderer VXGI")]
	public class ForwardRendererVXGI : ForwardRenderer
	{

		/// <summary>
		/// The voxel rendering stage
		/// </summary>
		public RenderStage VoxelStage { get; set; }

        protected Voxels.IVoxelRenderer VoxelRenderer;
        protected IShadowMapRenderer ShadowMapRenderer_notPrivate;

        public VoxelDebug VoxelVisualization { get; set; }
        protected override void InitializeCore()
        {
            VoxelRenderer = Context.RenderSystem.RenderFeatures.OfType<MeshRenderFeature>().FirstOrDefault()?.RenderFeatures.OfType<ForwardLightingRenderFeatureVXGI>().FirstOrDefault()?.VoxelRenderer;
            ShadowMapRenderer_notPrivate = Context.RenderSystem.RenderFeatures.OfType<MeshRenderFeature>().FirstOrDefault()?.RenderFeatures.OfType<ForwardLightingRenderFeatureVXGI>().FirstOrDefault()?.ShadowMapRenderer;
            base.InitializeCore();
        }
        protected override void CollectView(RenderContext context)
		{
			if (VoxelStage != null)
			{
                context.RenderView.RenderStages.Add(VoxelStage);
			}	

			base.CollectView(context);
		}

		protected override void CollectStages(RenderContext context)
		{
			if (VoxelStage != null)
			{
				VoxelStage.OutputValidator.BeginCustomValidation(PixelFormat.None, MultisampleCount.None);
                VoxelStage.OutputValidator.Add<ColorTargetSemantic>(context.RenderOutput.RenderTargetFormat0);
                VoxelStage.OutputValidator.EndCustomValidation();
                VoxelStage.Output = new RenderOutputDescription(PixelFormat.None,PixelFormat.None);
                VoxelStage.OutputValidator.Validate(ref context.RenderOutput);
            }

			base.CollectStages(context);
        }
        protected override void DrawCore(RenderContext context, RenderDrawContext drawContext)
        {
            using (drawContext.PushRenderTargetsAndRestore())
            {
                VoxelRenderer?.Draw(drawContext, ShadowMapRenderer_notPrivate);
            }

            base.DrawCore(context, drawContext);
        }

        protected override void DrawView(RenderContext context, RenderDrawContext drawContext, int eyeIndex, int eyeCount)
        {
            var renderSystem = context.RenderSystem;

            PrepareVRConstantBuffer(context, eyeIndex, eyeCount);

            // Z Prepass
            var lightProbes = LightProbes && GBufferRenderStage != null;
            if (lightProbes)
            {
                // Note: Baking lightprobe before GBuffer prepass because we are updating some cbuffer parameters needed by Opaque pass that GBuffer pass might upload early
                PrepareLightprobeConstantBuffer(context);

                // TODO: Temporarily using ShadowMap shader
                using (drawContext.QueryManager.BeginProfile(Color.Green, CompositingProfilingKeys.GBuffer))
                using (drawContext.PushRenderTargetsAndRestore())
                {
                    drawContext.CommandList.Clear(drawContext.CommandList.DepthStencilBuffer, DepthStencilClearOptions.DepthBuffer);
                    drawContext.CommandList.SetRenderTarget(drawContext.CommandList.DepthStencilBuffer, null);

                    // Draw [main view | z-prepass stage]
                    renderSystem.Draw(drawContext, context.RenderView, GBufferRenderStage);
                }

                // Bake lightprobes against Z-buffer
                BakeLightProbes(context, drawContext);
            }

            using (drawContext.PushRenderTargetsAndRestore())
            {
                // Draw [main view | main stage]
                if (OpaqueRenderStage != null)
                {
                    using (drawContext.QueryManager.BeginProfile(Color.Green, CompositingProfilingKeys.Opaque))
                    {
                        renderSystem.Draw(drawContext, context.RenderView, OpaqueRenderStage);
                    }
                }

                Texture depthStencilSRV = null;

                // Draw [main view | subsurface scattering post process]
                if (SubsurfaceScatteringBlurEffect != null)
                {
                    var materialIndex = OpaqueRenderStage?.OutputValidator.Find<MaterialIndexTargetSemantic>() ?? -1;
                    if (materialIndex != -1)
                    {
                        using (drawContext.PushRenderTargetsAndRestore())
                        {
                            depthStencilSRV = ResolveDepthAsSRV(drawContext);

                            var renderTarget = drawContext.CommandList.RenderTargets[0];
                            var materialIndexRenderTarget = drawContext.CommandList.RenderTargets[materialIndex];

                            SubsurfaceScatteringBlurEffect.Draw(drawContext, renderTarget, materialIndexRenderTarget, depthStencilSRV, renderTarget);
                        }
                    }
                }

                // Draw [main view | transparent stage]
                if (TransparentRenderStage != null)
                {
                    // Some transparent shaders will require the depth as a shader resource - resolve it only once and set it here
                    using (drawContext.QueryManager.BeginProfile(Color.Green, CompositingProfilingKeys.Transparent))
                    using (drawContext.PushRenderTargetsAndRestore())
                    {
                        if (depthStencilSRV == null)
                            depthStencilSRV = ResolveDepthAsSRV(drawContext);

                        renderSystem.Draw(drawContext, context.RenderView, TransparentRenderStage);
                    }
                }

                var colorTargetIndex = OpaqueRenderStage?.OutputValidator.Find(typeof(ColorTargetSemantic)) ?? -1;
                if (colorTargetIndex == -1)
                    return;

                // Resolve MSAA targets
                var renderTargets = currentRenderTargets;
                var depthStencil = currentDepthStencil;
                if (actualMultisampleCount != MultisampleCount.None)
                {
                    using (drawContext.QueryManager.BeginProfile(Color.Green, CompositingProfilingKeys.MsaaResolve))
                    {
                        ResolveMSAA(drawContext);
                    }

                    renderTargets = currentRenderTargetsNonMSAA;
                    depthStencil = currentDepthStencilNonMSAA;
                }

                // Shafts if we have them
                if (LightShafts != null)
                {
                    using (drawContext.QueryManager.BeginProfile(Color.Green, CompositingProfilingKeys.LightShafts))
                    {
                        LightShafts.Draw(drawContext, depthStencil, renderTargets[colorTargetIndex]);
                    }
                }

                // Voxel Debug if enabled
                if (VoxelVisualization != null)
                {
                    VoxelVisualization.Draw(drawContext, depthStencil, renderTargets[colorTargetIndex]);
                }

                if (PostEffects != null)
                {
                    // Run post effects
                    // Note: OpaqueRenderStage can't be null otherwise colorTargetIndex would be -1
                    PostEffects.Draw(drawContext, OpaqueRenderStage.OutputValidator, renderTargets.Items, depthStencil, viewOutputTarget);
                }
                else
                {
                    if (actualMultisampleCount != MultisampleCount.None)
                    {
                        using (drawContext.QueryManager.BeginProfile(Color.Green, CompositingProfilingKeys.MsaaResolve))
                        {
                            drawContext.CommandList.Copy(renderTargets[colorTargetIndex], viewOutputTarget);
                        }
                    }
                }

                // Free the depth texture since we won't need it anymore
                if (depthStencilSRV != null)
                {
                    drawContext.Resolver.ReleaseDepthStenctilAsShaderResource(depthStencilSRV);
                }
            }
        }
    }
}


