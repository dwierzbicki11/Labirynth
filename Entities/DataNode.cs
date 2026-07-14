using System;
using System.Numerics;
using CyberEngine.Core;

namespace CyberEngine.Entities;

public class DataNode : GameObject
{
    private double _timeAlive = 0; // Zmieniono na double, aby pasowało do pętli silnika

    public DataNode(float x, float z)
    {
        Transform.Position = new Vector3(x, 0.4f, z);
        Radius = 1.2f; 
    }

    // Prawidłowe nadpisanie metody z użyciem parametru double
    public override void Update(double deltaTime) 
    {
        _timeAlive += deltaTime; 
    }

    public override void Render(GameEngine engine)
    {
        float dx = Transform.Position.X - engine.CameraPosition.X;
        float dz = Transform.Position.Z - engine.CameraPosition.Z;
        if ((dx * dx + dz * dz) > 400f) return;

        float s = 0.15f; 
        
        // Rzutowanie (float) niezbędne dla biblioteki matematycznej MathF
        float hoverY = Transform.Position.Y + MathF.Sin((float)_timeAlive * 3.0f) * 0.05f;
        Vector3 pos = new Vector3(Transform.Position.X, hoverY, Transform.Position.Z);

        Vector3 top = pos + new Vector3(0, s, 0);
        Vector3 bot = pos + new Vector3(0, -s, 0);
        Vector3 f = pos + new Vector3(0, 0, s);
        Vector3 b = pos + new Vector3(0, 0, -s);
        Vector3 l = pos + new Vector3(-s, 0, 0);
        Vector3 r = pos + new Vector3(s, 0, 0);

        float matId = 3.0f; // ID 3.0 -> Złoty Pakiet Danych

        engine.DrawQuad(f, r, top, top, matId);
        engine.DrawQuad(r, b, top, top, matId);
        engine.DrawQuad(b, l, top, top, matId);
        engine.DrawQuad(l, f, top, top, matId);
        
        engine.DrawQuad(r, f, bot, bot, matId);
        engine.DrawQuad(b, r, bot, bot, matId);
        engine.DrawQuad(l, b, bot, bot, matId);
        engine.DrawQuad(f, l, bot, bot, matId);
    }
}