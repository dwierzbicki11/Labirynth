using System.Numerics;

namespace CyberEngine.Entities;

public struct Transform
{
    public Vector3 Position;
    public Vector3 Rotation;
    public Vector3 Scale;
    public static Transform Default => new Transform { Position = Vector3.Zero, Rotation = Vector3.Zero, Scale = Vector3.One };
}

public abstract class GameObject
{
    public Transform Transform = Transform.Default;
    public float Radius { get; set; } = 0.5f;
    public bool IsDestroyed { get; private set; } = false;

    public virtual void Update(double deltaTime) { }
    public virtual void Render(GameEngine engine) { }
    public virtual void Destroy() { IsDestroyed = true; }
}
