using System.Numerics;

namespace CyberEngine.Entities;

public class Laser : GameObject
{
    public Vector3 Direction;
    public float LifeTime = 2.0f;

    public Laser(Vector3 pos, Vector3 dir)
    {
        Transform.Position = pos;
        Direction = dir;
        Radius = 0.1f;
    }

    public override void Update(double dt)
    {
        Transform.Position += Direction * 35.0f * (float)dt;
        LifeTime -= (float)dt;
        if (LifeTime <= 0f) Destroy();
    }

    public override void Render(GameEngine engine)
    {
        // Rysujemy pocisk 3D obrócony precyzyjnie z wektorem lotu
        Vector3 right = Vector3.Normalize(Vector3.Cross(Direction, Vector3.UnitY));
        if (right.LengthSquared() < 0.001f) right = Vector3.UnitX; // Failsafe dla pionowych strzałów
        Vector3 up = Vector3.Cross(right, Direction);

        Vector3 tail = Transform.Position;
        Vector3 head = Transform.Position + Direction * 1.5f;

        float thickness = 0.02f;

        Vector3 p0 = tail - right * thickness - up * thickness;
        Vector3 p1 = tail - right * thickness + up * thickness;
        Vector3 p2 = tail + right * thickness + up * thickness;
        Vector3 p3 = tail + right * thickness - up * thickness;

        Vector3 e0 = head - right * thickness - up * thickness;
        Vector3 e1 = head - right * thickness + up * thickness;
        Vector3 e2 = head + right * thickness + up * thickness;
        Vector3 e3 = head + right * thickness - up * thickness;

        // matId = 0.0f wymusza omijanie oświetlenia w shaderze (czysty kolor/glow)
        engine.DrawQuad(p0, e0, e1, p1, 0.0f);
        engine.DrawQuad(p3, p2, e2, e3, 0.0f);
        engine.DrawQuad(p1, e1, e2, p2, 0.0f);
        engine.DrawQuad(p0, p3, e3, e0, 0.0f);
    }
}