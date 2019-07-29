using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Core;
using Xenko.Core.Mathematics;
using Xenko.Engine;
using Xenko.Engine.Design;
using Xenko.Engine.Processors;

namespace Xenko.Rendering.Voxels
{
    /// <summary>
    /// 
    /// </summary>
    [DataContract("VoxelVolumeComponent")]
    [DefaultEntityComponentRenderer(typeof(VoxelVolumeProcessor))]
    [Display("Voxel Volume")]
    [ComponentCategory("Lights")]
    public class VoxelVolumeComponent : ActivableEntityComponent
    {
        private bool enabled = true;

        public override bool Enabled
        {
            get { return enabled; }
            set { enabled = value; Changed?.Invoke(this, null); }
        }

        [DataMember(10)]
        public int ClipMapCount { get; set; } = 2;

        [DataMember(20)]
        public float AproximateVoxelSize { get; set; } = 0.15f;

        public event EventHandler Changed;
    }
}
