using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Xenko.Core;
using Xenko.Rendering.Voxels;
using Xenko.Shaders;

public interface IVoxelModifier
{
    bool RequiresColumns();
    void AddAttributes(ShaderSourceCollection modifiers);
    void PrepareLocalStorage(VoxelStorageContext context, IVoxelStorage storage);
    ShaderSource GetApplier(string layout);
}