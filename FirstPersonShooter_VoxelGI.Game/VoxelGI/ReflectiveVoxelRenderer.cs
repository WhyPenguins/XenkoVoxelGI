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
namespace Xenko.Rendering.Shadows
{
    [DataContract(DefaultMemberMode = DataMemberMode.Default)]
    public class ReflectiveVoxelRenderer : IReflectiveVoxelRenderer
    {
        public static readonly ProfilingKey FragmentVoxelizationProfilingKey = new ProfilingKey("Voxelization: Fragments");
        public static readonly ProfilingKey ArrangementVoxelizationProfilingKey = new ProfilingKey("Voxelization: Arrangement");
        public static readonly ProfilingKey MipmappingVoxelizationProfilingKey = new ProfilingKey("Voxelization: Mipmapping");
        public RenderStage VoxelStage { get; set; }
        protected FastList<RenderView> reflectiveVoxelViews = new FastList<RenderView>();

        public static int ClipMapCount = 2;
        public static Vector3 ClipMapResolution = new Vector3(128, 128, 64);//Z/Y reversed here for now
        public static Vector3 ClipMapCenter = new Vector3(0, 2.5f, 0);
        public static Vector3 ClipMapBaseSize = new Vector3(20, 10, 20);
        public class ClipMap{
            public Matrix Matrix;
            public Matrix ViewportMatrix;
        };
        public static ClipMap[] clipMaps = null;

        public static Xenko.Graphics.Buffer Fragments = null;
        public static Xenko.Graphics.Buffer FragmentsCounter = null;
        public static Xenko.Graphics.Texture ClipMaps = null;
        public static Xenko.Graphics.Texture MipMaps = null;
        public static Xenko.Graphics.Texture[] MipMapsViews = null;
        public static Xenko.Graphics.Texture[] TempMipMaps = null;
        public static Xenko.Graphics.Texture MSAARenderTarget = null;

        Xenko.Rendering.ComputeEffect.ComputeEffectShader Generate3DMipmaps;
        Xenko.Rendering.ComputeEffect.ComputeEffectShader ClearBuffer;
        Xenko.Rendering.ComputeEffect.ComputeEffectShader ArrangeFragments;
        public virtual void Collect(RenderContext Context, IShadowMapRenderer ShadowMapRenderer)
        {
            float maxResolution = Math.Max(Math.Max(ClipMapResolution.X, ClipMapResolution.Y), ClipMapResolution.Z);
            if (ClipMaps == null)
            {
                Vector3 resolution = ClipMapResolution;
                int size = (int)(resolution.X * resolution.Y * resolution.Z);
                int layersize = (int)(resolution.X * resolution.Y);

                MSAARenderTarget = Texture.New(Context.GraphicsDevice, TextureDescription.New2D((int)maxResolution, (int)maxResolution, new MipMapCount(false), PixelFormat.R8G8B8A8_UNorm, TextureFlags.RenderTarget, 1, GraphicsResourceUsage.Default, MultisampleCount.X8), null);

                Fragments = Xenko.Graphics.Buffer.Structured.New(Context.GraphicsDevice, size * ClipMapCount, 24, true);
                FragmentsCounter = Xenko.Graphics.Buffer.Typed.New(Context.GraphicsDevice, layersize * ClipMapCount, PixelFormat.R32_SInt, true);

                clipMaps = new ClipMap[ClipMapCount];
                TempMipMaps = new Xenko.Graphics.Texture[7];
                MipMapsViews = new Xenko.Graphics.Texture[7];


                ClipMaps = Xenko.Graphics.Texture.New3D(Context.GraphicsDevice, (int)resolution.X, (int)resolution.Y, (int)resolution.Z * ClipMapCount, new MipMapCount(false), Xenko.Graphics.PixelFormat.R16G16B16A16_Float, TextureFlags.ShaderResource | TextureFlags.UnorderedAccess);

                resolution /= 2;
                MipMaps = Xenko.Graphics.Texture.New3D(Context.GraphicsDevice, (int)resolution.X, (int)resolution.Y, (int)resolution.Z, new MipMapCount(true), Xenko.Graphics.PixelFormat.R16G16B16A16_Float, TextureFlags.ShaderResource | TextureFlags.UnorderedAccess);
                for (int i = 0; i < MipMapsViews.Length; i++)
                {
                    MipMapsViews[i] = MipMaps.ToTextureView(ViewType.MipBand, 0, i);
                }

                for (int i = 0; i < TempMipMaps.Length; i++)
                {
                    TempMipMaps[i] = Xenko.Graphics.Texture.New3D(Context.GraphicsDevice, (int)resolution.X, (int)resolution.Y, (int)resolution.Z, false, Xenko.Graphics.PixelFormat.R16G16B16A16_Float, TextureFlags.ShaderResource | TextureFlags.UnorderedAccess);
                    resolution /= 2;
                    if (resolution.X < 1.0f) resolution.X = 1.0f;
                    if (resolution.Y < 1.0f) resolution.Y = 1.0f;
                    if (resolution.Z < 1.0f) resolution.Z = 1.0f;
                }
            }
            if (Generate3DMipmaps == null)
            {
                Generate3DMipmaps = new Xenko.Rendering.ComputeEffect.ComputeEffectShader(Context) { ShaderSourceName = "Generate3DMipmaps" };
                ClearBuffer = new Xenko.Rendering.ComputeEffect.ComputeEffectShader(Context) { ShaderSourceName = "ClearBuffer" };
                ArrangeFragments = new Xenko.Rendering.ComputeEffect.ComputeEffectShader(Context) { ShaderSourceName = "ArrangeFragments" };
            }


            var shadowRenderView = new RenderView();

            float nearClip = 0.1f;
            float farClip = 999.7f;
            //Currently hard coded and kinda odd
            shadowRenderView.View = Matrix.Translation(0.0f, 1.0f, 0.0f) * Matrix.RotationX(-3.1415f / 2.0f);
            shadowRenderView.Projection = Matrix.OrthoRH(ClipMapBaseSize.X*(float)Math.Pow(2,ClipMapCount), ClipMapBaseSize.Z * (float)Math.Pow(2, ClipMapCount), nearClip, farClip);
            Matrix.Multiply(ref shadowRenderView.View, ref shadowRenderView.Projection, out shadowRenderView.ViewProjection);
            shadowRenderView.Frustum = new BoundingFrustum(ref shadowRenderView.ViewProjection);
            shadowRenderView.CullingMode = CameraCullingMode.Frustum;
            shadowRenderView.ViewSize = new Vector2(1024, 1024);



            var transmat = Matrix.Translation(-ClipMapCenter);
            var scalemat = Matrix.Scaling(new Vector3(2f)/ClipMapBaseSize);
            var rotmat = new Matrix(1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 1);//xzy

            var viewToTexTrans = Matrix.Translation(0.5f, 0.5f, 0.5f);
            var viewToTexScale = Matrix.Scaling(0.5f, 0.5f, 0.5f);
            var viewportAspect = Matrix.Scaling(ClipMapResolution / maxResolution);

            Matrix BaseVoxelMatrix = transmat * scalemat * rotmat;

            Vector3 scale = new Vector3(1.0f);

            for (int i = 0; i < ClipMapCount; i++)
            {
                clipMaps[i] = new ClipMap();

                clipMaps[i].Matrix = BaseVoxelMatrix * Matrix.Scaling(scale) * viewToTexScale * viewToTexTrans;
                clipMaps[i].ViewportMatrix = BaseVoxelMatrix * Matrix.Scaling(scale) * viewportAspect;
            }

            shadowRenderView.NearClipPlane = nearClip;
            shadowRenderView.FarClipPlane = farClip;
            shadowRenderView.RenderStages.Add(VoxelStage);

            reflectiveVoxelViews.Add(shadowRenderView);

            // Add the render view for the current frame
            Context.RenderSystem.Views.Add(shadowRenderView);

            // Collect objects in shadow views
            Context.VisibilityGroup.TryCollect(shadowRenderView);

            ShadowMapRenderer?.RenderViewsWithShadows.Add(shadowRenderView);
        }
        public virtual void Draw(RenderDrawContext drawContext, IShadowMapRenderer ShadowMapRenderer)
        {
            var context = drawContext;
            var renderSystem = drawContext.RenderContext.RenderSystem;

            using (drawContext.PushRenderTargetsAndRestore())
            {
                // Draw all shadow views generated for the current view
                foreach (var renderView in renderSystem.Views)
                {
                    RenderView voxelizeRenderView = reflectiveVoxelViews.Contains(renderView) ? renderView : null;

                    if (voxelizeRenderView != null)
                    {
                        reflectiveVoxelViews.Remove(renderView);
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
                            float maxResolution = Math.Max(Math.Max(ClipMapResolution.X, ClipMapResolution.Y), ClipMapResolution.Z);
                            drawContext.CommandList.SetViewport(new Viewport(0, 0, (int)maxResolution, (int)maxResolution));
                            renderSystem.Draw(drawContext, voxelizeRenderView, renderSystem.RenderStages[voxelizeRenderView.RenderStages[0].Index]);
                        }

                        //Fill and write to voxel volume
                        using (drawContext.QueryManager.BeginProfile(Color.Black, ArrangementVoxelizationProfilingKey))
                        {
                            context.CommandList.ClearReadWrite(ClipMaps, new Vector4(0.0f));

                            ArrangeFragments.ThreadGroupCounts = new Int3(16, 16, 1);
                            ArrangeFragments.ThreadNumbers = new Int3(128 / ArrangeFragments.ThreadGroupCounts.X, 128 / ArrangeFragments.ThreadGroupCounts.Y, ClipMapCount / ArrangeFragments.ThreadGroupCounts.Z);
                                ArrangeFragments.Parameters.Set(ArrangeFragmentsKeys.VoxelFragmentsCounts, FragmentsCounter);
                                ArrangeFragments.Parameters.Set(ArrangeFragmentsKeys.VoxelFragments, Fragments);
                                ArrangeFragments.Parameters.Set(ArrangeFragmentsKeys.VoxelVolumeW0, ClipMaps);
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
                            Vector3 resolution = new Vector3(128, 128, 64);
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
                                    Generate3DMipmaps.Parameters.Set(Generate3DMipmapsKeys.VoxelsTexR, ClipMaps);
                                }
                                else
                                {
                                    Generate3DMipmaps.Parameters.Set(Generate3DMipmapsKeys.VoxelsTexR, TempMipMaps[i - 1]);
                                }
                                Generate3DMipmaps.Parameters.Set(Generate3DMipmapsKeys.VoxelsTexW, TempMipMaps[i]);
                                ((RendererBase)Generate3DMipmaps).Draw(context);
                                //Don't seem to be able to read and write to the same texture, even if the views
                                //point to different mipmaps.
                                context.CommandList.CopyRegion(TempMipMaps[i], 0, null, MipMaps, i);
                            }
                        }
                    }
                }
            }
        }
    }
}
