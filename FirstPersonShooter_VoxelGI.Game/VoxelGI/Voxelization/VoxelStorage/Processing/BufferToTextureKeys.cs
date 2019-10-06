using Xenko.Shaders;

namespace Xenko.Rendering.Voxels
{
    public partial class BufferToTextureKeys
    {
        public static readonly PermutationParameterKey<ShaderSource> writer = ParameterKeys.NewPermutation<ShaderSource>();
        public static readonly PermutationParameterKey<ShaderSourceCollection> AttributesIndirect = ParameterKeys.NewPermutation<ShaderSourceCollection>();
        public static readonly PermutationParameterKey<ShaderSourceCollection> Modifiers = ParameterKeys.NewPermutation<ShaderSourceCollection>();
    }
}
