using System;
using System.Collections.Generic;
using System.Text;

using Xenko.Core;
using Xenko.Core.Collections;
using Xenko.Core.Diagnostics;
using Xenko.Core.Mathematics;
using Xenko.Engine;
using Xenko.Shaders;
using Xenko.Graphics;
using Xenko.Rendering.Lights;
using Xenko.Rendering.Voxels;
using Xenko.Core.Extensions;
using Xenko.Rendering;


//Uses a geometry shader to project each triangle to the axis
//of maximum coverage, and lets the pipeline generate fragments from there
[DataContract(DefaultMemberMode = DataMemberMode.Default)]
[Display("Geometry Shader")]
class VoxelizationMethodGeometry : IVoxelizationMethod
{
    [DataMemberIgnore]
    public RenderView voxelizationView { get; } = new RenderView();

    public RenderView CollectViews(RenderStage stage, RenderVoxelVolume volume, VoxelStorageContext storageContext, RenderContext RenderContext)
    {
        Matrix BaseVoxelMatrix = volume.VoxelMatrix * Matrix.Identity;
        BaseVoxelMatrix.Invert();
        BaseVoxelMatrix = BaseVoxelMatrix * Matrix.Scaling(2f, 2f, 2f);

        voxelizationView.View = Matrix.Identity;
        voxelizationView.Projection = BaseVoxelMatrix;
        voxelizationView.ViewProjection = voxelizationView.Projection;

        voxelizationView.Frustum = new BoundingFrustum(ref voxelizationView.ViewProjection);
        voxelizationView.CullingMode = CameraCullingMode.None;
        voxelizationView.NearClipPlane = 0.1f;
        voxelizationView.FarClipPlane = 1000.0f;
        voxelizationView.RenderStages.Add(stage);

        return volume.Storage.CollectViews(storageContext, RenderContext, voxelizationView);
    }

    Xenko.Graphics.Texture MSAARenderTarget = null;

    private bool TextureDimensionsEqual(Texture tex, Vector3 dim)
    {
        return (tex.Width == dim.X &&
                tex.Height == dim.Y &&
                tex.Depth == dim.Z);
    }
    private bool NeedToRecreateTexture(Xenko.Graphics.Texture tex, Vector3 dim, Xenko.Graphics.PixelFormat pixelFormat, MultisampleCount samples)
    {
        if (tex == null || !TextureDimensionsEqual(tex, dim) || tex.Format != pixelFormat || tex.MultisampleCount != samples)
        {
            if (tex != null)
                tex.Dispose();

            return true;
        }
        return false;
    }
    public void Render(VoxelStorageContext storageContext, IVoxelStorage Storage, RenderDrawContext drawContext)
    {
        if (NeedToRecreateTexture(MSAARenderTarget, new Vector3(voxelizationView.ViewSize.X, voxelizationView.ViewSize.Y, 1), PixelFormat.R8G8B8A8_UNorm, MultisampleCount.X8))
        {
            MSAARenderTarget = Texture.New(storageContext.device, TextureDescription.New2D((int)voxelizationView.ViewSize.X, (int)voxelizationView.ViewSize.Y, new MipMapCount(false), PixelFormat.R8G8B8A8_UNorm, TextureFlags.RenderTarget, 1, GraphicsResourceUsage.Default, MultisampleCount.X8), null);
        }
        drawContext.CommandList.ResetTargets();
        if (MSAARenderTarget!=null)
            drawContext.CommandList.SetRenderTarget(null, MSAARenderTarget);

        Storage.Render(storageContext, drawContext, voxelizationView);
    }
}
