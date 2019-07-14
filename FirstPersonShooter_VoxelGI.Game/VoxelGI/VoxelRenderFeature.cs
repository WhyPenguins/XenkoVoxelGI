// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xenko.Core;
using Xenko.Core.Mathematics;
using Xenko.Core.Storage;
using Xenko.Graphics;
using Xenko.Rendering.Shadows;

namespace Xenko.Rendering
{
    /// <summary>
    /// A render feature that computes and upload info for voxelization
    /// </summary>
    [DataContract(DefaultMemberMode = DataMemberMode.Never)]
    public class VoxelRenderFeature : SubRenderFeature
    {
        [DataMember]
        public RenderStage VoxelizerRenderStage { get; set; }
        
        private LogicalGroupReference VoxelizerStorerCasterKey;

        protected override void InitializeCore()
        {
            base.InitializeCore();
            VoxelizerStorerCasterKey = ((RootEffectRenderFeature)RootRenderFeature).CreateViewLogicalGroup("VoxelizerStorer");
        }
        public override void Prepare(RenderDrawContext context)
        {
            for (int index = 0; index < RenderSystem.Views.Count; index++)
            {
                var view = RenderSystem.Views[index];
                var viewFeature = view.Features[RootRenderFeature.Index];
                foreach (var viewLayout in viewFeature.Layouts)
                {
                    var voxelizerStorer = viewLayout.GetLogicalGroup(VoxelizerStorerCasterKey);

                    ParameterCollection VSViewParameters = new ParameterCollection();
                    ParameterCollection ViewParameters = new ParameterCollection();

                    VSViewParameters.Set(IsotropicVoxelFragmentKeys.VoxelVolumeW0, ReflectiveVoxelRenderer.VoxelsTex[0]);
                    VSViewParameters.Set(IsotropicVoxelFragmentKeys.VoxelFragments, ReflectiveVoxelRenderer.VoxelFragments);
                    VSViewParameters.Set(IsotropicVoxelFragmentKeys.VoxelFragmentsCounter, ReflectiveVoxelRenderer.VoxelFragmentsCounter);
                    VSViewParameters.Set(IsotropicVoxelFragmentKeys.VoxelMatrix, ReflectiveVoxelRenderer.VoxelMatrix);
                    VSViewParameters.Set(IsotropicVoxelFragmentKeys.VoxelMatrixViewport, ReflectiveVoxelRenderer.VoxelViewportMatrix);

                    var resourceGroup = viewLayout.Entries[view.Index].Resources;
                    resourceGroup.UpdateLogicalGroup(ref voxelizerStorer, VSViewParameters);
                }
            }
        }
    }
}
