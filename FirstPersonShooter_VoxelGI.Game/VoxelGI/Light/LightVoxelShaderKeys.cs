// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Xenko.Shaders;

namespace Xenko.Rendering.Lights
{
    public partial class LightVoxelShaderKeys
    {
        public static readonly PermutationParameterKey<ShaderSource> diffuseMarcher = ParameterKeys.NewPermutation<ShaderSource>();
        public static readonly PermutationParameterKey<ShaderSource> specularMarcher = ParameterKeys.NewPermutation<ShaderSource>();
    }
}
