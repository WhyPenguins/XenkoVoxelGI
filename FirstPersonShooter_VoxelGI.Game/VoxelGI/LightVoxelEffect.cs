﻿// <auto-generated>
// Do not edit this file yourself!
//
// This code was generated by Xenko Shader Mixin Code Generator.
// To generate it yourself, please install Xenko.VisualStudio.Package .vsix
// and re-save the associated .xkfx.
// </auto-generated>

using System;
using Xenko.Core;
using Xenko.Rendering;
using Xenko.Graphics;
using Xenko.Shaders;
using Xenko.Core.Mathematics;
using Buffer = Xenko.Graphics.Buffer;

using Xenko.Rendering.Data;
namespace Xenko.Rendering.Lights
{
    internal static partial class ShaderMixins
    {
        internal partial class LightVoxelEffect  : IShaderMixinBuilder
        {
            public void Generate(ShaderMixinSource mixin, ShaderMixinContext context)
            {
                context.Mixin(mixin, "LightVoxelShader");
                if (context.GetParam(LightVoxelShaderKeys.LightDiffuseVoxelColor) != null)
                {

                    {
                        var __mixinToCompose__ = context.GetParam(LightVoxelShaderKeys.LightDiffuseVoxelColor);
                        var __subMixin = new ShaderMixinSource();
                        context.PushComposition(mixin, "lightDiffuseVoxelColor", __subMixin);
                        context.Mixin(__subMixin, __mixinToCompose__);
                        context.PopComposition();
                    }
                }
            }

            [ModuleInitializer]
            internal static void __Initialize__()

            {
                ShaderMixinManager.Register("LightVoxelEffect", new LightVoxelEffect());
            }
        }
    }
}