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
    [Display("Isotropic (single)")]
    public class VoxelLayoutIsotropic : IVoxelLayout
    {
        public enum StorageFormats
        {
            R10G10B10A2,
            RGBA8,
            RGBA16F,
        };
        ShaderClassSource writer = new ShaderClassSource("VoxelIsotropicWriter_Float4");
        ShaderClassSource sampler = new ShaderClassSource("VoxelIsotropicSampler");

        [NotNull]
        public IStorageMethod StorageMethod { get; set; } = new StorageMethodIndirect();

        public StorageFormats StorageFormat { get; set; } = StorageFormats.RGBA16F;
        [Display("Max Brightness (non float format)")]
        public float maxBrightness = 10.0f;
        IVoxelStorageTexture IsotropicTex;

        public void PrepareLocalStorage(VoxelStorageContext context, IVoxelStorage storage)
        {
            StorageMethod.PrepareLocalStorage(context, storage, 4, 1);

            Graphics.PixelFormat format = Graphics.PixelFormat.R16G16B16A16_Float;
            switch (StorageFormat)
            {
                case StorageFormats.RGBA8:
                    format = Graphics.PixelFormat.R8G8B8A8_UNorm;
                    break;
                case StorageFormats.R10G10B10A2:
                    format = Graphics.PixelFormat.R10G10B10A2_UNorm;
                    break;
                case StorageFormats.RGBA16F:
                    format = Graphics.PixelFormat.R16G16B16A16_Float;
                    break;
            }
            storage.UpdateTexture(context, ref IsotropicTex, format, 1);
        }

        ValueParameterKey<float> BrightnessInvKey;
        ObjectParameterKey<Xenko.Graphics.Texture> DirectOutput;
        public void UpdateLayout(string compositionName, List<IVoxelModifierEmissionOpacity> modifiers)
        {
            DirectOutput = VoxelIsotropicWriter_Float4Keys.DirectOutput.ComposeWith(compositionName);
            BrightnessInvKey = VoxelIsotropicWriter_Float4Keys.maxBrightnessInv.ComposeWith(compositionName);
        }

        public ShaderSource GetSampler() {
            var mixin = new ShaderMixinSource();
            mixin.Mixins.Add(sampler);
            mixin.AddComposition("storage", IsotropicTex.GetSampler());
            return mixin;
        }

        ValueParameterKey<float> BrightnessKey;
        public void UpdateSamplerLayout(string compositionName)
        {
            BrightnessKey = VoxelIsotropicSamplerKeys.maxBrightness.ComposeWith(compositionName);
            IsotropicTex.UpdateSamplerLayout("storage."+compositionName);
        }
        public void ApplyViewParameters(ParameterCollection parameters)
        {
            if (StorageFormat != StorageFormats.RGBA16F)
                parameters.Set(BrightnessKey, maxBrightness);
            else
                parameters.Set(BrightnessKey, 1.0f);
            IsotropicTex.ApplyViewParameters(parameters);
        }

        public void ApplyWriteParameters(ParameterCollection parameters, List<IVoxelModifierEmissionOpacity> modifiers)
        {
            if (StorageFormat != StorageFormats.RGBA16F)
                parameters.Set(BrightnessInvKey, 1.0f / maxBrightness);
            else
                parameters.Set(BrightnessInvKey, 1.0f);
            IsotropicTex.ApplyParametersWrite(DirectOutput, parameters);
        }
        public void PostProcess(RenderDrawContext drawContext, string MipMapShader)
        {
            IsotropicTex.PostProcess(drawContext, MipMapShader);
        }


        public ShaderSource GetShaderFloat4(List<IVoxelModifierEmissionOpacity> modifiers)
        {
            var mixin = new ShaderMixinSource();
            mixin.Mixins.Add(writer);
            StorageMethod.Apply(mixin);
            foreach (var attr in modifiers)
            {
                ShaderSource applier = attr.GetApplier("Isotropic");
                if (applier!=null)
                    mixin.AddCompositionToArray("Modifiers", applier);
            }
            return mixin;
        }
        public ShaderSource GetShaderFloat3() { return null; }
        public ShaderSource GetShaderFloat2() { return null; }
        public ShaderSource GetShaderFloat1() { return null; }
    }
}
