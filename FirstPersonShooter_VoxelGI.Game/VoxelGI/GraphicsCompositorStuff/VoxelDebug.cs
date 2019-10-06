using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Graphics;
using Xenko.Core;
using Xenko.Core.Annotations;
using Xenko.Core.Collections;
using Xenko.Core.Extensions;
using Xenko.Core.Mathematics;
using Xenko.Shaders;
using Xenko.Rendering.Images;
using Xenko.Rendering.Voxels;
using Xenko.Rendering.Lights;
using Xenko.Rendering.Skyboxes;
using System.Linq;

namespace Xenko.Rendering.Compositing
{
    [DataContract("VoxelDebug")]
    public class VoxelDebug : ImageEffect
    {
        protected override void InitializeCore()
        {
            base.InitializeCore();
        }
        VoxelAttributeEmissionOpacity GetTraceAttr(RenderVoxelVolumeData data)
        {
            if (data == null)
                return null;

            VoxelAttributeEmissionOpacity traceAttr = null;
            foreach (var attr in data.Attributes)
            {
                if (attr.GetType() == typeof(VoxelAttributeEmissionOpacity))
                {
                    traceAttr = (VoxelAttributeEmissionOpacity)attr;
                }
            }
            return traceAttr;
        }
        protected override void DrawCore(RenderDrawContext context)
        {
            if (!Initialized)
                Initialize(context.RenderContext);
            Dictionary<VoxelVolumeComponent, RenderVoxelVolumeData> datas = Voxels.VoxelRenderer.GetDatas();

            foreach (var datapairs in datas)
            {
                var data = datapairs.Value;

                if (!data.VisualizeVoxels || data.VoxelVisualization == null || GetTraceAttr(data) == null)
                    continue;

                ImageEffectShader shader = data.VoxelVisualization.GetShader(context, GetTraceAttr(data));

                if (shader == null)
                    continue;

                shader.SetInput(0, GetSafeInput(0));
                shader.SetOutput(GetSafeOutput(0));

                shader.Draw(context);
            }
        }
        public void Draw(RenderDrawContext drawContext, Texture inputDepthStencil, Texture output)
        {
            SetInput(0, inputDepthStencil);
            SetOutput(output);
            DrawCore(drawContext);
        }
    }
}
