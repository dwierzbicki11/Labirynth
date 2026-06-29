using Veldrid;
using System;
using System.Numerics;

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
        
        // Optymalizacja: Używamy dystansu do kwadratu (MathF.Sqrt zabija CPU na ARM)
        if ((dx * dx + dz * dz) > 900f) return; 

        engine.DrawCube(Transform.Position.X - 1.0f, 0.0f, Transform.Position.Z - 1.0f, 2.0f, 2.0f, 2.0f);

        if ((int)(MathF.Abs(Transform.Position.X) + MathF.Abs(Transform.Position.Z)) % 14 == 0)
        {
            engine.RegisterLantern(new Vector3(Transform.Position.X, 1.0f, Transform.Position.Z));
            engine.DrawCube(Transform.Position.X - 0.15f, 0.9f, Transform.Position.Z - 0.15f, 0.3f, 0.3f, 0.3f);
        }
    }
}

