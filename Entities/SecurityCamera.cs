using System;
using System.Collections.Generic;
using System.Numerics;
using CyberEngine.Core;
using CyberEngine.Logic;

namespace CyberEngine.Entities;

public class SecurityCamera : GameObject
{
    public float Yaw { get; set; }
    public float Pitch { get; set; }
    public float BaseYaw { get; private set; }
    
    public float FieldOfView { get; set; } = 1.047f; // ~60 stopni w radianach
    public float DetectionRange { get; set; } = 15.0f;
    public bool IsAlerted { get; private set; } = false;

    private float _timeActive = 0f;
    private readonly float _sweepAngle = 0.785f; // ~45 stopni wychylenia
    private readonly float _sweepSpeed = 1.5f;

    private Func<List<GameObject>> _objectsProvider;
    private Player _target;
    private GodDirector _director;
    public SecurityCamera(Vector3 position, float baseYaw, Player target, Func<List<GameObject>> objectsProvider,GodDirector director)
    {
        Transform = new Transform { Position = position };
        BaseYaw = baseYaw;
        Yaw = baseYaw;
        Pitch = -0.3f; 
        Radius = 0.3f; 

        _director = director;
        _target = target;
        _objectsProvider = objectsProvider;
    }

    public override void Update(double deltaTime)
    {
        float dt = (float)deltaTime;
        _timeActive += dt;

        if (!IsAlerted)
        {
            Yaw = BaseYaw + MathF.Sin(_timeActive * _sweepSpeed) * _sweepAngle;
        }

        Vector3 forward = new Vector3(MathF.Sin(Yaw) * MathF.Cos(Pitch), MathF.Sin(Pitch), -MathF.Cos(Yaw) * MathF.Cos(Pitch));
        Vector3 vecToTarget = _target.Transform.Position - Transform.Position;
        float distSq = vecToTarget.LengthSquared();

        if (distSq < DetectionRange * DetectionRange)
        {
            float dist = MathF.Sqrt(distSq);
            Vector3 dirToTarget = vecToTarget / dist; 

            float dotProduct = Vector3.Dot(forward, dirToTarget);
            if (dotProduct > MathF.Cos(FieldOfView / 2f))
            {
                if (CheckLineOfSight(dirToTarget, dist, _objectsProvider()))
                {
                    TriggerAlert(dirToTarget);
                }
                else
                {
                    IsAlerted = false;
                }
            }
            else
            {
                IsAlerted = false;
            }
        }
        else
        {
            IsAlerted = false;
        }
    }

    private bool CheckLineOfSight(Vector3 dirToTarget, float distance, List<GameObject> objs)
    {
        for (float d = 0.5f; d < distance; d += 0.5f)
        {
            Vector3 point = Transform.Position + dirToTarget * d;
            
            foreach (var obj in objs)
            {
                if (obj is Wall w)
                {
                    float dx = MathF.Abs(point.X - w.Transform.Position.X);
                    float dz = MathF.Abs(point.Z - w.Transform.Position.Z);
                    if (dx < 1.0f && dz < 1.0f) return false; 
                }
            }
        }
        return true; 
    }

    private void TriggerAlert(Vector3 dirToTarget)
    {
        IsAlerted = true;
        Yaw = MathF.Atan2(dirToTarget.X, -dirToTarget.Z);
        _director.ReportIntrusion(Transform.Position, _target.Transform.Position);
    }

    public override void Render(GameEngine engine)
    {
        // 1. UCHWYT ŚCIENNY (Statyczny)
        // Obliczamy wektor wskazujący na ścianę w oparciu o BaseYaw
        Vector3 wallOffset = new Vector3(MathF.Sin(BaseYaw), 0, -MathF.Cos(BaseYaw)) * -0.35f;
        Vector3 basePos = Transform.Position + wallOffset;
        
        // Płaski panel przylegający do ściany
        engine.DrawCube(basePos.X - 0.15f, basePos.Y + 0.05f, basePos.Z - 0.15f, 0.3f, 0.3f, 0.3f);
        
        // Przegub kulowy / ramię łączące z osią obrotu
        engine.DrawCube(Transform.Position.X - 0.05f, Transform.Position.Y + 0.1f, Transform.Position.Z - 0.05f, 0.1f, 0.2f, 0.1f);

        // 2. KORPUS KAMERY (Zorientowany wektorowo)
        float bodyWidth = 0.12f;
        float bodyHeight = 0.15f;
        float bodyLength = 0.35f;
        float baseMaterial = 1.0f; // Szary/Metaliczny materiał w Twoim silniku

        DrawOrientedBox(engine, Transform.Position, Yaw, Pitch, bodyWidth, bodyHeight, bodyLength, baseMaterial);

        // 3. OBIEKTYW / SENSOR LASEROWY
        // Wektor Forward używany do wysunięcia soczewki na przód korpusu
        Vector3 forward = new Vector3(MathF.Sin(Yaw) * MathF.Cos(Pitch), MathF.Sin(Pitch), -MathF.Cos(Yaw) * MathF.Cos(Pitch));
        Vector3 lensPos = Transform.Position + forward * ((bodyLength / 2f) + 0.02f); 
        
        // Zmiana ID Materiału na czerwony (np. 3.0f) w przypadku alarmu
        float lensMaterial = IsAlerted ? 3.0f : 2.0f; 
        
        DrawOrientedBox(engine, lensPos, Yaw, Pitch, 0.08f, 0.08f, 0.05f, lensMaterial);
    }

    // SILNIK RENDEROWANIA GEOMETRII ZORIENTOWANEJ W PRZESTRZENI
    // Zastępuje AABB (DrawCube) precyzyjną siatką poligonów, wyliczaną w locie.
    private void DrawOrientedBox(GameEngine engine, Vector3 center, float yaw, float pitch, float width, float height, float length, float materialId)
    {
        // 1. Kalkulacja układu odniesienia (Macierz rotacji w postaci wektorów)
        Vector3 forward = new Vector3(MathF.Sin(yaw) * MathF.Cos(pitch), MathF.Sin(pitch), -MathF.Cos(yaw) * MathF.Cos(pitch));
        
        // Iloczyn wektorowy wyznacza wektor "w prawo" względem kamery
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        if (right.LengthSquared() < 0.001f) right = Vector3.UnitX; // Zabezpieczenie przed Gimbal Lock
        
        // Wektor "w górę"
        Vector3 up = Vector3.Normalize(Vector3.Cross(right, forward));

        // 2. Skalowanie wektorów bazowych do rozmiarów prostopadłościanu
        Vector3 hw = right * (width / 2f);
        Vector3 hh = up * (height / 2f);
        Vector3 hl = forward * (length / 2f);

        // 3. Obliczenie pozycji w przestrzeni 8 wierzchołków
        Vector3 ftl = center + hl - hw + hh; // Front Top Left
        Vector3 ftr = center + hl + hw + hh; // Front Top Right
        Vector3 fbl = center + hl - hw - hh; // Front Bottom Left
        Vector3 fbr = center + hl + hw - hh; // Front Bottom Right

        Vector3 btl = center - hl - hw + hh; // Back Top Left
        Vector3 btr = center - hl + hw + hh; // Back Top Right
        Vector3 bbl = center - hl - hw - hh; // Back Bottom Left
        Vector3 bbr = center - hl + hw - hh; // Back Bottom Right

        // 4. Rzutowanie siatki do potoku Veldrid (6 ścian x Quad)
        engine.DrawQuad(ftl, ftr, fbr, fbl, materialId); // Front
        engine.DrawQuad(btr, btl, bbl, bbr, materialId); // Tył
        engine.DrawQuad(btl, btr, ftr, ftl, materialId); // Góra
        engine.DrawQuad(fbl, fbr, bbr, bbl, materialId); // Dół
        engine.DrawQuad(btl, ftl, fbl, bbl, materialId); // Lewo
        engine.DrawQuad(ftr, btr, bbr, fbr, materialId); // Prawo
    }
}