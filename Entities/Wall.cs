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

        float maxDist = SystemConfig.GraphicsQuality switch { 0 => 256f, 1 => 576f, 2 => 1296f, _ => 576f }; 
        if ((dx * dx + dz * dz) > maxDist) return;

        int wallSeed = (int)(MathF.Abs(Transform.Position.X) * 1337 + MathF.Abs(Transform.Position.Z) * 80085);
        Random blockGen = new Random(wallSeed);

        float height = 4.5f + (float)blockGen.NextDouble() * 1.5f;
        float baseDepth = -1.0f;

        // Materiał 1.0f (Teraz zinterpretowany jako Cyber-Siatka)
        DrawMatCube(engine, Transform.Position.X - 1.0f, baseDepth, Transform.Position.Z - 1.0f, 2.0f, height, 2.0f, 1.0f);

        // ZAGĘSZCZENIE: Zmiana modulo z 14 na 7 (Latarnie pojawiają się 2x częściej)
        if ((int)(MathF.Abs(Transform.Position.X) + MathF.Abs(Transform.Position.Z)) % 7 == 0)
        {
            float offsetX = (wallSeed % 2 == 0) ? 1.0f : -1.0f;
            float offsetZ = 0.0f;
            if (wallSeed % 3 == 0) 
            {
                offsetX = 0.0f;
                offsetZ = (wallSeed % 2 == 0) ? 1.0f : -1.0f;
            }

            float lampCX = Transform.Position.X + (offsetX * 1.4f);
            float lampCZ = Transform.Position.Z + (offsetZ * 1.4f);

            float armWidth = (offsetX != 0) ? 0.8f : 0.1f;
            float armDepth = (offsetZ != 0) ? 0.8f : 0.1f;
            float armX = Transform.Position.X + (offsetX * 1.0f) - (armWidth / 2.0f);
            float armZ = Transform.Position.Z + (offsetZ * 1.0f) - (armDepth / 2.0f);
            
            float metalId = 2.0f;
            
            DrawMatCube(engine, armX, 2.6f, armZ, armWidth, 0.1f, armDepth, metalId);
            
            float wireSize = 0.05f;
            DrawMatCube(engine, lampCX - (wireSize / 2.0f), 2.0f, lampCZ - (wireSize / 2.0f), wireSize, 0.6f, wireSize, metalId);

            float casingSize = 0.35f;
            DrawMatCube(engine, lampCX - (casingSize / 2.0f), 1.8f, lampCZ - (casingSize / 2.0f), casingSize, 0.2f, casingSize, metalId);

            float bulbSize = 0.15f;
            DrawMatCube(engine, lampCX - (bulbSize / 2.0f), 1.65f, lampCZ - (bulbSize / 2.0f), bulbSize, bulbSize, bulbSize, 5.0f);

            engine.RegisterLantern(new Vector3(lampCX, 1.7f, lampCZ));
        }
    }

    private void DrawMatCube(GameEngine engine, float x, float y, float z, float w, float h, float d, float matId)
    {
        Vector3 p0 = new Vector3(x, y, z);
        Vector3 p1 = new Vector3(x + w, y, z);
        Vector3 p2 = new Vector3(x + w, y + h, z);
        Vector3 p3 = new Vector3(x, y + h, z);
        Vector3 p4 = new Vector3(x, y, z + d);
        Vector3 p5 = new Vector3(x + w, y, z + d);
        Vector3 p6 = new Vector3(x + w, y + h, z + d);
        Vector3 p7 = new Vector3(x, y + h, z + d);

        engine.DrawQuad(p4, p5, p6, p7, matId); 
        engine.DrawQuad(p1, p0, p3, p2, matId); 
        engine.DrawQuad(p0, p4, p7, p3, matId); 
        engine.DrawQuad(p5, p1, p2, p6, matId); 
        engine.DrawQuad(p7, p6, p2, p3, matId); 
        engine.DrawQuad(p0, p1, p5, p4, matId); 
    }
}