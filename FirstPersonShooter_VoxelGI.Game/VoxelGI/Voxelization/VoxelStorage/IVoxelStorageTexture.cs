using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Graphics;
using Xenko.Shaders;

namespace Xenko.Rendering.Voxels
{
    public interface IVoxelStorageTexture
    {
        void UpdateLayout(string compositionName);
        void UpdateSamplerLayout(string compositionName);
        void ApplyParametersWrite(ObjectParameterKey<Texture> MainKey, ParameterCollection parameters);
        void PostProcess(RenderDrawContext drawContext, string MipMapShader);
        ShaderClassSource GetSampler();
        void Apply(ShaderMixinSource mixin);
        void ApplyViewParameters(ParameterCollection parameters);
    }
}
