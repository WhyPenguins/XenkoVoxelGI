using System;
using System.Collections.Generic;
using System.Text;

using Xenko.Core;
using Xenko.Core.Collections;
using Xenko.Core.Diagnostics;
using Xenko.Core.Mathematics;
using Xenko.Engine;
using Xenko.Shaders;
using Xenko.Graphics;
using Xenko.Rendering.Lights;
using Xenko.Rendering.Voxels;
using Xenko.Core.Extensions;
namespace Xenko.Rendering.Voxels
{
    [DataContract(DefaultMemberMode = DataMemberMode.Default)]
    public class VoxelRenderer : IVoxelRenderer
    {
        [DataMemberIgnore]
        public static readonly PropertyKey<Dictionary<VoxelVolumeComponent, RenderVoxelVolume>> CurrentRenderVoxelVolumes = new PropertyKey<Dictionary<VoxelVolumeComponent, RenderVoxelVolume>>("VoxelRenderer.CurrentRenderVoxelVolumes", typeof(VoxelRenderer));
        private static Dictionary<VoxelVolumeComponent, RenderVoxelVolume> renderVoxelVolumes;
        private static readonly Dictionary<VoxelVolumeComponent, RenderVoxelVolumeData> renderVoxelVolumeData = new Dictionary<VoxelVolumeComponent, RenderVoxelVolumeData>();

        public static readonly List<RenderVoxelVolumeData> renderVoxelVolumeDataList = new List<RenderVoxelVolumeData>();

        public static readonly ProfilingKey FragmentVoxelizationProfilingKey = new ProfilingKey("Voxelization: Fragments");
        public static readonly ProfilingKey ArrangementVoxelizationProfilingKey = new ProfilingKey("Voxelization: Arrangement");
        public static readonly ProfilingKey MipmappingVoxelizationProfilingKey = new ProfilingKey("Voxelization: Mipmapping");

        public RenderStage VoxelStage { get; set; }


        public static RenderVoxelVolumeData GetDataForComponent(VoxelVolumeComponent component)
        {
            if (!renderVoxelVolumeData.TryGetValue(component, out var data))
                return null;
            return data;
        }
        public static Dictionary<VoxelVolumeComponent, RenderVoxelVolumeData> GetDatas()
        {
            return renderVoxelVolumeData;
        }
        public virtual void Collect(RenderContext Context, Shadows.IShadowMapRenderer ShadowMapRenderer)
        {
            renderVoxelVolumes = Context.VisibilityGroup.Tags.Get(CurrentRenderVoxelVolumes);

            if (renderVoxelVolumes == null || renderVoxelVolumes.Count == 0)
                return;

            List<VoxelVolumeComponent> toRemove = new List<VoxelVolumeComponent>();
            foreach (var pair in renderVoxelVolumeData)
            {
                bool used = false;
                foreach (var pair2 in renderVoxelVolumes)
                {
                    if (pair2.Key == pair.Key)
                        used = true;
                }
                if (!used)
                {
                    toRemove.Add(pair.Key);
                }
            }
            foreach (var comp in toRemove)
            {
                renderVoxelVolumeDataList.Remove(renderVoxelVolumeData[comp]);
                renderVoxelVolumeData.Remove(comp);
                renderVoxelVolumes.Remove(comp);
            }
            //Create per-volume textures
            foreach ( var pair in renderVoxelVolumes )
            {
                var volume = pair.Value;
                var bounds = volume.VoxelMatrix.ScaleVector;

                RenderVoxelVolumeData data;
                if (!renderVoxelVolumeData.TryGetValue(pair.Key, out data))
                {
                    data = new RenderVoxelVolumeData();
                    renderVoxelVolumeDataList.Add(data);
                    renderVoxelVolumeData.Add(pair.Key, data);
                }

                VoxelStorageContext storageContext = new VoxelStorageContext
                {
                    device = Context.GraphicsDevice,
                    Extents = bounds,
                    VoxelSize = volume.AproxVoxelSize,
                    Matrix = volume.VoxelMatrix
                };

                volume.Storage.UpdateFromContext(storageContext, data);

                foreach (var attr in volume.Attributes)
                {
                    attr.PrepareLocalStorage(storageContext, volume.Storage);
                }
                volume.Storage.UpdateTempStorage(storageContext);

                ShaderSourceCollection AttributeIndirect = new ShaderSourceCollection();
                ShaderSourceCollection AttributeModifiers = new ShaderSourceCollection();
                foreach (var attr in volume.Attributes)
                {
                    AttributeIndirect.Add(attr.GetShader());
                    attr.AddAttributes(AttributeModifiers);
                }
                if (AttributeModifiers != data.AttributeModifiers)
                {
                    data.AttributeModifiers = AttributeModifiers;
                }

                if (AttributeIndirect != data.AttributeIndirect)
                {
                    data.AttributeIndirect = AttributeIndirect;
                }
                data.VisualizeVoxels = volume.VisualizeVoxels;
                data.Attributes = volume.Attributes;
                data.Storage = volume.Storage;
                data.StorageContext = storageContext;
                data.VoxelizationMethod = volume.VoxelizationMethod;
                data.VoxelVisualization = volume.VoxelVisualization;
                data.Voxelize = volume.Voxelize;

                data.ReprView = volume.VoxelizationMethod.CollectViews(VoxelStage, volume, storageContext, Context);

                ShadowMapRenderer?.RenderViewsWithShadows.Add(data.ReprView);
            }
        }
        public virtual void Draw(RenderDrawContext drawContext, Shadows.IShadowMapRenderer ShadowMapRenderer)
        {
            if (renderVoxelVolumes == null || renderVoxelVolumes.Count == 0)
                return;

            var context = drawContext;

            using (drawContext.PushRenderTargetsAndRestore())
            {
                // Draw all shadow views generated for the current view
                foreach (var data in renderVoxelVolumeDataList)
                {
                    if (!data.Voxelize) continue;

                    RenderView voxelizeRenderView = data.ReprView;

                    //Render Shadow Maps
                    RenderView oldView = drawContext.RenderContext.RenderView;

                    drawContext.RenderContext.RenderView = voxelizeRenderView;
                    ShadowMapRenderer.Draw(drawContext);
                    drawContext.RenderContext.RenderView = oldView;

                    VoxelStorageContext storageContext = data.StorageContext;
                    //Render/Collect voxel fragments
                    using (drawContext.QueryManager.BeginProfile(Color.Black, FragmentVoxelizationProfilingKey))
                    {
                        data.VoxelizationMethod.Render(storageContext, data.Storage, context);
                    }

                    //Fill and write to voxel volume
                    using (drawContext.QueryManager.BeginProfile(Color.Black, ArrangementVoxelizationProfilingKey))
                    {
                        data.Storage.PostProcess(storageContext, context, data);
                    }

                    //Mipmap
                    using (drawContext.QueryManager.BeginProfile(Color.Black, MipmappingVoxelizationProfilingKey))
                    {
                        foreach (var attr in data.Attributes)
                        {
                            attr.PostProcess(context);
                        }
                    }
                }
            }
        }
    }
}
