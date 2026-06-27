using System.Runtime.InteropServices;
using Core.Math;

namespace Core.Entities
{
    [StructLayout(LayoutKind.Sequential)]
    public struct State 
    {
        public Vector3 Position;
        public Vector3 Velocity;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Derivative 
    {
        public Vector3 Velocity;
        public Vector3 Acceleration;
    }
}