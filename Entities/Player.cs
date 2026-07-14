using System.Numerics;
using CyberEngine.Entities;

public class Player : GameObject
{
    public bool IsHudOffline { get; set; } = false;
    public float FlashbangIntensity { get; set; } = 0f;
    public float PoisonTimer { get; set; } = 0f;
    public Vector3 Velocity;
    public float Speed { get; set; } = 3.5f;
    public float Yaw { get; set; } = 0f;
    public float Pitch { get; set; } = 0f;

    // Kinematyka głowy
    public float CameraBobOffset { get; private set; } = 0f;
    private float _bobTimer = 0f;
    private readonly float _bobFrequency = 14.0f; 
    private readonly float _bobAmplitude = 0.06f; 

    // Statystyki
    public int Money = 0;
    public int MaxEnergy = 100;
    public float FireRateModifier = 1.0f; 

    // System Broni
    public WeaponInstance CurrentWeapon { get; set; }
    public float ReloadTimer { get; set; } = 0f;
    public bool IsReloading => ReloadTimer > 0f;

    // Zasoby
    public int Energy { get; set; } = 100;
    public float ShootCooldown { get; set; } = 0f;
    public float WeaponRecoil { get; set; } = 0f;

    public Player() { Radius = 0.35f; }

    public override void Update(double deltaTime)
    {
        // 1. Kaganiec fizyki - twardo trzyma gracza na podłożu
        Transform.Position.Y = 0f;

        // 2. Kinematyka (Head-bobbing) bazująca na prędkości
        float currentSpeed = MathF.Sqrt(Velocity.X * Velocity.X + Velocity.Z * Velocity.Z);
        if (currentSpeed > 0.1f)
        {
            _bobTimer += (float)deltaTime * _bobFrequency;
            CameraBobOffset = MathF.Sin(_bobTimer) * _bobAmplitude;
        }
        else
        {
            _bobTimer = 0f;
            CameraBobOffset = Lerp(CameraBobOffset, 0f, (float)deltaTime * 10f);
        }
        if (FlashbangIntensity > 0) FlashbangIntensity = MathF.Max(0f, FlashbangIntensity - (float)deltaTime * 0.5f);
        if (PoisonTimer > 0) { PoisonTimer -= (float)deltaTime; Energy -= 1; }
        if (IsHudOffline && new Random().NextDouble() < 0.01) IsHudOffline = false; // Losowy restart systemów
        
        // ZASADA DOD: Zero odliczania ShootCooldown, WeaponRecoil i ReloadTimer w tym miejscu!
        // Nadpisywanie stanu wykonuje wyłącznie GameScene.cs
    }

    private float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
}