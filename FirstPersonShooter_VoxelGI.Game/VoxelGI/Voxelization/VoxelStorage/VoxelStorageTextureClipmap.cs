using System;
using System.Collections.Generic;
using Xenko.Core.Mathematics;
using Xenko.Graphics;
using Xenko.Rendering.Shadows;
using Xenko.Shaders;

namespace Xenko.Rendering.Voxels
{
    public class VoxelStorageTextureClipmap : IVoxelStorageTexture
    {
        public Vector3 ClipMapResolution;
        public int ClipMapCount;
        public int LayoutSize;
        public Matrix VoxelMatrix;
        public float VoxelSize;

        public bool MipmapInner;

        public Xenko.Graphics.Texture ClipMaps = null;
        public Xenko.Graphics.Texture MipMaps = null;
        public Xenko.Graphics.Texture[] TempMipMaps = null;

        ShaderClassSource sampler = new ShaderClassSource("VoxelStorageTextureClipmapShader");

        public void UpdateLayout(string compositionName)
        {

        }
        public void ApplyParametersWrite(ObjectParameterKey<Texture> MainKey, ParameterCollection parameters)
        {
            parameters.Set(MainKey, ClipMaps);
        }
        Xenko.Rendering.ComputeEffect.ComputeEffectShader VoxelMipmapSimple;
        //Memory leaks if the ThreadGroupCounts/Numbers changes (I suppose due to recompiles...?)
        //so instead cache them as seperate shaders.
        Xenko.Rendering.ComputeEffect.ComputeEffectShader[] VoxelMipmapSimpleGroups;
        public void PostProcess(RenderDrawContext drawContext)
        {
            if (VoxelMipmapSimple == null)
                VoxelMipmapSimple = new Xenko.Rendering.ComputeEffect.ComputeEffectShader(drawContext.RenderContext) { ShaderSourceName = "VoxelMipmapSimple" };
            if (VoxelMipmapSimpleGroups == null || VoxelMipmapSimpleGroups.Length != TempMipMaps.Length)
            {
                if (VoxelMipmapSimpleGroups != null)
                {
                    foreach (var shader in VoxelMipmapSimpleGroups)
                    {
                        shader.Dispose();
                    }
                }
                VoxelMipmapSimpleGroups = new Xenko.Rendering.ComputeEffect.ComputeEffectShader[TempMipMaps.Length];
                for (int i = 0; i < VoxelMipmapSimpleGroups.Length; i++)
                {
                    VoxelMipmapSimpleGroups[i] = new Xenko.Rendering.ComputeEffect.ComputeEffectShader(drawContext.RenderContext) { ShaderSourceName = "VoxelMipmapSimple" };
                }
            }
            //Mipmap detailed clipmaps into less detailed ones
            Vector3 totalResolution = ClipMapResolution * new Vector3(1,LayoutSize,1);
            Int3 threadGroupCounts = new Int3(32, 32, 32);
            if (MipmapInner)
            {
                for (int i = 0; i < ClipMapCount - 1; i++)
                {
                    VoxelMipmapSimple.ThreadGroupCounts = threadGroupCounts;
                    VoxelMipmapSimple.ThreadNumbers = new Int3((int)totalResolution.X / threadGroupCounts.X, (int)totalResolution.Y / threadGroupCounts.Y, (int)totalResolution.Z / threadGroupCounts.Z);

                    VoxelMipmapSimple.Parameters.Set(VoxelMipmapSimpleKeys.ReadTex, ClipMaps);
                    VoxelMipmapSimple.Parameters.Set(VoxelMipmapSimpleKeys.WriteTex, TempMipMaps[0]);
                    VoxelMipmapSimple.Parameters.Set(VoxelMipmapSimpleKeys.ReadOffset, new Vector3(0, (int)totalResolution.Y * i, 0));
                    ((RendererBase)VoxelMipmapSimple).Draw(drawContext);

                    //Copy each axis, ignoring the top and bottom plane
                    for (int axis = 0; axis < LayoutSize; axis++)
                    {
                        int offset = axis * (int)ClipMapResolution.Y;
                        drawContext.CommandList.CopyRegion(TempMipMaps[0], 0,
                            new ResourceRegion(
                                0, offset / 2 + 1, 0,
                                (int)ClipMapResolution.X / 2, offset / 2 + (int)ClipMapResolution.Y / 2 - 1, (int)ClipMapResolution.Z / 2
                            ),
                            ClipMaps, 0,
                            (int)ClipMapResolution.X / 4, (int)totalResolution.Y * (i + 1) + offset + (int)ClipMapResolution.Y / 4 + 1, (int)ClipMapResolution.Z / 4);
                    }
                }
            }

            Vector3 resolution = ClipMapResolution;
            threadGroupCounts = new Int3((int)resolution.X, (int)resolution.Y, (int)resolution.Z) / 4;
            resolution.Y *= LayoutSize;
            //Mipmaps for the largest clipmap
            for (int i = 0; i < TempMipMaps.Length - 1; i++)
            {
                var mipmapShader = VoxelMipmapSimpleGroups[i];
                resolution /= 2;
                if (resolution.X < 1) resolution.X = 1;
                if (resolution.Y < 1) resolution.Y = 1;
                if (resolution.Z < 1) resolution.Z = 1;
                if (resolution.X < threadGroupCounts.X || resolution.Y < threadGroupCounts.Y || resolution.Z < threadGroupCounts.Z)
                {
                    threadGroupCounts /= 4;
                    if (threadGroupCounts.X < 1) threadGroupCounts.X = 1;
                    if (threadGroupCounts.Y < 1) threadGroupCounts.Y = 1;
                    if (threadGroupCounts.Z < 1) threadGroupCounts.Z = 1;
                }
                mipmapShader.ThreadGroupCounts = threadGroupCounts;
                mipmapShader.ThreadNumbers = new Int3((int)resolution.X / threadGroupCounts.X, (int)resolution.Y / threadGroupCounts.Y, (int)resolution.Z / threadGroupCounts.Z);

                if (i == 0)
                {
                    mipmapShader.Parameters.Set(VoxelMipmapSimpleKeys.ReadTex, ClipMaps);
                    mipmapShader.Parameters.Set(VoxelMipmapSimpleKeys.ReadOffset, new Vector3(0, (int)ClipMapResolution.Y * LayoutSize * (ClipMapCount - 1), 0));
                }
                else
                {
                    mipmapShader.Parameters.Set(VoxelMipmapSimpleKeys.ReadTex, TempMipMaps[i - 1]);
                    mipmapShader.Parameters.Set(VoxelMipmapSimpleKeys.ReadOffset, new Vector3(0, 0, 0));
                }
                mipmapShader.Parameters.Set(VoxelMipmapSimpleKeys.WriteTex, TempMipMaps[i]);
                ((RendererBase)mipmapShader).Draw(drawContext);
                //Don't seem to be able to read and write to the same texture, even if the views
                //point to different mipmaps.
                drawContext.CommandList.CopyRegion(TempMipMaps[i], 0, null, MipMaps, i);
            }
        }


        private ObjectParameterKey<Texture> ClipMapskey;
        private ObjectParameterKey<Texture> MipMapskey;
        private ValueParameterKey<float> clipCountKey;
        private ValueParameterKey<float> axisCountKey;
        private ValueParameterKey<float> voxelSizeKey;
        private ValueParameterKey<float> voxelSizeInvKey;
        private ValueParameterKey<Matrix> matrixKey;
        public void UpdateSamplerLayout(string compositionName)
        {
            ClipMapskey = VoxelStorageTextureClipmapShaderKeys.clipMaps.ComposeWith(compositionName);
            voxelSizeInvKey = VoxelStorageTextureClipmapShaderKeys.voxelSizeInv.ComposeWith(compositionName);
            voxelSizeKey = VoxelStorageTextureClipmapShaderKeys.voxelSize.ComposeWith(compositionName);
            MipMapskey = VoxelStorageTextureClipmapShaderKeys.mipMaps.ComposeWith(compositionName);
            clipCountKey = VoxelStorageTextureClipmapShaderKeys.clipCount.ComposeWith(compositionName);
            axisCountKey = VoxelStorageTextureClipmapShaderKeys.axisCount.ComposeWith(compositionName);
            matrixKey = VoxelStorageTextureClipmapShaderKeys.VoxelMatrix.ComposeWith(compositionName);
        }
        public ShaderClassSource GetSampler()
        {
            sampler = new ShaderClassSource("VoxelStorageTextureClipmapShader", VoxelSize, ClipMapCount, LayoutSize, ClipMapResolution.Y/2.0f);
            return sampler;
        }
        public void Apply(ShaderMixinSource mixin)
        {
            mixin.AddComposition("writer", GetSampler());
        }
        public void ApplyViewParameters(ParameterCollection parameters)
        {
            parameters.Set(ClipMapskey, ClipMaps);
            parameters.Set(MipMapskey, MipMaps);
            parameters.Set(clipCountKey, ClipMapCount);
            parameters.Set(axisCountKey, LayoutSize);
            parameters.Set(matrixKey, VoxelMatrix);
            parameters.Set(voxelSizeInvKey, 1.0f/VoxelSize);
            parameters.Set(voxelSizeKey, VoxelSize);
        }
    }
}
