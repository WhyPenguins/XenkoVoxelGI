// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System.Collections.Generic;
using System.Linq;
using Xenko.Core.Mathematics;
using Xenko.Engine;
using Xenko.Engine.Events;
using Xenko.Input;
using FirstPersonShooter_VoxelGI.Core;
using Xenko.Rendering;
using Xenko.Rendering.Materials;

namespace FirstPersonShooter_VoxelGI.Player
{
    public class EmissionToggler : SyncScript
    {
        public List<Keys> ToggleEmission { get; } = new List<Keys>();
        public int materialIndex;
        float swapIntensity = 0.0f;
        public override void Update()
        {
            if (ToggleEmission.Any(key => Input.IsKeyPressed(key)))
            {
                float newSwapIntensity = Entity.Get<ModelComponent>().GetMaterial(materialIndex).Passes[0].Parameters.Get(MaterialKeys.EmissiveIntensity);
                Entity.Get<ModelComponent>().GetMaterial(materialIndex).Passes[0].Parameters.Set(MaterialKeys.EmissiveIntensity, swapIntensity);
                swapIntensity = newSwapIntensity;
            }
        }
    }
}
