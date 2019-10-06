using System;
using System.Collections.Generic;
using Xenko.Graphics;
using Xenko.Shaders;

namespace Xenko.Rendering.Voxels
{
    public interface IVoxelStorage
    {
        void UpdateFromContext(VoxelStorageContext context, RenderVoxelVolumeData data);
        RenderView CollectViews(VoxelStorageContext context, RenderContext RenderContext, RenderView view);
        void Render(VoxelStorageContext context, RenderDrawContext drawContext, RenderView view);
        void RequestTempStorage(int count);
        void UpdateTexture(VoxelStorageContext context, ref IVoxelStorageTexture texture, Xenko.Graphics.PixelFormat pixelFormat, int layoutCount);
        void UpdateTempStorage(VoxelStorageContext context);
        void PostProcess(VoxelStorageContext context, RenderDrawContext drawContext, RenderVoxelVolumeData data);
        void ApplyWriteParameters(ParameterCollection param);
        void UpdateLayout(string compositionName);
        ShaderSource GetSource(RenderVoxelVolumeData data);
    }
}
