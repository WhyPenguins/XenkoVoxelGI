using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Core;
using Xenko.Shaders;

namespace Xenko.Rendering.Voxels
{
    [DataContract(DefaultMemberMode = DataMemberMode.Default)]
    [Display("Beam")]
    public class VoxelMarchBeam : IVoxelMarchMethod
    {
        public int Steps = 9;
        public float StepScale = 1.0f;
        public float BeamRadius = 1.0f;
        public VoxelMarchBeam()
        {

        }
        public VoxelMarchBeam(int steps, float stepScale, float radius)
        {
            Steps = steps;
            StepScale = stepScale;
            BeamRadius = radius;
        }
        public ShaderSource GetMarcher(int attrID)
        {
            var mixin = new ShaderMixinSource();
            mixin.Mixins.Add(new ShaderClassSource("VoxelMarchBeam", Steps, StepScale, BeamRadius));
            mixin.Macros.Add(new ShaderMacro("AttributeID", attrID));
            return mixin;
        }

        public void UpdateSamplerLayout(string compositionName)
        {
        }
        public void ApplyViewParameters(ParameterCollection parameters)
        {
        }
    }
}
