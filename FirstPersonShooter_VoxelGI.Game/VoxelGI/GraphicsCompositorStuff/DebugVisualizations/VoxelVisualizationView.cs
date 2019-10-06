using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Core.Mathematics;
using Xenko.Core;
using Xenko.Rendering.Images;
using Xenko.Core.Annotations;
using Xenko.Shaders;
using Xenko.Rendering.Shadows;

namespace Xenko.Rendering.Voxels
{
    [DataContract(DefaultMemberMode = DataMemberMode.Default)]
    [Display("Standard")]
    public class VoxelVisualizationView : IVoxelVisualization
    {
        [NotNull]
        public IVoxelMarchMethod MarchMethod { get; set; } = new VoxelMarchBeam(200, 1.0f, 1.0f);

        public Color Background = new Color(0.1f,0.1f,0.1f,1.0f);

        private ImageEffectShader voxelDebugEffectShader = new ImageEffectShader("VoxelVisualizationViewEffect");
        public ImageEffectShader GetShader(RenderDrawContext context, IVoxelAttribute attr)
        {
            Matrix ViewProjection = context.RenderContext.RenderView.ViewProjection;

            voxelDebugEffectShader.Parameters.Set(VoxelVisualizationViewShaderKeys.view, ViewProjection);
            voxelDebugEffectShader.Parameters.Set(VoxelVisualizationViewShaderKeys.viewInv, Matrix.Invert(ViewProjection));
            voxelDebugEffectShader.Parameters.Set(VoxelVisualizationViewShaderKeys.background, (Vector4)Background);

            attr.UpdateSamplerLayout("AttributeSamplers[0]");
            attr.ApplyViewParameters(voxelDebugEffectShader.Parameters);
            MarchMethod.UpdateSamplerLayout("marcher");
            MarchMethod.ApplyViewParameters(voxelDebugEffectShader.Parameters);
            voxelDebugEffectShader.Parameters.Set(VoxelVisualizationViewShaderKeys.marcher, MarchMethod.GetMarcher(0));

            ShaderSourceCollection collection = new ShaderSourceCollection
            {
                attr.GetSampler()
            };
            voxelDebugEffectShader.Parameters.Set(MarchAttributesKeys.AttributeSamplers, collection);

            return voxelDebugEffectShader;
        }
    }
}
