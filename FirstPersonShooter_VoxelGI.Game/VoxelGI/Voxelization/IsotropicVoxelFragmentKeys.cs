using System.Collections.Generic;
using Xenko.Rendering.Voxels;
using Xenko.Shaders;

namespace Xenko.Rendering.Voxels
{
    /// <summary>
    /// Keys used by <see cref="ToneMap"/> and ToneMapEffect xkfx
    /// </summary>
    public static partial class IsotropicVoxelFragmentKeys
    {
        public static readonly PermutationParameterKey<ShaderSource> Storage = ParameterKeys.NewPermutation<ShaderSource>();
    }
}