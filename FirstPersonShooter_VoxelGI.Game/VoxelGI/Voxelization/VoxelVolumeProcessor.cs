using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Engine;
using Xenko.Games;
using Xenko.Extensions;
using Xenko.Core.Mathematics;
using Xenko.Graphics;
using Xenko.Graphics.GeometricPrimitives;
using Xenko.Rendering;
using Xenko.Rendering.Voxels;
using Xenko.Rendering.Shadows;
using Xenko.Rendering.Materials;
using Xenko.Rendering.Materials.ComputeColors;

namespace Xenko.Engine.Processors
{
    public class VoxelVolumeProcessor : EntityProcessor<VoxelVolumeComponent>, IEntityComponentRenderProcessor
    {
        private Dictionary<VoxelVolumeComponent, RenderVoxelVolume> renderVoxelVolumes = new Dictionary<VoxelVolumeComponent, RenderVoxelVolume>();
        bool isDirty;
        SceneSystem sceneSystem;
        GraphicsDevice graphicsDevice;
        CommandList commandList;

        public VisibilityGroup VisibilityGroup { get; set; }
        public RenderGroup RenderGroup { get; set; }
        protected override void OnSystemAdd()
        {
            base.OnSystemAdd();

            VisibilityGroup.Tags.Set(VoxelRenderer.CurrentRenderVoxelVolumes, renderVoxelVolumes);
            sceneSystem = Services.GetService<SceneSystem>();
            graphicsDevice = Services.GetService<IGraphicsDeviceService>().GraphicsDevice;
            commandList = Services.GetService<CommandList>();
        }
        public override void Update(GameTime time)
        {
            RegenerateVoxelVolumes();
        }
        public override void Draw(RenderContext context)
        {

        }

        public RenderVoxelVolume GetRenderVolumeForComponent(VoxelVolumeComponent component)
        {
            if (!renderVoxelVolumes.TryGetValue(component, out var data))
                return null;
            return data;
        }

        protected override void OnEntityComponentAdding(Entity entity, VoxelVolumeComponent component, VoxelVolumeComponent data)
        {
            component.Changed += ComponentChanged;
        }
        protected override void OnEntityComponentRemoved(Entity entity, VoxelVolumeComponent component, VoxelVolumeComponent data)
        {
            component.Changed -= ComponentChanged;
        }
        private void ComponentChanged(object sender, EventArgs eventArgs)
        {
            isDirty = true;
        }
        private void RegenerateVoxelVolumes()
        {
            //if (!isDirty)
            //    return;
            renderVoxelVolumes.Clear();
            foreach (var pair in ComponentDatas)
            {
                if (!pair.Key.Enabled)
                    continue;

                var volume = pair.Key;

                RenderVoxelVolume data;
                if (!renderVoxelVolumes.TryGetValue(volume, out data))
                    renderVoxelVolumes.Add(volume, data = new RenderVoxelVolume());

                data.VoxelMatrix = volume.Entity.Transform.LocalMatrix;
                data.Voxelize = volume.Voxelize;
                data.AproxVoxelSize = volume.AproximateVoxelSize;
                
                data.VisualizeVoxels = volume.VisualizeVoxels;
                data.VoxelVisualization = volume.Visualization;
                data.Attributes = volume.Attributes;
                data.Storage = volume.Storage;
                data.VoxelizationMethod = volume.VoxelizationMethod;
            }


        }
    }
}
