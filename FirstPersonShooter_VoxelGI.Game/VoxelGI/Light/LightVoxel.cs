// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Xenko.Core;
using Xenko.Core.Mathematics;
using Xenko.Engine;
using Xenko.Rendering.Voxels;

namespace Xenko.Rendering.Lights
{
    /// <summary>
    /// A light casting from a voxel representation.
    /// </summary>
    [DataContract("LightVoxel")]
    [Display("Voxel")]
    public class LightVoxel : IEnvironmentLight
    {
        public VoxelVolumeComponent Volume { get; set; }
        public IVoxelMarchSet DiffuseMarcher { get; set; } = new VoxelMarchSetHemisphere12(new VoxelMarchCone(9, 1.0f, 1.0f));
        public IVoxelMarchMethod SpecularMarcher { get; set; } = new VoxelMarchCone(30, 0.5f, 1.0f);
        public float BounceIntensityScale { get; set; }
        public float SpecularIntensityScale { get; set; }

        public bool Update(RenderLight light)
        {
            return true;
        }
    }
}
