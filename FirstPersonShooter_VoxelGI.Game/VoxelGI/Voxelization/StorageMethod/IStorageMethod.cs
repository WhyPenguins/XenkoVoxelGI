using System;
using System.Collections.Generic;
using Xenko.Shaders;

namespace Xenko.Rendering.Voxels
{
    public interface IStorageMethod
    {
        void Apply(ShaderMixinSource mixin);
        void PrepareLocalStorage(VoxelStorageContext context, IVoxelStorage storage, int channels, int layoutCount);
    }
}
