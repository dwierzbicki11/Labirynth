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
}
