// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Xenko.Core.Collections;
using Xenko.Core.Mathematics;
using Xenko.Engine;
using Xenko.Graphics;
using Xenko.Rendering.Skyboxes;
using Xenko.Shaders;
using Xenko.Rendering.Voxels;

namespace Xenko.Rendering.Lights
{
    /// <summary>
    /// Light renderer for <see cref="LightVoxel"/>.
    /// </summary>
    public class LightVoxelRenderer : LightGroupRendererBase
    {
        private readonly Dictionary<RenderLight, LightVoxelShaderGroup> lightShaderGroupsPerVoxel = new Dictionary<RenderLight, LightVoxelShaderGroup>();
        private PoolListStruct<LightVoxelShaderGroup> pool = new PoolListStruct<LightVoxelShaderGroup>(8, CreateLightVoxelShaderGroup);

        public override Type[] LightTypes { get; } = { typeof(LightVoxel) };

        public LightVoxelRenderer()
        {
            IsEnvironmentLight = true;
        }

        /// <param name="viewCount"></param>
        /// <inheritdoc/>
        public override void Reset()
        {
            base.Reset();

            foreach (var lightShaderGroup in lightShaderGroupsPerVoxel)
                lightShaderGroup.Value.Reset();

            lightShaderGroupsPerVoxel.Clear();
            pool.Reset();
        }

        /// <inheritdoc/>
        public override void ProcessLights(ProcessLightsParameters parameters)
        {
            foreach (var index in parameters.LightIndices)
            {
                // For now, we allow only one cubemap at once
                var light = parameters.LightCollection[index];

                // Prepare LightVoxelShaderGroup
                LightVoxelShaderGroup lightShaderGroup;
                if (!lightShaderGroupsPerVoxel.TryGetValue(light, out lightShaderGroup))
                {
                    lightShaderGroup = pool.Add();
                    lightShaderGroup.Light = light;

                    lightShaderGroupsPerVoxel.Add(light, lightShaderGroup);
                }
            }

            // Consume all the lights
            parameters.LightIndices.Clear();
        }

        public override void UpdateShaderPermutationEntry(ForwardLightingRenderFeature.LightShaderPermutationEntry shaderEntry)
        {
            // TODO: Some kind of sort?

            foreach (var cubemap in lightShaderGroupsPerVoxel)
            {
                shaderEntry.EnvironmentLights.Add(cubemap.Value);
            }
        }

        private static LightVoxelShaderGroup CreateLightVoxelShaderGroup()
        {
            return new LightVoxelShaderGroup(new ShaderMixinGeneratorSource("LightVoxelEffect"));
        }

        private class LightVoxelShaderGroup : LightShaderGroup
        {
            private static readonly ShaderClassSource IsotropicVoxelColorSource = new ShaderClassSource("IsotropicVoxelColor");

            private ValueParameterKey<float> intensityKey;
            private ValueParameterKey<float> intensityBounceScaleKey;
            private ValueParameterKey<Matrix> voxelMatrixKey;

            private PermutationParameterKey<ShaderSource> lightDiffuseVoxelColorKey;
            private ObjectParameterKey<Texture> voxelVolumekey;
            private ObjectParameterKey<Texture> mipMapsVolumekey;
            private ObjectParameterKey<Texture> voxelVolumekeyR1;
            private ObjectParameterKey<Texture> voxelVolumekeyR2;
            private ObjectParameterKey<Texture> voxelVolumekeyR3;
            private ObjectParameterKey<Texture> voxelVolumekeyR4;
            private ObjectParameterKey<Texture> voxelVolumekeyR5;
            private ObjectParameterKey<Texture> voxelVolumekeyR6;
            private ValueParameterKey<float> voxelVolumeMipCountKey;
            private ValueParameterKey<float> voxelVolumeClipMapCountKey;

            public RenderLight Light { get; set; }

            public LightVoxelShaderGroup(ShaderSource mixin) : base(mixin)
            {
                HasEffectPermutations = true;
            }

            public override void UpdateLayout(string compositionName)
            {
                base.UpdateLayout(compositionName);

                intensityKey = LightVoxelShaderKeys.Intensity.ComposeWith(compositionName);
                intensityBounceScaleKey = LightVoxelShaderKeys.IntensityBounceScale.ComposeWith(compositionName);
                voxelMatrixKey = LightVoxelShaderKeys.VoxelMatrix.ComposeWith(compositionName);

                lightDiffuseVoxelColorKey = LightVoxelShaderKeys.LightDiffuseVoxelColor.ComposeWith(compositionName);

                voxelVolumekey = IsotropicVoxelColorKeys.VoxelVolume.ComposeWith("lightDiffuseVoxelColor." + compositionName);
                mipMapsVolumekey = IsotropicVoxelColorKeys.VoxelMipMaps.ComposeWith("lightDiffuseVoxelColor." + compositionName);
                voxelVolumekeyR1 = IsotropicVoxelColorKeys.VoxelVolumeR1.ComposeWith("lightDiffuseVoxelColor." + compositionName);
                voxelVolumekeyR2 = IsotropicVoxelColorKeys.VoxelVolumeR2.ComposeWith("lightDiffuseVoxelColor." + compositionName);
                voxelVolumekeyR3 = IsotropicVoxelColorKeys.VoxelVolumeR3.ComposeWith("lightDiffuseVoxelColor." + compositionName);
                voxelVolumekeyR4 = IsotropicVoxelColorKeys.VoxelVolumeR4.ComposeWith("lightDiffuseVoxelColor." + compositionName);
                voxelVolumekeyR5 = IsotropicVoxelColorKeys.VoxelVolumeR5.ComposeWith("lightDiffuseVoxelColor." + compositionName);
                voxelVolumekeyR6 = IsotropicVoxelColorKeys.VoxelVolumeR6.ComposeWith("lightDiffuseVoxelColor." + compositionName);
                voxelVolumeMipCountKey = IsotropicVoxelColorKeys.MipCount.ComposeWith("lightDiffuseVoxelColor." + compositionName);
                voxelVolumeClipMapCountKey = IsotropicVoxelColorKeys.ClipMapCount.ComposeWith("lightDiffuseVoxelColor." + compositionName);
            }

            public override void ApplyEffectPermutations(RenderEffect renderEffect)
            {
                var lightVoxel = (LightVoxel)Light.Type;
                var lightDiffuseVoxelColorShader = IsotropicVoxelColorSource;
                renderEffect.EffectValidator.ValidateParameter(lightDiffuseVoxelColorKey, lightDiffuseVoxelColorShader);
            }

            public override void ApplyViewParameters(RenderDrawContext context, int viewIndex, ParameterCollection parameters)
            {
                base.ApplyViewParameters(context, viewIndex, parameters);

                var lightVoxel = ((LightVoxel)Light.Type);

                var intensity = Light.Intensity;
                var intensityBounceScale = lightVoxel.BounceIntensityScale;

                RenderVoxelVolumeData data = Xenko.Rendering.Shadows.ReflectiveVoxelRenderer.GetDataForComponent(lightVoxel.Volume);
                if (data == null)
                    return;

                var voxelMatrix = data.Matrix;// ViewProjection;// Matrix.Invert(Matrix.RotationQuaternion(lightVoxel.Rotation));
                // global parameters
                parameters.Set(intensityKey, intensity);
                parameters.Set(intensityBounceScaleKey, intensityBounceScale);
                parameters.Set(voxelMatrixKey, voxelMatrix);

                if (data.ClipMaps != null)
                {
                    parameters.Set(voxelVolumekey, data.ClipMaps);
                    parameters.Set(mipMapsVolumekey, data.MipMaps);
                }
                else
                {
                    parameters.Set(voxelVolumekey, null);
                    parameters.Set(mipMapsVolumekey, null);
                }
                parameters.Set(voxelVolumeMipCountKey, 1);
                parameters.Set(voxelVolumeClipMapCountKey, data.ClipMapCount);
            }
        }
    }
}

