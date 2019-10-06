using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Shaders;

namespace Xenko.Rendering.Voxels
{
    public interface IVoxelAttribute
    {
        ShaderSource GetShader();
        void PrepareLocalStorage(VoxelStorageContext context, IVoxelStorage storage);
        void UpdateLayout(string compositionName);
        void ApplyWriteParameters(ParameterCollection parameters);
        void PostProcess(RenderDrawContext drawContext);

        ShaderSource GetSampler();
        void UpdateSamplerLayout(string compositionName);
        void ApplyViewParameters(ParameterCollection parameters);

        void AddAttributes(ShaderSourceCollection modifiers);
    }
}
