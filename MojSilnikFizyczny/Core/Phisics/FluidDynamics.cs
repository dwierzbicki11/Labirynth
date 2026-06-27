using Core.Math;
using Core.Entities;

namespace Core.Physics
{
    public static class FluidDynamics
    {
        public static Vector3 CalculateFluidForces(State state, float time)
        {
            // Podstawowa grawitacja
            Vector3 totalForce = new Vector3(0, -9.81f, 0);

            // Poziom odniesienia morza
            float waterHeight = 10.0f;

            // =========================================================================
            // ULTRA-CIĘŻKI PROFIL OBLICZENIOWY: 64 SKŁADOWE FAL GERSTNERA (ALU STRESS)
            // =========================================================================
            // Ta pętla generuje potężną intensywność arytmetyczną w rejestrach GPU.
            // Zmusza rdzenie CUDA do ciągłej pracy nad trygonometrią bez czekania na VRAM!
            for (int i = 1; i <= 64; i++)
            {
                // Dynamicznie generujemy unikalne, ostre parametry fal dla każdego przebiegu
                float steepness = 0.05f + (i * 0.002f);
                float wavelength = 3.0f + (i * 0.25f);
                
                // Generujemy dynamiczne kierunki fal przy użyciu trygonometrii szeregów Taylora
                float angle = i * 0.1f;
                Vector3 direction = new Vector3(MathUtils.PreciseCos(angle), 0, MathUtils.PreciseSin(angle));

                // Próbkowanie matematyczne fali
                Vector3 wave = WaveSystem.GetGerstnerWave(state.Position, time, steepness, wavelength, direction);
                
                // Kumulujemy wysokość fal trójwymiarowego oceanu
                waterHeight += wave.Y;
            }

            // Wyliczanie sił zanurzenia
            float submergence = waterHeight - state.Position.Y;
            if (submergence > 0.0f)
            {
                // Siła wyporu Archimedesa
                totalForce.Y += submergence * 35.0f;
                
                // Tłumienie hydroinstalacyjne (Drag)
                totalForce.X -= state.Velocity.X * 0.4f;
                totalForce.Y -= state.Velocity.Y * 0.8f;
                totalForce.Z -= state.Velocity.Z * 0.4f;
            }

            return totalForce;
        }
    }
}