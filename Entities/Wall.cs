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
        float dx = Transform.Position.X - engine.CameraPosition.X;
        float dz = Transform.Position.Z - engine.CameraPosition.Z;
        if (MathF.Sqrt(dx * dx + dz * dz) > 30f) return;

        // Rysujemy wysoki sześcian ściany. Silnik automatycznie nałoży na niego teksturę wall_texture.png
        engine.DrawCube(
            Transform.Position.X - 1.0f, 
            0.0f, 
            Transform.Position.Z - 1.0f, 
            2.0f, 2.0f, 2.0f
        );
    }
}