using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Core.Mathematics;

namespace Xenko.Rendering.Voxels
{
    public class RenderVoxelVolume
    {
        public Matrix ClipMapMatrix;
        public int ClipMapCount;
        public float AproxVoxelSize;
    }
    public class RenderVoxelVolumeData
    {
        public Xenko.Graphics.Texture ClipMaps = null;
        public Xenko.Graphics.Texture MipMaps = null;
        public Matrix Matrix;
        public Matrix ViewportMatrix;
        public int ClipMapCount;
        public Vector3 ClipMapResolution;
    }
}
