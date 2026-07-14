using System;
using System.Numerics;
using CyberEngine.Core;

namespace CyberEngine.Entities;

public class Honeypot : GameObject
{
    private double _timeAlive = 0;
    private Vector3 _spawnPoint;
    private float _seedOffset;

    public Honeypot(float x, float z)
    {
        _spawnPoint = new Vector3(x, 0.5f, z);
        Transform.Position = _spawnPoint;
        Radius = 0.8f;
        
        // Asynchroniczny offset, aby każda anomalia poruszała się w swoim tempie
        _seedOffset = (MathF.Abs(x) * 100f + MathF.Abs(z) * 100f) % 10.0f;
    }

    public override void Update(double deltaTime)
    {
        _timeAlive += deltaTime;
        
        // Algorytm Lissajous - aktywne skanowanie korytarza bez wchodzenia w ściany
        float t = (float)_timeAlive + _seedOffset;
        float offsetX = MathF.Sin(t * 2.1f) * 0.65f;
        float offsetZ = MathF.Cos(t * 1.3f) * 0.65f;
        
        // Niestabilność w osi Y (agresywny glitch w powietrzu)
        float hoverY = MathF.Sin(t * 20.0f) * 0.15f;
        
        Transform.Position = _spawnPoint + new Vector3(offsetX, hoverY, offsetZ);
    }

    public override void Render(GameEngine engine)
    {
        float dx = Transform.Position.X - engine.CameraPosition.X;
        float dz = Transform.Position.Z - engine.CameraPosition.Z;
        if ((dx * dx + dz * dz) > 400f) return;

        float s = 0.2f;
        Vector3 pos = Transform.Position;

        Vector3 top = pos + new Vector3(0, s, 0);
        Vector3 bot = pos + new Vector3(0, -s, 0);
        Vector3 f = pos + new Vector3(0, 0, s);
        Vector3 b = pos + new Vector3(0, 0, -s);
        Vector3 l = pos + new Vector3(-s, 0, 0);
        Vector3 r = pos + new Vector3(s, 0, 0);

        float matId = 4.0f; // MATERIAL ID 4.0 -> Czerwona Anomalia

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