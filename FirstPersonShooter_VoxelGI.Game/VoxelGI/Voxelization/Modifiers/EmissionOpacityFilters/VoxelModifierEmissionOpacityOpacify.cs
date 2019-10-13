using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Core;
using Xenko.Rendering;
using Xenko.Rendering.Voxels;
using Xenko.Shaders;

[DataContract(DefaultMemberMode = DataMemberMode.Default)]
[Display("Opacify")]
public class VoxelModifierEmissionOpacityOpacify : VoxelModifierBase, IVoxelModifierEmissionOpacity
{
    public float Amount = 2.0f;
    public bool RequiresColumns()
    {
        if (!Enabled) return false;
        return true;
    }
    public void AddAttributes(ShaderSourceCollection modifiers)
    {
        if (!Enabled) return;
    }
    public ShaderSource GetApplier(string layout)
    {
        if (!Enabled) return null;
        return new ShaderClassSource("VoxelModifierApplierOpacify" + layout);
    }

    public void PrepareLocalStorage(VoxelStorageContext context, IVoxelStorage storage)
    {
    }

    ValueParameterKey<float> AmountKey;
    public void UpdateLayout(string compositionName)
    {
        AmountKey = VoxelModifierApplierOpacifyIsotropicKeys.Amount.ComposeWith(compositionName);
    }

    public void ApplyWriteParameters(ParameterCollection parameters)
    {
        parameters.Set(AmountKey, Amount);
    }
}
