using System;
using System.Collections.Generic;
using Xenko.Shaders;

namespace Xenko.Rendering.Voxels
{
    public interface IVoxelLayout
    {
        void PrepareLocalStorage(VoxelStorageContext context, IVoxelStorage storage);
        void UpdateLayout(string compositionName, List<IVoxelModifierEmissionOpacity> modifiers);
        ShaderSource GetSampler();
        void UpdateSamplerLayout(string compositionName);
        void ApplyViewParameters(ParameterCollection parameters);
        void ApplyWriteParameters(ParameterCollection parameters, List<IVoxelModifierEmissionOpacity> modifiers);
        void PostProcess(RenderDrawContext drawContext, string MipMapShader);
        ShaderSource GetShaderFloat4(List<IVoxelModifierEmissionOpacity> modifiers);
        ShaderSource GetShaderFloat3();
        ShaderSource GetShaderFloat2();
        ShaderSource GetShaderFloat1();
    }
}
