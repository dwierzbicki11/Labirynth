using ILGPU;
using Core.Math;
using ILGPU.Algorithms;

namespace Core.Physics
{
    public static class WaveSystem
    {
        public static Vector3 GetGerstnerWave(Vector3 position, float time, float steepness, float wavelength, Vector3 direction)
        {
            Vector3 d = direction.Normalize();
            float k = 2.0f * 3.14159265f / wavelength;
            float c = XMath.Sqrt(9.81f / k);
            float dot = d.X * position.X + d.Z * position.Z;
            float phase = k * (dot - c * time);
            float a = steepness / k;

            return new Vector3(
                d.X * (a * MathUtils.PreciseCos(phase)),
                a * MathUtils.PreciseSin(phase),
                d.Z * (a * MathUtils.PreciseCos(phase))
            );
        }
    }
}