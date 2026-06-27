using System.Numerics;
using CyberEngine.Entities;

namespace CyberEngine.Entities;

public class Laser : GameObject
{
    public Vector3 Direction;
    public float LifeTime = 2.0f;

    public Laser(Core.Math.Vector3 pos, Core.Math.Vector3 dir)
    {
        Transform.Position = pos;
        Direction = new Vector3(dir.X, dir.Y, dir.Z);
    }

    public override void Update(double dt)
    {
        Transform.Position += new Core.Math.Vector3(Direction.X, Direction.Y, Direction.Z) * 35.0f * (float)dt;
        LifeTime -= (float)dt;
    }
}