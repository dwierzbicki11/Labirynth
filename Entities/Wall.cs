using System;
using System.Numerics;
using CyberEngine.Core;

namespace CyberEngine.Entities;

public class Wall : GameObject
{
    public Wall(float x, float z, float size)
    {
        Transform.Position = new Vector3(x, 0f, z);
        Radius = size * 0.5f;
    }

    public override void Render(GameEngine engine)
    {
        float dx = Transform.Position.X - engine.CameraPosition.X;
        float dz = Transform.Position.Z - engine.CameraPosition.Z;
        
        // Optymalizacja renderowania na podstawie wybranej jakości grafiki w ustawieniach
        float maxDist = SystemConfig.GraphicsQuality switch { 0 => 256f, 1 => 576f, 2 => 1296f, _ => 576f }; // 16^2, 24^2, 36^2
        if ((dx * dx + dz * dz) > maxDist) return; 

        engine.DrawCube(Transform.Position.X - 1.0f, 0.0f, Transform.Position.Z - 1.0f, 2.0f, 2.0f, 2.0f);

        if ((int)(MathF.Abs(Transform.Position.X) + MathF.Abs(Transform.Position.Z)) % 14 == 0)
        {
            engine.RegisterLantern(new Vector3(Transform.Position.X, 1.0f, Transform.Position.Z));
            engine.DrawCube(Transform.Position.X - 0.15f, 0.9f, Transform.Position.Z - 0.15f, 0.3f, 0.3f, 0.3f);
        }
    }
}
