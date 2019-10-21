// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System.Collections.Generic;
using System.Linq;
using Xenko.Core.Mathematics;
using Xenko.Engine;
using Xenko.Engine.Events;
using Xenko.Input;
using FirstPersonShooter_VoxelGI.Core;

namespace FirstPersonShooter_VoxelGI.Player
{
    public class LightToggler : SyncScript
    {
        public List<Keys> ToggleLight { get; } = new List<Keys>();

        public override void Update()
        {
            if (ToggleLight.Any(key => Input.IsKeyPressed(key)))
                Entity.Get<LightComponent>().Enabled = !Entity.Get<LightComponent>().Enabled;
        }
    }
}
