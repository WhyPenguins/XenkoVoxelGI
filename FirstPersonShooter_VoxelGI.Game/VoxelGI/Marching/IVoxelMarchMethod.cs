using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Shaders;

namespace Xenko.Rendering.Voxels
{
    public interface IVoxelMarchMethod
    {
        ShaderSource GetMarcher(int attrID);
        void UpdateSamplerLayout(string compositionName);
        void ApplyViewParameters(ParameterCollection parameters);
    }
}
