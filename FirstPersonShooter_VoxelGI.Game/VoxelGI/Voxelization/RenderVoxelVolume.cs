using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Core.Mathematics;
using Xenko.Games;
using Xenko.Engine;
using Xenko.Shaders;

namespace Xenko.Rendering.Voxels
{
    public class RenderVoxelVolume
    {
        public Matrix VoxelMatrix;
        public float AproxVoxelSize;
        public bool Voxelize;

        public bool VisualizeVoxels;
        public IVoxelVisualization VoxelVisualization;

        public List<IVoxelAttribute> Attributes;

        public IVoxelStorage Storage;
        public IVoxelizationMethod VoxelizationMethod;
    }
    public class RenderVoxelVolumeData
    {
        public RenderView ReprView;

        public bool Voxelize;
        public bool VisualizeVoxels;
        public IVoxelVisualization VoxelVisualization;

        public IVoxelStorage Storage;
        public IVoxelizationMethod VoxelizationMethod;
        public VoxelStorageContext StorageContext;

        public List<IVoxelAttribute> Attributes;
        //Stage 1 - Voxelization
        public ShaderSourceCollection AttributeTransient;
        public ShaderSourceCollection AttributeDirect;
        public ShaderSourceCollection AttributeIndirect;

        //Stage 2 - Indirect writout
        public ShaderSourceCollection AttributeModifiers;
    }
}
