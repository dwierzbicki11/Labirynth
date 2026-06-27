using System.Runtime.InteropServices;

namespace Core.Math
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Matrix
    {
        public float M11, M12, M13;
        public float M21, M22, M23;
        public float M31, M32, M33;

        public Matrix(float m11, float m12, float m13,
                      float m21, float m22, float m23,
                      float m31, float m32, float m33)
        {
            M11 = m11; M12 = m12; M13 = m13;
            M21 = m21; M22 = m22; M23 = m23;
            M31 = m31; M32 = m32; M33 = m33;
        }

        public static Matrix Identity => new Matrix(1, 0, 0, 0, 1, 0, 0, 0, 1);

        public static Matrix operator *(Matrix a, Matrix b) => new Matrix(
            a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31,
            a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32,
            a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33,

            a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31,
            a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32,
            a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33,

            a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31,
            a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32,
            a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33
        );

        public static Vector3 operator *(Matrix m, Vector3 v) => new Vector3(
            m.M11 * v.X + m.M12 * v.Y + m.M13 * v.Z,
            m.M21 * v.X + m.M22 * v.Y + m.M23 * v.Z,
            m.M31 * v.X + m.M32 * v.Y + m.M33 * v.Z
        );

        public static Matrix RotationX(float angle)
        {
            float c = MathUtils.PreciseCos(angle);
            float s = MathUtils.PreciseSin(angle);
            return new Matrix(1, 0, 0, 0, c, -s, 0, s, c);
        }

        public static Matrix RotationY(float angle)
        {
            float c = MathUtils.PreciseCos(angle);
            float s = MathUtils.PreciseSin(angle);
            return new Matrix(c, 0, s, 0, 1, 0, -s, 0, c);
        }

        public static Matrix RotationZ(float angle)
        {
            float c = MathUtils.PreciseCos(angle);
            float s = MathUtils.PreciseSin(angle);
            return new Matrix(c, -s, 0, s, c, 0, 0, 0, 1);
        }
    }
}