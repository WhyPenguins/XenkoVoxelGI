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
    public class SunControl : SyncScript
    {
        public List<Keys> KeysLeft { get; } = new List<Keys>();

        public List<Keys> KeysRight { get; } = new List<Keys>();

        public List<Keys> KeysUp { get; } = new List<Keys>();

        public List<Keys> KeysDown { get; } = new List<Keys>();

        public SunControl()
        {
            // Fix single frame input lag
            Priority = -1000;
        }

        Vector2 rotationDirection = new Vector2(-0.826f,-2.51f);
        public float speed = 0.02f;
        public override void Update()
        {
            {
                if (KeysLeft.Any(key => Input.IsKeyDown(key)))
                    rotationDirection += -Vector2.UnitX * speed;
                if (KeysRight.Any(key => Input.IsKeyDown(key)))
                    rotationDirection += +Vector2.UnitX * speed;
                if (KeysUp.Any(key => Input.IsKeyDown(key)))
                    rotationDirection += +Vector2.UnitY * speed;
                if (KeysDown.Any(key => Input.IsKeyDown(key)))
                    rotationDirection += -Vector2.UnitY * speed;

                var rotation = Quaternion.RotationYawPitchRoll(rotationDirection.X, rotationDirection.Y, 0);

                Entity.Transform.Rotation = rotation;
            }
        }
    }
}
