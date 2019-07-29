using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Games;
using Xenko.Core.Mathematics;
using Xenko.Rendering;
using Xenko.Rendering.Voxels;
using Xenko.Rendering.Shadows;

namespace Xenko.Engine.Processors
{
    public class VoxelVolumeProcessor : EntityProcessor<VoxelVolumeComponent>, IEntityComponentRenderProcessor
    {
        private Dictionary<VoxelVolumeComponent, RenderVoxelVolume> renderVoxelVolumes = new Dictionary<VoxelVolumeComponent, RenderVoxelVolume>();
        bool isDirty;

        public VisibilityGroup VisibilityGroup { get; set; }
        protected override void OnSystemAdd()
        {
            base.OnSystemAdd();

            VisibilityGroup.Tags.Set(ReflectiveVoxelRenderer.CurrentRenderVoxelVolumes, renderVoxelVolumes);
        }
        public override void Update(GameTime time)
        {
            RegenerateVoxelVolumes();
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

                data.ClipMapMatrix = volume.Entity.Transform.LocalMatrix;
                data.ClipMapCount = volume.ClipMapCount;
                data.AproxVoxelSize = volume.AproximateVoxelSize;
            }


        }
    }
}
