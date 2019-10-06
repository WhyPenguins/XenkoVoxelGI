using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Rendering.Images;

namespace Xenko.Rendering.Voxels
{
    public interface IVoxelVisualization
    {
        ImageEffectShader GetShader(RenderDrawContext context, IVoxelAttribute attr);
    }
}
