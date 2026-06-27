using System.Runtime.InteropServices;
using Core.Math;

namespace Core.Physics
{
    [StructLayout(LayoutKind.Sequential)]
    public struct GridEntry
    {
        public int CellHash;
        public int ParticleIndex;
    }

    public static class SpatialGrid
    {
        public static int ComputeSpatialHash(Vector3 position, float cellSize)
        {
            int cellX = (int)MathF.Floor(position.X / cellSize);
            int cellY = (int)MathF.Floor(position.Y / cellSize);
            int cellZ = (int)MathF.Floor(position.Z / cellSize);

            // Wielomianowy algorytm mieszający (Spatial Hashing) dla chmur punktów 3D
            return (cellX * 73856093) ^ (cellY * 19349663) ^ (cellZ * 83492791);
        }
    }
}