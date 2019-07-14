// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Xenko.Core;
using Xenko.Core.Annotations;
using Xenko.Core.Collections;
using Xenko.Core.Storage;
using Xenko.Core.Threading;
using Xenko.Graphics;
using Xenko.Rendering.Shadows;
using Xenko.Shaders;
using Xenko.Core.Mathematics;
//using Xenko.Rendering.Lights.ForwardLightingRenderFeature;

namespace Xenko.Rendering.Lights
{
    /// <summary>
    /// Compute lighting shaders and data.
    /// </summary>
    public class ForwardLightingRenderFeatureVXGI : ForwardLightingRenderFeature
    {
        private IReflectiveVoxelRenderer reflectiveVoxelRenderer;

        [DataMember]
        public IReflectiveVoxelRenderer ReflectiveVoxelRenderer {
            get {
                return reflectiveVoxelRenderer;
            }
            set {
                reflectiveVoxelRenderer = value;
            }
        }

        public override void Collect()
        {
            ReflectiveVoxelRenderer?.Collect(Context, ShadowMapRenderer);

            base.Collect();
        }
    }
}
