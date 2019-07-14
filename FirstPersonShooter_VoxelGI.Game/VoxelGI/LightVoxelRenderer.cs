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
            private ObjectParameterKey<Texture> voxelVolumekeyR1;
            private ObjectParameterKey<Texture> voxelVolumekeyR2;
            private ObjectParameterKey<Texture> voxelVolumekeyR3;
            private ObjectParameterKey<Texture> voxelVolumekeyR4;
            private ObjectParameterKey<Texture> voxelVolumekeyR5;
            private ObjectParameterKey<Texture> voxelVolumekeyR6;
            private ValueParameterKey<float> voxelVolumeMipCountKey;

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
                voxelVolumekeyR1 = IsotropicVoxelColorKeys.VoxelVolumeR1.ComposeWith("lightDiffuseVoxelColor." + compositionName);
                voxelVolumekeyR2 = IsotropicVoxelColorKeys.VoxelVolumeR2.ComposeWith("lightDiffuseVoxelColor." + compositionName);
                voxelVolumekeyR3 = IsotropicVoxelColorKeys.VoxelVolumeR3.ComposeWith("lightDiffuseVoxelColor." + compositionName);
                voxelVolumekeyR4 = IsotropicVoxelColorKeys.VoxelVolumeR4.ComposeWith("lightDiffuseVoxelColor." + compositionName);
                voxelVolumekeyR5 = IsotropicVoxelColorKeys.VoxelVolumeR5.ComposeWith("lightDiffuseVoxelColor." + compositionName);
                voxelVolumekeyR6 = IsotropicVoxelColorKeys.VoxelVolumeR6.ComposeWith("lightDiffuseVoxelColor." + compositionName);
                voxelVolumeMipCountKey = IsotropicVoxelColorKeys.MipCount.ComposeWith("lightDiffuseVoxelColor." + compositionName);
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

                var voxelMatrix = Xenko.Rendering.Shadows.ReflectiveVoxelRenderer.VoxelMatrix;// ViewProjection;// Matrix.Invert(Matrix.RotationQuaternion(lightVoxel.Rotation));
                // global parameters
                parameters.Set(intensityKey, intensity);
                parameters.Set(intensityBounceScaleKey, intensityBounceScale);
                parameters.Set(voxelMatrixKey, voxelMatrix);

                if (Xenko.Rendering.Shadows.ReflectiveVoxelRenderer.VoxelsTex != null && Xenko.Rendering.Shadows.ReflectiveVoxelRenderer.VoxelsTex[3] != null)
                {
                    parameters.Set(voxelVolumekey, Xenko.Rendering.Shadows.ReflectiveVoxelRenderer.VoxelsTex[0]);
                    parameters.Set(voxelVolumekeyR1, Xenko.Rendering.Shadows.ReflectiveVoxelRenderer.VoxelsTex[1]);
                    parameters.Set(voxelVolumekeyR2, Xenko.Rendering.Shadows.ReflectiveVoxelRenderer.VoxelsTex[2]);
                    parameters.Set(voxelVolumekeyR3, Xenko.Rendering.Shadows.ReflectiveVoxelRenderer.VoxelsTex[3]);
                    parameters.Set(voxelVolumekeyR4, Xenko.Rendering.Shadows.ReflectiveVoxelRenderer.VoxelsTex[4]);
                    parameters.Set(voxelVolumekeyR5, Xenko.Rendering.Shadows.ReflectiveVoxelRenderer.VoxelsTex[5]);
                    parameters.Set(voxelVolumekeyR6, Xenko.Rendering.Shadows.ReflectiveVoxelRenderer.VoxelsTex[6]);
                }
                else
                {
                    parameters.Set(voxelVolumekey, null);
                    parameters.Set(voxelVolumekeyR1, null);
                    parameters.Set(voxelVolumekeyR2, null);
                    parameters.Set(voxelVolumekeyR3, null);
                    parameters.Set(voxelVolumekeyR4, null);
                    parameters.Set(voxelVolumekeyR5, null);
                    parameters.Set(voxelVolumekeyR6, null);
                }
                parameters.Set(voxelVolumeMipCountKey, 1);
            }
        }
    }
}

