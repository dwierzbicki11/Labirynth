using System;
using System.Runtime.InteropServices;

namespace Core.Math
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vector3
    {
        public float X;
        public float Y;
        public float Z;

        public Vector3(float x, float y, float z)
        {
            X = x; Y = y; Z = z;
        }

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3 operator *(Vector3 a, float scalar) => new Vector3(a.X * scalar, a.Y * scalar, a.Z * scalar);
        public static Vector3 operator *(float scalar, Vector3 v) => new Vector3(v.X * scalar, v.Y * scalar, v.Z * scalar);
        public static Vector3 operator /(Vector3 a, float scalar) => new Vector3(a.X / scalar, a.Y / scalar, a.Z / scalar);
        public static Vector3 operator -(Vector3 v) => new Vector3(-v.X, -v.Y, -v.Z);

        public float Length() => MathF.Sqrt(X * X + Y * Y + Z * Z);

        public Vector3 Normalize()
        {
            float length = Length();
            if (length == 0) return new Vector3(0, 0, 0);
            return new Vector3(X / length, Y / length, Z / length);
        }

        public static float Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X
        );

        public static Vector3 Zero => new Vector3(0, 0, 0);
        public override string ToString() => $"[{X:F2}, {Y:F2}, {Z:F2}]";
    }
}