using Core.Math;
using Veldrid;

namespace CyberEngine.Entities;

public class Player : GameObject
{
    public Vector3 Velocity = Vector3.Zero;
    public float Speed { get; set; } = 8.0f;

    public Player()
    {
        Radius = 0.35f; 
    }

    public override void Update(double deltaTime)
    {
        Transform.Position += Velocity * (float)deltaTime;
    }

    public override void Render(GameEngine engine)
    {
        // Rysujemy gracza jako zielony prostopadłościan o wysokości 1.2 jednostki
        engine.DrawCube(
            Transform.Position.X - 0.3f, 
            0.0f, 
            Transform.Position.Z - 0.3f, 
            0.6f, 1.2f, 0.6f, 
            new RgbaFloat(0.0f, 1.0f, 0.0f, 1.0f)
        );
    }
}