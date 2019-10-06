using System;
using System.Collections.Generic;
using System.Text;

using Xenko.Core;
using Xenko.Core.Collections;
using Xenko.Core.Diagnostics;
using Xenko.Core.Mathematics;
using Xenko.Engine;
using Xenko.Shaders;
using Xenko.Graphics;
using Xenko.Rendering.Lights;
using Xenko.Rendering.Voxels;
using Xenko.Core.Extensions;
using Xenko.Rendering;

public interface IVoxelizationMethod
{
    RenderView CollectViews(RenderStage stage, RenderVoxelVolume volume, VoxelStorageContext context, RenderContext RenderContext);
    void Render(VoxelStorageContext storageContext, IVoxelStorage Storage, RenderDrawContext drawContext);
}
