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
        /*public enum PixelFormats
        {
            R11FG11FB10F_Alpha8,
            R11FG11FB10F_Alpha16F,
            R11FG11FB10F_Alpha32F,
            R10G10B10A2,
            RGBA16F,
            RGBA32F
        };*/

        ShaderClassSource source = new ShaderClassSource("VoxelAttributeEmissionOpacityShader");

        [NotNull]
        public IVoxelLayout VoxelLayout { get; set; } = new VoxelLayoutIsotropic();

        public List<IVoxelModifierEmissionOpacity> Modifiers { get; set; } = new List<IVoxelModifierEmissionOpacity>();


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
            VoxelLayout.UpdateLayout("layout." + compositionName);
        }
        public void ApplyWriteParameters(ParameterCollection parameters)
        {
            VoxelLayout.ApplyWriteParameters(parameters);
        }
        public void PostProcess(RenderDrawContext drawContext)
        {
            VoxelLayout.PostProcess(drawContext);
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
