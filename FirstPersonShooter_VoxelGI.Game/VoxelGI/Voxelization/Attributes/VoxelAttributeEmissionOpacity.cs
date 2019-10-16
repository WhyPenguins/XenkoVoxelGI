using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Core;
using Xenko.Core.Annotations;
using Xenko.Shaders;
using Xenko.Rendering.Materials;

namespace Xenko.Rendering.Voxels
{
    [DataContract(DefaultMemberMode = DataMemberMode.Default)]
    [Display("Emission+Opacity")]
    public class VoxelAttributeEmissionOpacity : IVoxelAttribute
    {
        ShaderClassSource source = new ShaderClassSource("VoxelAttributeEmissionOpacityShader");

        public enum LightFalloffs
        {
            Sharp,
            PhysicallyBasedButNotAccurate,
            Heuristic,
        }

        [NotNull]
        public IVoxelLayout VoxelLayout { get; set; } = new VoxelLayoutIsotropic();

        public List<IVoxelModifierEmissionOpacity> Modifiers { get; set; } = new List<IVoxelModifierEmissionOpacity>();

        public LightFalloffs LightFalloff { get; set; } = LightFalloffs.Heuristic;

        public void AddAttributes(ShaderSourceCollection modifiers)
        {
            foreach (IVoxelModifierEmissionOpacity modifier in Modifiers)
            {
                modifier.AddAttributes(modifiers);
            }
        }
        public ShaderSource GetShader()
        {
            var mixin = new ShaderMixinSource();
            mixin.Mixins.Add(source);
            mixin.AddComposition("layout", VoxelLayout.GetShaderFloat4(Modifiers));
            return mixin;
        }
        public void PrepareLocalStorage(VoxelStorageContext context, IVoxelStorage storage)
        {
            foreach (var modifier in Modifiers)
            {
                modifier.PrepareLocalStorage(context, storage);
            }
            VoxelLayout.PrepareLocalStorage(context, storage);
        }

        public void UpdateLayout(string compositionName)
        {
            int i = 0;
            foreach (IVoxelModifierEmissionOpacity modifier in Modifiers)
            {
                modifier.UpdateLayout("Modifiers[" + i.ToString() + "].layout." + compositionName);
                i++;
            }
            VoxelLayout.UpdateLayout("layout." + compositionName, Modifiers);
        }
        public void ApplyWriteParameters(ParameterCollection parameters)
        {
            foreach (IVoxelModifierEmissionOpacity modifier in Modifiers)
            {
                modifier.ApplyWriteParameters(parameters);
            }
            VoxelLayout.ApplyWriteParameters(parameters, Modifiers);
        }
        public void PostProcess(RenderDrawContext drawContext)
        {
            switch (LightFalloff)
            {
                case LightFalloffs.Sharp:
                    VoxelLayout.PostProcess(drawContext, "VoxelMipmapSimple");break;
                case LightFalloffs.PhysicallyBasedButNotAccurate:
                    VoxelLayout.PostProcess(drawContext, "VoxelMipmapPhysicallyBased"); break;
                case LightFalloffs.Heuristic:
                    VoxelLayout.PostProcess(drawContext, "VoxelMipmapHeuristic"); break;
            }
        }
        public ShaderSource GetSampler()
        {
            return VoxelLayout.GetSampler();
        }
        public void UpdateSamplerLayout(string compositionName)
        {
            VoxelLayout.UpdateSamplerLayout(compositionName);
        }
        public void ApplyViewParameters(ParameterCollection parameters)
        {
            VoxelLayout.ApplyViewParameters(parameters);
        }
    }
}
