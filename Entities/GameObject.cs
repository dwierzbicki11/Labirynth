using Core.Entities;

namespace CyberEngine.Entities;

public abstract class GameObject
{
    public Transform Transform = Transform.Default;
    public float Radius { get; set; } = 0.5f;

    public virtual void Update(double deltaTime) { }
    public virtual void Render(GameEngine engine) { }
}