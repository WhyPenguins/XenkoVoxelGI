using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Core;
using Xenko.Rendering;
using Xenko.Rendering.Voxels;
using Xenko.Shaders;

[DataContract(DefaultMemberMode = DataMemberMode.Default)]
[Display("Solidify")]
public class VoxelModifierEmissionOpacitySolidify : VoxelModifierBase, IVoxelModifierEmissionOpacity
{
    ShaderClassSource source = new ShaderClassSource("VoxelModifierSolidify");
    public bool RequiresColumns()
    {
        if (!Enabled) return false;
        return true;
    }
    public void AddAttributes(ShaderSourceCollection modifiers)
    {
        if (!Enabled) return;
        modifiers.Add(source);
    }
    public ShaderSource GetApplier(string layout)
    {
        if (!Enabled) return null;
        return new ShaderClassSource("VoxelModifierApplierSolidify" + layout);
    }

    public void PrepareLocalStorage(VoxelStorageContext context, IVoxelStorage storage)
    {
        if (!Enabled) return;
        storage.RequestTempStorage(64);
    }
    public void UpdateLayout(string compositionName) { }
    public void ApplyWriteParameters(ParameterCollection parameters) { }
}
