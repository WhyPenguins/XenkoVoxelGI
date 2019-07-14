using Xenko.Engine;

namespace FirstPersonShooter_VoxelGI.Windows
{
    class FirstPersonShooter_VoxelGIApp
    {
        static void Main(string[] args)
        {
            using (var game = new Game())
            {
                game.Run();
            }
        }
    }
}
