using ILGPU;
using ILGPU.Algorithms;

namespace Core.Math
{
    public static class MathUtils
    {
        public static float PreciseSin(float x)
        {
            float twoPi = 2.0f * 3.14159265f;
            x = x - twoPi * XMath.Floor((x + 3.14159265f) / twoPi);
            float x2 = x * x;
            return x * (1.0f - x2 / 6.0f * (1.0f - x2 / 20.0f * (1.0f - x2 / 42.0f)));
        }

        public static float PreciseCos(float x)
        {
            float twoPi = 2.0f * 3.14159265f;
            x = x - twoPi * XMath.Floor((x + 3.14159265f) / twoPi);
            float x2 = x * x;
            return 1.0f - x2 / 2.0f * (1.0f - x2 / 12.0f * (1.0f - x2 / 30.0f));
        }
    }
}