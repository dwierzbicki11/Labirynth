using Core.Math;
using Veldrid;

namespace CyberEngine.Entities;

public class Player : GameObject
{
    public Vector3 Velocity = Vector3.Zero;
    public float Speed { get; set; } = 8.0f;
    public float Yaw { get; set; } = 0f; // Kąt obrotu kamery i głowy postaci w radianach

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
        // W trybie pierwszoosobowym (FPP) nie renderujemy własnego ciała, 
        // aby nie zasłaniało widoku z oczu postaci.
    }
}