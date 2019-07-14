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

        protected IReflectiveVoxelRenderer reflectiveVoxelRenderer;
        protected IShadowMapRenderer ShadowMapRenderer_notPrivate;
        protected override void InitializeCore()
        {
            reflectiveVoxelRenderer = Context.RenderSystem.RenderFeatures.OfType<MeshRenderFeature>().FirstOrDefault()?.RenderFeatures.OfType<ForwardLightingRenderFeatureVXGI>().FirstOrDefault()?.ReflectiveVoxelRenderer;
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
                reflectiveVoxelRenderer?.Draw(drawContext, ShadowMapRenderer_notPrivate);
            }

            base.DrawCore(context, drawContext);
        }
    }
}


