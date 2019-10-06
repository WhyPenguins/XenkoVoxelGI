// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xenko.Core;
using Xenko.Core.Mathematics;
using Xenko.Core.Storage;
using Xenko.Core.Threading;
using Xenko.Graphics;
using Xenko.Rendering.Shadows;
using Xenko.Rendering.Voxels;
using Xenko.Shaders;

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
        private StaticObjectPropertyKey<RenderEffect> renderEffectKey;
        private static readonly ShaderClassSource VoxelFragmentWriterFloatR11G11B10Source = new ShaderClassSource("VoxelFragmentWriterFloatR11G11B10");

        protected override void InitializeCore()
        {
            base.InitializeCore();
            renderEffectKey = ((RootEffectRenderFeature)RootRenderFeature).RenderEffectKey;
            VoxelizerStorerCasterKey = ((RootEffectRenderFeature)RootRenderFeature).CreateViewLogicalGroup("VoxelizerStorer");
        }

        public override void PrepareEffectPermutations(RenderDrawContext context)
        {
            var renderEffects = RootRenderFeature.RenderData.GetData(renderEffectKey);
            int effectSlotCount = ((RootEffectRenderFeature)RootRenderFeature).EffectPermutationSlotCount;

            foreach (var data in Voxels.VoxelRenderer.renderVoxelVolumeDataList)
            {
                var view = data.ReprView;
                var viewFeature = view.Features[RootRenderFeature.Index];
                if (data == null)
                    continue;


                data.Storage.UpdateLayout("Storage");
                for (int i  = 0; i < data.Attributes.Count;  i++)
                {
                    var attr = data.Attributes[i];
                    attr.UpdateLayout("AttributesIndirect["+i+"]");
                }

                foreach (var renderObject in view.RenderObjects)
                {
                    var staticObjectNode = renderObject.StaticObjectNode;

                    for (int i = 0; i < effectSlotCount; ++i)
                    {
                        var staticEffectObjectNode = staticObjectNode * effectSlotCount + i;
                        if (staticEffectObjectNode == null)
                            continue;
                        RenderEffect renderEffect = null;
                        try
                        {
                            renderEffect = renderEffects[staticEffectObjectNode];
                        }
                        catch { };

                        // Skip effects not used during this frame
                        if (renderEffect == null || !renderEffect.IsUsedDuringThisFrame(RenderSystem))
                            continue;

                        renderEffect.EffectValidator.ValidateParameter(IsotropicVoxelFragmentKeys.Storage, data.Storage.GetSource(data));
                    }
                }
                
            }
        }
        public override void Prepare(RenderDrawContext context)
        {
            foreach (var data in Voxels.VoxelRenderer.renderVoxelVolumeDataList)
            {
                var view = data.ReprView;
                var viewFeature = view.Features[RootRenderFeature.Index];


                var viewParameters = new ParameterCollection();
                // Find a PerView layout from an effect in normal state
                ViewResourceGroupLayout firstViewLayout = null;
                foreach (var viewLayout in viewFeature.Layouts)
                {
                    // Only process view layouts in normal state
                    if (viewLayout.State != RenderEffectState.Normal)
                        continue;

                    var viewLighting = viewLayout.GetLogicalGroup(VoxelizerStorerCasterKey);
                    if (viewLighting.Hash != ObjectId.Empty)
                    {
                        firstViewLayout = viewLayout;
                        break;
                    }
                }

                // Nothing found for this view (no effects in normal state)
                if (firstViewLayout == null)
                    continue;


                var firstViewLighting = firstViewLayout.GetLogicalGroup(VoxelizerStorerCasterKey);

                // Prepare layout (should be similar for all PerView)
                {

                    // Generate layout
                    var viewParameterLayout = new ParameterCollectionLayout();
                    viewParameterLayout.ProcessLogicalGroup(firstViewLayout, ref firstViewLighting);

                    viewParameters.UpdateLayout(viewParameterLayout);
                }



                ParameterCollection VSViewParameters = viewParameters;

                data.Storage.ApplyWriteParameters(VSViewParameters);
                foreach (var attr in data.Attributes)
                {
                    attr.ApplyWriteParameters(VSViewParameters);
                }

                foreach (var viewLayout in viewFeature.Layouts)
                {


                    if (viewLayout.State != RenderEffectState.Normal)
                        continue;

                    var voxelizerStorer = viewLayout.GetLogicalGroup(VoxelizerStorerCasterKey);
                    if (voxelizerStorer.Hash == ObjectId.Empty)
                        continue;

                    if (voxelizerStorer.Hash != firstViewLighting.Hash)
                        throw new InvalidOperationException("PerView VoxelizerStorer layout differs between different RenderObject in the same RenderView");


                    var resourceGroup = viewLayout.Entries[view.Index].Resources;
                    resourceGroup.UpdateLogicalGroup(ref voxelizerStorer, VSViewParameters);
                }
            }
        }
    }
}
