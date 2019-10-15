using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Core;
using Xenko.Shaders;

namespace Xenko.Rendering.Voxels
{
    [DataContract(DefaultMemberMode = DataMemberMode.Default)]
    [Display("Hemisphere (6)")]
    public class VoxelMarchSetHemisphere6 : IVoxelMarchSet
    {
        public IVoxelMarchMethod Marcher { set; get; } = new VoxelMarchCone(9, 1.0f, 1.7f);
        public VoxelMarchSetHemisphere6()
        {

        }
        public VoxelMarchSetHemisphere6(IVoxelMarchMethod marcher)
        {
            Marcher = marcher;
        }
        public ShaderSource GetMarcher(int attrID)
        {
            var mixin = new ShaderMixinSource();
            mixin.Mixins.Add(new ShaderClassSource("VoxelMarchSetHemisphere6"));
            mixin.AddComposition("Marcher", Marcher.GetMarcher(attrID));
            return mixin;
        }

        public void UpdateSamplerLayout(string compositionName)
        {
            Marcher.UpdateSamplerLayout("Marcher." + compositionName);
        }
        public void ApplyViewParameters(ParameterCollection parameters)
        {
            Marcher.ApplyViewParameters(parameters);
        }
    }
}
