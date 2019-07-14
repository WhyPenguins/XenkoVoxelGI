// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Xenko.Core;
using Xenko.Core.Mathematics;
using Xenko.Engine;
using Xenko.Rendering.Skyboxes;

namespace Xenko.Rendering.Lights
{
    /// <summary>
    /// A light casting from a voxel representation.
    /// </summary>
    [DataContract("LightVoxel")]
    [Display("Voxel")]
    public class LightVoxel : IEnvironmentLight
    {

        public float BounceIntensityScale { get; set; }

        public bool Update(RenderLight light)
        {
            return true;
        }
    }
}
