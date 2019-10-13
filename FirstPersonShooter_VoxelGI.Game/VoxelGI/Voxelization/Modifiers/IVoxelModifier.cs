using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Xenko.Core;
using Xenko.Rendering;
using Xenko.Rendering.Voxels;
using Xenko.Shaders;

public interface IVoxelModifier
{
    bool RequiresColumns();
    void AddAttributes(ShaderSourceCollection modifiers);
    void PrepareLocalStorage(VoxelStorageContext context, IVoxelStorage storage);
    void UpdateLayout(string compositionName);
    void ApplyWriteParameters(ParameterCollection parameters);
    ShaderSource GetApplier(string layout);
}