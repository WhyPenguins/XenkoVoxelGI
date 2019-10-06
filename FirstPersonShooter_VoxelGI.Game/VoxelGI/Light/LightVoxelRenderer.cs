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
using Xenko.Rendering.Shadows;

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
            private ValueParameterKey<float> intensityKey;
            private ValueParameterKey<float> specularIntensityKey;

            private PermutationParameterKey<ShaderSource> diffuseMarcherKey;
            private PermutationParameterKey<ShaderSource> specularMarcherKey;
            private PermutationParameterKey<ShaderSourceCollection> attributeSamplersKey;

            public RenderLight Light { get; set; }

            public LightVoxelShaderGroup(ShaderSource mixin) : base(mixin)
            {
                HasEffectPermutations = true;
            }
            VoxelAttributeEmissionOpacity GetTraceAttr()
            {
                var lightVoxel = ((LightVoxel)Light.Type);
                if (lightVoxel.Volume == null)
                    return null;
                RenderVoxelVolumeData data = Voxels.VoxelRenderer.GetDataForComponent(lightVoxel.Volume);
                if (data == null)
                    return null;

                VoxelAttributeEmissionOpacity traceAttr = null;
                foreach (var attr in data.Attributes)
                {
                    if (attr.GetType() == typeof(VoxelAttributeEmissionOpacity))
                    {
                        traceAttr = (VoxelAttributeEmissionOpacity)attr;
                    }
                }
                return traceAttr;
            }
            public override void UpdateLayout(string compositionName)
            {
                base.UpdateLayout(compositionName);

                intensityKey = LightVoxelShaderKeys.Intensity.ComposeWith(compositionName);
                specularIntensityKey = LightVoxelShaderKeys.SpecularIntensity.ComposeWith(compositionName);

                diffuseMarcherKey = LightVoxelShaderKeys.diffuseMarcher.ComposeWith(compositionName);
                specularMarcherKey = LightVoxelShaderKeys.specularMarcher.ComposeWith(compositionName);
                attributeSamplersKey = MarchAttributesKeys.AttributeSamplers.ComposeWith(compositionName);

                if (GetTraceAttr() != null)
                {
                    if (((LightVoxel)Light.Type).DiffuseMarcher != null)
                        ((LightVoxel)Light.Type).DiffuseMarcher.UpdateSamplerLayout("diffuseMarcher." + compositionName);
                    if (((LightVoxel)Light.Type).SpecularMarcher != null)
                        ((LightVoxel)Light.Type).SpecularMarcher.UpdateSamplerLayout("specularMarcher." + compositionName);
                    GetTraceAttr().UpdateSamplerLayout("AttributeSamplers[0]." + compositionName);
                }
            }

            public override void ApplyEffectPermutations(RenderEffect renderEffect)
            {
                if (GetTraceAttr() != null)
                {
                    ShaderSourceCollection collection = new ShaderSourceCollection
                    {
                        GetTraceAttr().GetSampler()
                    };
                    renderEffect.EffectValidator.ValidateParameter(attributeSamplersKey, collection);

                    if (((LightVoxel)Light.Type).DiffuseMarcher != null)
                        renderEffect.EffectValidator.ValidateParameter(diffuseMarcherKey, ((LightVoxel)Light.Type).DiffuseMarcher.GetMarcher(0));
                    if (((LightVoxel)Light.Type).SpecularMarcher != null)
                        renderEffect.EffectValidator.ValidateParameter(specularMarcherKey, ((LightVoxel)Light.Type).SpecularMarcher.GetMarcher(0));
                }
            }

            public override void ApplyViewParameters(RenderDrawContext context, int viewIndex, ParameterCollection parameters)
            {
                base.ApplyViewParameters(context, viewIndex, parameters);

                var lightVoxel = ((LightVoxel)Light.Type);

                var intensity = Light.Intensity;
                var intensityBounceScale = lightVoxel.BounceIntensityScale;
                var specularIntensity = lightVoxel.SpecularIntensityScale * intensity;

                if (viewIndex != 0)
                {
                    intensity *= intensityBounceScale;
                    specularIntensity = 0.0f;
                }

                if (lightVoxel.Volume == null)
                    return;
                RenderVoxelVolumeData data = Voxels.VoxelRenderer.GetDataForComponent(lightVoxel.Volume);
                if (data == null)
                    return;

                parameters.Set(intensityKey, intensity * 3.1415f);//I don't understand why I need to multiply by pi here...
                parameters.Set(specularIntensityKey, specularIntensity * 3.1415f);

                if (GetTraceAttr() != null)
                {
                    lightVoxel.DiffuseMarcher?.ApplyViewParameters(parameters);
                    lightVoxel.SpecularMarcher?.ApplyViewParameters(parameters);
                    GetTraceAttr().ApplyViewParameters(parameters);
                }
            }
        }
    }
}

