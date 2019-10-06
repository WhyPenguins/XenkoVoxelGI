using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Core;
using Xenko.Shaders;

namespace Xenko.Rendering.Voxels
{
    [DataContract(DefaultMemberMode = DataMemberMode.Default)]
    [Display("Cone")]
    public class VoxelMarchCone : IVoxelMarchMethod
    {
        public int Steps = 9;
        public float StepScale = 1.0f;
        public float ConeRadius = 1.0f;

        public VoxelMarchCone()
        {

        }
        public VoxelMarchCone(int steps, float stepScale, float radius)
        {
            Steps = steps;
            StepScale = stepScale;
            ConeRadius = radius;
        }
        public ShaderSource GetMarcher(int attrID)
        {
            var mixin = new ShaderMixinSource();
            mixin.Mixins.Add(new ShaderClassSource("VoxelMarchCone", Steps, StepScale, ConeRadius));
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
