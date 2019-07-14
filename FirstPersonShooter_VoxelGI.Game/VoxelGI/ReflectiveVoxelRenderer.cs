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


        public static Xenko.Graphics.Buffer VoxelFragments = null;
        public static Xenko.Graphics.Buffer VoxelFragmentsCounter = null;

        public static Xenko.Graphics.Buffer Voxels = null;
        public static Xenko.Graphics.Texture[] VoxelsTex = null;

        public static Matrix VoxelMatrix;
        public static Matrix VoxelViewportMatrix;

        Xenko.Rendering.ComputeEffect.ComputeEffectShader Generate3DMipmaps;
        Xenko.Rendering.ComputeEffect.ComputeEffectShader ClearBuffer;
        Xenko.Rendering.ComputeEffect.ComputeEffectShader ArrangeFragments;
        public virtual void Collect(RenderContext Context, IShadowMapRenderer ShadowMapRenderer)
        {
            if (Voxels == null)
            {
                Voxels = Xenko.Graphics.Buffer.Structured.New(Context.GraphicsDevice, 100, 28, true);
                VoxelFragments = Xenko.Graphics.Buffer.Structured.New(Context.GraphicsDevice, 128 * 128 * 64, 24, true);
                VoxelFragmentsCounter = Xenko.Graphics.Buffer.Typed.New(Context.GraphicsDevice, 128 * 128, PixelFormat.R32_SInt, true);
                VoxelsTex = new Xenko.Graphics.Texture[7];
                Vector3 resolution = new Vector3(128,128,64);
                for (int i = 0; i < VoxelsTex.Length; i++)
                {
                    VoxelsTex[i] = Xenko.Graphics.Texture.New3D(Context.GraphicsDevice, (int)resolution.X, (int)resolution.Y, (int)resolution.Z, false, Xenko.Graphics.PixelFormat.R16G16B16A16_Float, TextureFlags.ShaderResource | TextureFlags.UnorderedAccess | TextureFlags.RenderTarget);
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
            shadowRenderView.Projection = Matrix.OrthoRH(20, 20, nearClip, farClip);
            Matrix.Multiply(ref shadowRenderView.View, ref shadowRenderView.Projection, out shadowRenderView.ViewProjection);
            shadowRenderView.Frustum = new BoundingFrustum(ref shadowRenderView.ViewProjection);
            shadowRenderView.CullingMode = CameraCullingMode.Frustum;
            shadowRenderView.ViewSize = new Vector2(1024, 1024);

            float ResX = 128;
            float ResY = 128;
            float ResZ = 64;

            float maxRes = Math.Max(Math.Max(ResX,ResY),ResZ);

            var transmat = Matrix.Translation(0.0f, 2.5f-5.0f, 0.0f);
            var scalemat = Matrix.Scaling(new Vector3(0.1f, 0.2f, 0.1f));
            var rotmat = new Matrix(1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 1);

            var viewporttrans = Matrix.Translation(0.5f, 0.5f, 0.5f);
            var viewportscale = Matrix.Scaling(0.5f, 0.5f, 0.5f);
            VoxelMatrix = transmat * scalemat * rotmat * viewportscale * viewporttrans;

            var viewportsquish = Matrix.Scaling(ResX / maxRes, ResY / maxRes, ResZ / maxRes);
            VoxelViewportMatrix = transmat * scalemat * rotmat * viewportsquish;// the naming is probably a bit backwards

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
                            drawContext.CommandList.SetViewport(new Viewport(0, 0, 128, 128));
                            renderSystem.Draw(drawContext, voxelizeRenderView, renderSystem.RenderStages[voxelizeRenderView.RenderStages[0].Index]);
                        }

                        //Fill and write to voxel volume
                        using (drawContext.QueryManager.BeginProfile(Color.Black, ArrangementVoxelizationProfilingKey))
                        {
                            context.CommandList.ClearReadWrite(VoxelsTex[0], new Vector4(0.0f));

                            ArrangeFragments.ThreadGroupCounts = new Int3(16, 16, 1);
                            ArrangeFragments.ThreadNumbers = new Int3(128 / ArrangeFragments.ThreadGroupCounts.X, 128 / ArrangeFragments.ThreadGroupCounts.Y, 1 / ArrangeFragments.ThreadGroupCounts.Z);
                                ArrangeFragments.Parameters.Set(ArrangeFragmentsKeys.VoxelFragmentsCounts, VoxelFragmentsCounter);
                                ArrangeFragments.Parameters.Set(ArrangeFragmentsKeys.VoxelFragments, VoxelFragments);
                                ArrangeFragments.Parameters.Set(ArrangeFragmentsKeys.VoxelVolumeW0, VoxelsTex[0]);
                            ((RendererBase)ArrangeFragments).Draw(context);


                            context.CommandList.ClearReadWrite(VoxelFragmentsCounter, new Vector4(0));
                        }

                        //Mipmap
                        using (drawContext.QueryManager.BeginProfile(Color.Black, MipmappingVoxelizationProfilingKey))
                        {
                            ClearBuffer.ThreadGroupCounts = new Int3(32, 1, 1);
                            ClearBuffer.ThreadNumbers = new Int3(VoxelFragmentsCounter.ElementCount / ClearBuffer.ThreadGroupCounts.X, 1 / ClearBuffer.ThreadGroupCounts.Y, 1 / ClearBuffer.ThreadGroupCounts.Z);
                                ClearBuffer.Parameters.Set(ClearBufferKeys.buffer, VoxelFragmentsCounter);
                            ((RendererBase)ClearBuffer).Draw(context);

                            //Mipmap
                            Vector3 resolution = new Vector3(128, 128, 64);
                            Int3 threadGroupCounts = new Int3(8, 8, 8);
                            for(int i = 0; i < VoxelsTex.Length - 1; i ++)
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
                                Generate3DMipmaps.Parameters.Set(Generate3DMipmapsKeys.VoxelsTexR, VoxelsTex[i]);
                                Generate3DMipmaps.Parameters.Set(Generate3DMipmapsKeys.VoxelsTexW, VoxelsTex[i + 1]);
                                ((RendererBase)Generate3DMipmaps).Draw(context);
                            }
                        }
                    }
                }
            }
        }
    }
}
