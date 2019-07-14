using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Core;
using Xenko.Core.Mathematics;
using Xenko.Engine;
using Xenko.Engine.Design;

namespace FirstPersonShooter2.Effects
{
    /// <summary>
    /// This does nothing currently!
    /// </summary>
    [DataContract]
    [Display("Voxel Volume")]
    [ComponentCategory("Lights")]
    public class VoxelVolumeComponent : EntityComponent
    {
        /// <summary>
        /// The size of one edge of the bounding box
        /// </summary>
        [DataMember(0)]
        public Vector3 Size { get; set; } = Vector3.One;
    }
}
