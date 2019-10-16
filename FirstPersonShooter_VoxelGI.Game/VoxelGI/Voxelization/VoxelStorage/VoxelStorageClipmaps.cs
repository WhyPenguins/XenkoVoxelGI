using System;
using System.Collections.Generic;
using Xenko.Core.Mathematics;
using Xenko.Core;
using Xenko.Core.Annotations;
using Xenko.Graphics;
using Xenko.Shaders;

namespace Xenko.Rendering.Voxels
{
    [DataContract(DefaultMemberMode = DataMemberMode.Default)]
    [Display("Clipmaps")]
    public class VoxelStorageClipmaps : IVoxelStorage
    {
        public enum Resolutions{
            x32 = 32,
            x64 = 64,
            x128 = 128,
            x256 = 256
        };
        public enum RenderMethods
        {
            MultipleRenders,
            GeometryShader
        };
        public Resolutions ClipResolution { get; set; } = Resolutions.x128;
        public RenderMethods RenderMethod { get; set; } = RenderMethods.GeometryShader;
        public bool MipmapInner { get; set; } = true;

        int storageUints;
        Xenko.Graphics.Buffer FragmentsBuffer = null;

        int ClipMapCount;
        float InnerClipMapScale;
        Vector3 ClipMapResolution;
        Matrix VoxelMatrix;
        Matrix TextureMatrix;
        Matrix InnerTextureMatrix;

        public void UpdateFromContext(VoxelStorageContext context, RenderVoxelVolumeData data)
        {
            VoxelMatrix = context.Matrix;

            var resolution = context.Resolution();
            var maxRes = (double)Math.Max(resolution.X, Math.Max(resolution.Y, resolution.Z));

            ClipMapCount = (int)Math.Log(maxRes / Math.Min(maxRes, (double)ClipResolution), 2) + 1;
            InnerClipMapScale = (float)Math.Pow(2, ClipMapCount - 1);
            ClipMapResolution = new Vector3(resolution.X, resolution.Y, resolution.Z) / InnerClipMapScale;

            var viewToTexTrans = Matrix.Translation(0.5f, 0.5f, 0.5f);
            var viewToTexScale = Matrix.Scaling(0.5f, 0.5f, 0.5f);

            Matrix BaseVoxelMatrix = VoxelMatrix;
            BaseVoxelMatrix.Invert();
            BaseVoxelMatrix = BaseVoxelMatrix * Matrix.Scaling(2f, 2f, 2f);

            TextureMatrix = BaseVoxelMatrix * viewToTexScale * viewToTexTrans;
            InnerTextureMatrix = BaseVoxelMatrix * Matrix.Scaling(InnerClipMapScale) * viewToTexScale * viewToTexTrans;

        }


        int tempstorageCounter;
        public void RequestTempStorage(int count)
        {
            tempstorageCounter += ((count+31)/32)*32;
        }

        private bool NeedToRecreateBuffer(Xenko.Graphics.Buffer buf, int count)
        {
            if (buf == null || buf.ElementCount != count)
            {
                if (buf != null)
                    buf.Dispose();

                return true;
            }
            return false;
        }
        public void UpdateTempStorage(VoxelStorageContext context)
        {
            storageUints = (tempstorageCounter+31)/32;
            tempstorageCounter = 0;

            var resolution = ClipMapResolution;
            int fragments = (int)(resolution.X * resolution.Y * resolution.Z) * ClipMapCount;

            if (NeedToRecreateBuffer(FragmentsBuffer, storageUints * fragments) && storageUints * fragments > 0)
            {
                FragmentsBuffer = Xenko.Graphics.Buffer.Typed.New(context.device, storageUints * fragments, PixelFormat.R32_UInt, true);
            }
        }



        private bool TextureDimensionsEqual(Texture tex, Vector3 dim)
        {
            return (tex.Width == dim.X &&
                    tex.Height == dim.Y &&
                    tex.Depth == dim.Z);
        }
        private bool NeedToRecreateTexture(Xenko.Graphics.Texture tex, Vector3 dim, Xenko.Graphics.PixelFormat pixelFormat)
        {
            if (tex == null || !TextureDimensionsEqual(tex, dim) || tex.Format != pixelFormat)
            {
                if (tex != null)
                    tex.Dispose();

                return true;
            }
            return false;
        }
        public void UpdateTexture(VoxelStorageContext context, ref IVoxelStorageTexture texture, Xenko.Graphics.PixelFormat pixelFormat, int layoutCount)
        {
            VoxelStorageTextureClipmap clipmap = texture as VoxelStorageTextureClipmap;
            if (clipmap == null)
            {
                clipmap = new VoxelStorageTextureClipmap();
            }

            Vector3 ClipMapTextureResolution = new Vector3(ClipMapResolution.X, ClipMapResolution.Y * ClipMapCount * layoutCount, ClipMapResolution.Z);
            Vector3 MipMapResolution = new Vector3(ClipMapResolution.X / 2, ClipMapResolution.Y / 2 * layoutCount, ClipMapResolution.Z / 2);
            Vector3 MipMapNoLayoutResolution = new Vector3(ClipMapResolution.X / 2, ClipMapResolution.Y / 2, ClipMapResolution.Z / 2);
            if (NeedToRecreateTexture(clipmap.ClipMaps, ClipMapTextureResolution, pixelFormat))
            {
                clipmap.ClipMaps = Xenko.Graphics.Texture.New3D(context.device, (int)ClipMapTextureResolution.X, (int)ClipMapTextureResolution.Y, (int)ClipMapTextureResolution.Z, new MipMapCount(false), pixelFormat, TextureFlags.ShaderResource | TextureFlags.UnorderedAccess);
            }
            if (NeedToRecreateTexture(clipmap.MipMaps, MipMapResolution, pixelFormat))
            {
                if (clipmap.TempMipMaps != null)
                {
                    for (int i = 0; i < clipmap.TempMipMaps.Length; i++)
                    {
                        clipmap.TempMipMaps[i].Dispose();
                    }
                }


                Vector3 MipMapResolutionMax = MipMapResolution;
                int mipCount = 1 + (int)Math.Floor(Math.Log(Math.Min(MipMapNoLayoutResolution.X, Math.Min(MipMapNoLayoutResolution.Y, MipMapNoLayoutResolution.Z)), 2));

                clipmap.MipMaps = Xenko.Graphics.Texture.New3D(context.device, (int)MipMapResolution.X, (int)MipMapResolution.Y, (int)MipMapResolution.Z, new MipMapCount(true), pixelFormat, TextureFlags.ShaderResource | TextureFlags.UnorderedAccess);

                clipmap.TempMipMaps = new Xenko.Graphics.Texture[mipCount];

                for (int i = 0; i < clipmap.TempMipMaps.Length; i++)
                {
                    clipmap.TempMipMaps[i] = Xenko.Graphics.Texture.New3D(context.device, (int)MipMapResolutionMax.X, (int)MipMapResolutionMax.Y, (int)MipMapResolutionMax.Z, false, pixelFormat, TextureFlags.ShaderResource | TextureFlags.UnorderedAccess);

                    MipMapResolutionMax /= 2;
                }
            }
            clipmap.MipmapInner = MipmapInner;
            clipmap.ClipMapResolution = ClipMapResolution;
            clipmap.ClipMapCount = ClipMapCount;
            clipmap.LayoutSize = layoutCount;
            clipmap.VoxelMatrix = InnerTextureMatrix;
            clipmap.VoxelSize = context.RealVoxelSize();

            texture = clipmap;
        }

        
        public RenderView CollectViews(VoxelStorageContext context, RenderContext RenderContext, RenderView view)
        {
            if (RenderMethod == RenderMethods.GeometryShader)
            {
                float maxRes = Math.Max(ClipMapResolution.X, Math.Max(ClipMapResolution.Y, ClipMapResolution.Z));
                Matrix aspectScale = Matrix.Scaling(ClipMapResolution/maxRes);
                view.Projection *= aspectScale;
                view.ViewProjection = view.View * view.Projection;

                view.ViewSize = new Vector2(maxRes * 8, maxRes * 8);// * 8 improves shadow quality drastically, TODO: maybe should be configurable?
                RenderContext.RenderSystem.Views.Add(view);
                RenderContext.VisibilityGroup.TryCollect(view);
                return view;
            }
            return null;
        }
        public void Render(VoxelStorageContext context, RenderDrawContext drawContext, RenderView view)
        {
            var renderSystem = drawContext.RenderContext.RenderSystem;

            var resolution = ClipMapResolution;


            float maxResolution = Math.Max(Math.Max(resolution.X, resolution.Y), resolution.Z);
            drawContext.CommandList.SetViewport(new Viewport(0, 0, (int)maxResolution, (int)maxResolution));

            if (RenderMethod == RenderMethods.GeometryShader)
            {
                renderSystem.Draw(drawContext, view, renderSystem.RenderStages[view.RenderStages[0].Index]);
            }
        }

        Xenko.Rendering.ComputeEffect.ComputeEffectShader BufferToTextureColumns;
        Xenko.Rendering.ComputeEffect.ComputeEffectShader ClearBuffer;
        public void PostProcess(VoxelStorageContext storageContext, RenderDrawContext drawContext, RenderVoxelVolumeData data)
        {
            if (Math.Max(Math.Max(ClipMapResolution.X, ClipMapResolution.Y), ClipMapResolution.Z) < 32)
                return;
            if (FragmentsBuffer == null)
                return;
            var context = drawContext.RenderContext;
            if (ClearBuffer == null)
            {
                ClearBuffer = new Xenko.Rendering.ComputeEffect.ComputeEffectShader(context) { ShaderSourceName = "ClearBuffer" };
                //BufferToTexture = new Xenko.Rendering.ComputeEffect.ComputeEffectShader(Context) { ShaderSourceName = "BufferToTextureEffect" };
                BufferToTextureColumns = new Xenko.Rendering.ComputeEffect.ComputeEffectShader(context) { ShaderSourceName = "BufferToTextureColumnsEffect" };
            }
            var BufferWriter = BufferToTextureColumns;
            for (int i = 0; i < data.Attributes.Count; i++)
            {
                var attr = data.Attributes[i];
                attr.UpdateLayout("AttributesIndirect[" + i + "]");
            }
            foreach (var attr in data.Attributes)
            {
                attr.ApplyWriteParameters(BufferWriter.Parameters);
            }
            if (BufferWriter == BufferToTextureColumns)
            {
                BufferWriter.ThreadGroupCounts = new Int3(32, 1, 32);
                BufferWriter.ThreadNumbers = new Int3((int)ClipMapResolution.X / BufferWriter.ThreadGroupCounts.X, ClipMapCount / BufferWriter.ThreadGroupCounts.Y, (int)ClipMapResolution.Z / BufferWriter.ThreadGroupCounts.Z);
            }
            else
            {
                BufferWriter.ThreadGroupCounts = new Int3(32, 16, 32);
                BufferWriter.ThreadNumbers = new Int3((int)ClipMapResolution.X / BufferWriter.ThreadGroupCounts.X, (int)ClipMapResolution.Y * ClipMapCount / BufferWriter.ThreadGroupCounts.Y, (int)ClipMapResolution.Z / BufferWriter.ThreadGroupCounts.Z);
            }
                BufferWriter.Parameters.Set(BufferToTextureKeys.VoxelFragments, FragmentsBuffer);
                BufferWriter.Parameters.Set(BufferToTextureKeys.clipMapResolution, ClipMapResolution);
                BufferWriter.Parameters.Set(BufferToTextureKeys.storageUints, storageUints);
                BufferWriter.Parameters.Set(BufferToTextureKeys.AttributesIndirect, data.AttributeIndirect);
                BufferWriter.Parameters.Set(BufferToTextureKeys.Modifiers, data.AttributeModifiers);
            ((RendererBase)BufferWriter).Draw(drawContext);

            drawContext.CommandList.ClearReadWrite(FragmentsBuffer, new Vector4(0));
            ClearBuffer.ThreadGroupCounts = new Int3(4096 * storageUints * 2, 1, 1);
            ClearBuffer.ThreadNumbers = new Int3(FragmentsBuffer.ElementCount / ClearBuffer.ThreadGroupCounts.X, 1 / ClearBuffer.ThreadGroupCounts.Y, 1 / ClearBuffer.ThreadGroupCounts.Z);
            ClearBuffer.Parameters.Set(ClearBufferKeys.buffer, FragmentsBuffer);
            ((RendererBase)ClearBuffer).Draw(drawContext);
        }
        ObjectParameterKey<Xenko.Graphics.Buffer> fragmentsBufferKey;
        ValueParameterKey<Vector3> clipMapResolutionKey;
        ValueParameterKey<float> storageUintsKey;
        ValueParameterKey<float> clipMapCountKey;
        ValueParameterKey<Matrix> VoxelMatrixKey;
        public void UpdateLayout(string compositionName)
        {
            fragmentsBufferKey = VoxelStorageClipmapShaderKeys.fragmentsBuffer.ComposeWith(compositionName);
            clipMapResolutionKey = VoxelStorageClipmapShaderKeys.clipMapResolution.ComposeWith(compositionName);
            storageUintsKey = VoxelStorageClipmapShaderKeys.storageUints.ComposeWith(compositionName);
            clipMapCountKey = VoxelStorageClipmapShaderKeys.clipMapCount.ComposeWith(compositionName);
            VoxelMatrixKey = VoxelStorageClipmapShaderKeys.VoxelMatrix.ComposeWith(compositionName);
        }
        public void ApplyWriteParameters(ParameterCollection param)
        {
            param.Set(fragmentsBufferKey, FragmentsBuffer);
            param.Set(clipMapResolutionKey, ClipMapResolution);
            param.Set(storageUintsKey, storageUints);
            param.Set(clipMapCountKey, ClipMapCount);
            param.Set(VoxelMatrixKey, TextureMatrix);
        }

        ShaderClassSource storage = new ShaderClassSource("VoxelStorageClipmapShader");
        public ShaderSource GetSource(RenderVoxelVolumeData data)
        {

            var writermixin = new ShaderMixinSource();
            writermixin.Mixins.Add(storage);
            foreach (var attr in data.AttributeIndirect)
            {
                writermixin.AddCompositionToArray("AttributesIndirect", attr);
            }
            foreach (var attr in data.AttributeModifiers)
            {
                writermixin.AddCompositionToArray("Modifiers", attr);
            }
            return writermixin;
        }
    }
}
