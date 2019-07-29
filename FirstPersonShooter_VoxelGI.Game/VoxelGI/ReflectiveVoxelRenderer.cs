using System;
using System.Collections.Generic;
using System.Text;

using Xenko.Core;
using Xenko.Core.Collections;
using Xenko.Core.Diagnostics;
using Xenko.Core.Mathematics;
using Xenko.Engine;
using Xenko.Graphics;
using Xenko.Rendering.Lights;
using Xenko.Rendering.Voxels;
namespace Xenko.Rendering.Shadows
{
    [DataContract(DefaultMemberMode = DataMemberMode.Default)]
    public class ReflectiveVoxelRenderer : IReflectiveVoxelRenderer
    {
        [DataMemberIgnore]
        public static readonly PropertyKey<Dictionary<VoxelVolumeComponent, RenderVoxelVolume>> CurrentRenderVoxelVolumes = new PropertyKey<Dictionary<VoxelVolumeComponent, RenderVoxelVolume>>("ReflectiveVoxelRenderer.CurrentRenderVoxelVolumes", typeof(ReflectiveVoxelRenderer));
        private static Dictionary<VoxelVolumeComponent, RenderVoxelVolume> renderVoxelVolumes;
        private static readonly Dictionary<VoxelVolumeComponent, RenderVoxelVolumeData> renderVoxelVolumeData = new Dictionary<VoxelVolumeComponent, RenderVoxelVolumeData>();

        public static readonly ProfilingKey FragmentVoxelizationProfilingKey = new ProfilingKey("Voxelization: Fragments");
        public static readonly ProfilingKey ArrangementVoxelizationProfilingKey = new ProfilingKey("Voxelization: Arrangement");
        public static readonly ProfilingKey MipmappingVoxelizationProfilingKey = new ProfilingKey("Voxelization: Mipmapping");

        public RenderStage VoxelStage { get; set; }
        protected static Dictionary<RenderView, VoxelVolumeComponent> reflectiveVoxelViews = new Dictionary<RenderView, VoxelVolumeComponent>();

        public static Xenko.Graphics.Buffer Fragments = null;
        public static Xenko.Graphics.Buffer FragmentsCounter = null;
        public static Xenko.Graphics.Texture[] TempMipMaps = null;
        public static Xenko.Graphics.Texture MSAARenderTarget = null;

        Xenko.Rendering.ComputeEffect.ComputeEffectShader Generate3DMipmaps;
        Xenko.Rendering.ComputeEffect.ComputeEffectShader ClearBuffer;
        Xenko.Rendering.ComputeEffect.ComputeEffectShader ArrangeFragments;

        public static RenderVoxelVolumeData GetDataForComponent(VoxelVolumeComponent component)
        {
            if (!renderVoxelVolumeData.TryGetValue(component, out var data))
                return null;
            return data;
        }
        public static RenderVoxelVolumeData GetDataForView(RenderView view)
        {
            if (!reflectiveVoxelViews.TryGetValue(view, out var component))
                return null;
            if (!renderVoxelVolumeData.TryGetValue(component, out var data))
                return null;
            return data;
        }
        public static Dictionary<VoxelVolumeComponent, RenderVoxelVolumeData> GetDatas()
        {
            return renderVoxelVolumeData;
        }
        private bool TextureDimensionsEqual(Texture tex, Vector3 dim)
        {
            return (tex.Width == dim.X  &&
                    tex.Height == dim.Y &&
                    tex.Depth == dim.Z  );
        }
        private bool NeedToRecreateTexture(Xenko.Graphics.Texture tex, Vector3 dim)
        {
            if (tex == null || !TextureDimensionsEqual(tex, dim))
            {
                if (tex != null)
                    tex.Dispose();

                return true;
            }
            return false;
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
        public virtual void Collect(RenderContext Context, IShadowMapRenderer ShadowMapRenderer)
        {
            renderVoxelVolumes = Context.VisibilityGroup.Tags.Get(CurrentRenderVoxelVolumes);

            if (renderVoxelVolumes == null)
                return;

            Vector3 resolutionMax = new Vector3(0,0,0);
            int fragmentsMax = 0;
            int fragmentsCountMax = 0;

            //Create per-volume textures
            foreach( var pair in renderVoxelVolumes )
            {
                var volume = pair.Value;
                var bounds = volume.ClipMapMatrix.ScaleVector;

                var resolution = bounds / volume.AproxVoxelSize;

                //Calculate closest power of 2 on each axis
                resolution.X = (float)Math.Pow(2, Math.Round(Math.Log(resolution.X, 2)));
                resolution.Y = (float)Math.Pow(2, Math.Round(Math.Log(resolution.Y, 2)));
                resolution.Z = (float)Math.Pow(2, Math.Round(Math.Log(resolution.Z, 2)));

                resolution = new Vector3(resolution.X, resolution.Z, resolution.Y);//Temporary

                Vector3 ClipMapResolution = new Vector3(resolution.X,resolution.Y,resolution.Z * volume.ClipMapCount);
                Vector3 MipMapResolution  = resolution / 2;

                RenderVoxelVolumeData data;
                if (!renderVoxelVolumeData.TryGetValue(pair.Key, out data))
                    renderVoxelVolumeData.Add(pair.Key, data = new RenderVoxelVolumeData());

                data.ClipMapResolution = resolution;
                if (NeedToRecreateTexture(data.ClipMaps, ClipMapResolution))
                {
                    data.ClipMaps = Xenko.Graphics.Texture.New3D(Context.GraphicsDevice, (int)ClipMapResolution.X, (int)ClipMapResolution.Y, (int)ClipMapResolution.Z, new MipMapCount(false), Xenko.Graphics.PixelFormat.R16G16B16A16_Float, TextureFlags.ShaderResource | TextureFlags.UnorderedAccess);
                }
                if (NeedToRecreateTexture(data.MipMaps, resolution))
                {
                    data.MipMaps = Xenko.Graphics.Texture.New3D(Context.GraphicsDevice, (int)MipMapResolution.X, (int)MipMapResolution.Y, (int)MipMapResolution.Z, new MipMapCount(true), Xenko.Graphics.PixelFormat.R16G16B16A16_Float, TextureFlags.ShaderResource | TextureFlags.UnorderedAccess);
                }


                resolutionMax = Vector3.Min(new Vector3(256), Vector3.Max(resolutionMax, resolution));
                fragmentsMax = Math.Max(fragmentsMax, (int)(ClipMapResolution.X * ClipMapResolution.Y * ClipMapResolution.Z));
                fragmentsCountMax = Math.Max(fragmentsCountMax, (int)(ClipMapResolution.X * ClipMapResolution.Y) * volume.ClipMapCount);
            }

            //Create re-usable textures

            float resolutionMaxSide = Math.Max(Math.Max(resolutionMax.X, resolutionMax.Y), resolutionMax.Z);

            if (NeedToRecreateTexture(MSAARenderTarget, new Vector3(resolutionMaxSide, resolutionMaxSide, 1)))
            {
                MSAARenderTarget = Texture.New(Context.GraphicsDevice, TextureDescription.New2D((int)resolutionMaxSide, (int)resolutionMaxSide, new MipMapCount(false), PixelFormat.R8G8B8A8_UNorm, TextureFlags.RenderTarget, 1, GraphicsResourceUsage.Default, MultisampleCount.X8), null);
            }

            if (NeedToRecreateBuffer(Fragments, fragmentsMax))
            {
                Fragments = Xenko.Graphics.Buffer.Structured.New(Context.GraphicsDevice, fragmentsMax, 24, true);
            }
            if (NeedToRecreateBuffer(FragmentsCounter, fragmentsCountMax))
            {
                FragmentsCounter = Xenko.Graphics.Buffer.Typed.New(Context.GraphicsDevice, fragmentsCountMax, PixelFormat.R32_SInt, true);
            }

            Vector3 MipMapResolutionMax = resolutionMax / 2;
            if (TempMipMaps==null || !TextureDimensionsEqual(TempMipMaps[0], MipMapResolutionMax))
            {
                if (TempMipMaps != null)
                {
                    for (int i = 0; i < TempMipMaps.Length; i++)
                    {
                        TempMipMaps[0].Dispose();
                    }
                }

                int mipCount = 1 + (int)Math.Floor(Math.Log(Math.Max(MipMapResolutionMax.X, Math.Max(MipMapResolutionMax.Y, MipMapResolutionMax.Z)),2));

                TempMipMaps = new Xenko.Graphics.Texture[mipCount];

                for (int i = 0; i < TempMipMaps.Length; i++)
                {
                    TempMipMaps[i] = Xenko.Graphics.Texture.New3D(Context.GraphicsDevice, (int)MipMapResolutionMax.X, (int)MipMapResolutionMax.Y, (int)MipMapResolutionMax.Z, false, Xenko.Graphics.PixelFormat.R16G16B16A16_Float, TextureFlags.ShaderResource | TextureFlags.UnorderedAccess);

                    MipMapResolutionMax /= 2;
                    MipMapResolutionMax = Vector3.Max(new Vector3(1), MipMapResolutionMax);
                }
            }
            if (Generate3DMipmaps == null)
            {
                Generate3DMipmaps = new Xenko.Rendering.ComputeEffect.ComputeEffectShader(Context) { ShaderSourceName = "Generate3DMipmaps" };
                ClearBuffer = new Xenko.Rendering.ComputeEffect.ComputeEffectShader(Context) { ShaderSourceName = "ClearBuffer" };
                ArrangeFragments = new Xenko.Rendering.ComputeEffect.ComputeEffectShader(Context) { ShaderSourceName = "ArrangeFragments" };
            }

            //Create all the views
            reflectiveVoxelViews.Clear();
            foreach (var pair in renderVoxelVolumes)
            {
                var volume = pair.Value;
                var data = renderVoxelVolumeData[pair.Key];
                var bounds = volume.ClipMapMatrix.ScaleVector;

                var shadowRenderView = new RenderView();

                float nearClip = 0.1f;
                float farClip = 999.7f;
                //Currently hard coded and kinda odd
                shadowRenderView.View = Matrix.Translation(0.0f, 1.0f, 0.0f) * Matrix.RotationX(-3.1415f / 2.0f);
                shadowRenderView.Projection = Matrix.OrthoRH(bounds.X * (float)Math.Pow(2, volume.ClipMapCount), bounds.Z * (float)Math.Pow(2, volume.ClipMapCount), nearClip, farClip);
                Matrix.Multiply(ref shadowRenderView.View, ref shadowRenderView.Projection, out shadowRenderView.ViewProjection);
                shadowRenderView.Frustum = new BoundingFrustum(ref shadowRenderView.ViewProjection);
                shadowRenderView.CullingMode = CameraCullingMode.Frustum;
                shadowRenderView.ViewSize = new Vector2(1024, 1024);

                float maxRes = Math.Max(Math.Max(data.ClipMapResolution.X, data.ClipMapResolution.Y), data.ClipMapResolution.Z);

                var rotmat = new Matrix(1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 1);//xzy

                var viewToTexTrans = Matrix.Translation(0.5f, 0.5f, 0.5f);
                var viewToTexScale = Matrix.Scaling(0.5f, 0.5f, 0.5f);
                var viewportAspect = Matrix.Scaling(data.ClipMapResolution / maxRes);

                // Matrix BaseVoxelMatrix = transmat * scalemat * rotmat;
                Matrix BaseVoxelMatrix = volume.ClipMapMatrix * Matrix.Identity;
                BaseVoxelMatrix.Invert();
                BaseVoxelMatrix = BaseVoxelMatrix * Matrix.Scaling(2f, 2f, 2f) * rotmat;

                data.Matrix = BaseVoxelMatrix * viewToTexScale * viewToTexTrans;
                data.ViewportMatrix = BaseVoxelMatrix * viewportAspect;
                data.ClipMapCount = volume.ClipMapCount;

                shadowRenderView.NearClipPlane = nearClip;
                shadowRenderView.FarClipPlane = farClip;
                shadowRenderView.RenderStages.Add(VoxelStage);

                reflectiveVoxelViews.Add(shadowRenderView,pair.Key);

                // Add the render view for the current frame
                Context.RenderSystem.Views.Add(shadowRenderView);

                // Collect objects in shadow views
                Context.VisibilityGroup.TryCollect(shadowRenderView);

                ShadowMapRenderer?.RenderViewsWithShadows.Add(shadowRenderView);
            }
        }
        public virtual void Draw(RenderDrawContext drawContext, IShadowMapRenderer ShadowMapRenderer)
        {
            if (renderVoxelVolumes == null)
                return;
            var context = drawContext;
            var renderSystem = drawContext.RenderContext.RenderSystem;

            using (drawContext.PushRenderTargetsAndRestore())
            {
                // Draw all shadow views generated for the current view
                foreach (var renderView in renderSystem.Views)
                {
                    VoxelVolumeComponent component = null;

                    if (reflectiveVoxelViews.TryGetValue(renderView, out component))
                    {
                        RenderView voxelizeRenderView = renderView;
                        var data = renderVoxelVolumeData[component];

                        if (data.ClipMapResolution.X < 16 || data.ClipMapResolution.Y < 16 || data.ClipMapResolution.Z < 16)
                            continue;

                        //Render Shadow Maps
                        RenderView oldView = drawContext.RenderContext.RenderView;

                        drawContext.RenderContext.RenderView = voxelizeRenderView;
                        ShadowMapRenderer.Draw(drawContext);

                        drawContext.RenderContext.RenderView = oldView;

                        //Render/Collect voxel fragments
                        using (drawContext.QueryManager.BeginProfile(Color.Black, FragmentVoxelizationProfilingKey))
                        {
                            drawContext.CommandList.ResetTargets();
                            drawContext.CommandList.SetRenderTarget(null, MSAARenderTarget);
                            float maxResolution = Math.Max(Math.Max(data.ClipMapResolution.X, data.ClipMapResolution.Y), data.ClipMapResolution.Z);
                            drawContext.CommandList.SetViewport(new Viewport(0, 0, (int)maxResolution, (int)maxResolution));
                            renderSystem.Draw(drawContext, voxelizeRenderView, renderSystem.RenderStages[voxelizeRenderView.RenderStages[0].Index]);
                        }

                        //Fill and write to voxel volume
                        using (drawContext.QueryManager.BeginProfile(Color.Black, ArrangementVoxelizationProfilingKey))
                        {
                            context.CommandList.ClearReadWrite(data.ClipMaps, new Vector4(0.0f));

                            ArrangeFragments.ThreadGroupCounts = new Int3(16, 16, 1);
                            ArrangeFragments.ThreadNumbers = new Int3((int)data.ClipMapResolution.X / ArrangeFragments.ThreadGroupCounts.X, (int)data.ClipMapResolution.Y / ArrangeFragments.ThreadGroupCounts.Y, data.ClipMapCount / ArrangeFragments.ThreadGroupCounts.Z);
                                ArrangeFragments.Parameters.Set(ArrangeFragmentsKeys.VoxelFragmentsCounts, FragmentsCounter);
                                ArrangeFragments.Parameters.Set(ArrangeFragmentsKeys.VoxelFragments, Fragments);
                                ArrangeFragments.Parameters.Set(ArrangeFragmentsKeys.VoxelVolumeW0, data.ClipMaps);
                                ArrangeFragments.Parameters.Set(ArrangeFragmentsKeys.clipMapResolution, data.ClipMapResolution);
                            ((RendererBase)ArrangeFragments).Draw(context);


                            context.CommandList.ClearReadWrite(FragmentsCounter, new Vector4(0));
                        }

                        //Mipmap
                        using (drawContext.QueryManager.BeginProfile(Color.Black, MipmappingVoxelizationProfilingKey))
                        {
                            ClearBuffer.ThreadGroupCounts = new Int3(64, 1, 1);
                            ClearBuffer.ThreadNumbers = new Int3(FragmentsCounter.ElementCount / ClearBuffer.ThreadGroupCounts.X, 1 / ClearBuffer.ThreadGroupCounts.Y, 1 / ClearBuffer.ThreadGroupCounts.Z);
                                ClearBuffer.Parameters.Set(ClearBufferKeys.buffer, FragmentsCounter);
                            ((RendererBase)ClearBuffer).Draw(context);

                            //Mipmap
                            Vector3 resolution = data.ClipMapResolution;
                            Int3 threadGroupCounts = new Int3(8, 8, 8);
                            for(int i = 0; i < TempMipMaps.Length - 1; i ++)
                            {
                                resolution /= 2;
                                if (resolution.X < threadGroupCounts.X|| resolution.Y < threadGroupCounts.Y|| resolution.Z < threadGroupCounts.Z)
                                {
                                    threadGroupCounts /= 4;
                                    if (threadGroupCounts.X < 1) threadGroupCounts.X = 1;
                                    if (threadGroupCounts.Y < 1) threadGroupCounts.Y = 1;
                                    if (threadGroupCounts.Z < 1) threadGroupCounts.Z = 1;
                                }
                                Generate3DMipmaps.ThreadGroupCounts = threadGroupCounts;
                                Generate3DMipmaps.ThreadNumbers = new Int3((int)resolution.X / threadGroupCounts.X, (int)resolution.Y / threadGroupCounts.Y, (int)resolution.Z / threadGroupCounts.Z);

                                if (i == 0)
                                {
                                    Generate3DMipmaps.Parameters.Set(Generate3DMipmapsKeys.VoxelsTexR, data.ClipMaps);
                                }
                                else
                                {
                                    Generate3DMipmaps.Parameters.Set(Generate3DMipmapsKeys.VoxelsTexR, TempMipMaps[i - 1]);
                                }
                                Generate3DMipmaps.Parameters.Set(Generate3DMipmapsKeys.VoxelsTexW, TempMipMaps[i]);
                                ((RendererBase)Generate3DMipmaps).Draw(context);
                                //Don't seem to be able to read and write to the same texture, even if the views
                                //point to different mipmaps.
                                context.CommandList.CopyRegion(TempMipMaps[i], 0, null, data.MipMaps, i);
                            }
                        }
                    }
                }
            }
        }
    }
}
