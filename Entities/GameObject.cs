using Core.Entities;

namespace CyberEngine.Entities;

public abstract class GameObject
{
    public Transform Transform = Transform.Default;
    public float Radius { get; set; } = 0.5f;
    
    // Flaga oznaczająca obiekt do usunięcia na końcu ramki
    public bool IsDestroyed { get; private set; } = false;

    public virtual void Update(double deltaTime) { }
    public virtual void Render(GameEngine engine) { }

    // Wywołaj to, zamiast manualnie modyfikować kolekcję
    public virtual void Destroy()
    {
        IsDestroyed = true;
    }
}