using Veldrid;
using System;

namespace CyberEngine.Entities;

public class Wall : GameObject
{
    public Wall(float x, float z, float size)
    {
        Transform.Position = new Core.Math.Vector3(x, 0f, z);
        Radius = size * 0.5f;
    }

    public override void Render(GameEngine engine)
    {
        // Dynamiczny Culling: jeśli ściana leży dalej niż 30 jednostek od kamery, nie wrzucaj jej do GPU
        float dx = Transform.Position.X - engine.CameraPosition.X;
        float dz = Transform.Position.Z - engine.CameraPosition.Z;
        if (MathF.Sqrt(dx * dx + dz * dz) > 30f) return;

        // Rysujemy przestrzenny, wysoki szary blok (szerokość 2, wysokość 2, głębokość 2)
        engine.DrawCube(
            Transform.Position.X - 1.0f, 
            0.0f, 
            Transform.Position.Z - 1.0f, 
            2.0f, 2.0f, 2.0f, 
            new RgbaFloat(0.22f, 0.22f, 0.25f, 1.0f)
        );
    }
}