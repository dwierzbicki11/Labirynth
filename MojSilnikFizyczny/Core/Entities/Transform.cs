using System.Runtime.InteropServices;
using Core.Math;

namespace Core.Entities
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Transform
    {
        public Vector3 Position;
        public Matrix Rotation;

        public static Transform Default => new Transform 
        { 
            Position = Vector3.Zero, 
            Rotation = Matrix.Identity 
        };
    }
}