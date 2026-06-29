using System.Numerics;
using Veldrid;

namespace CyberEngine.Entities;

public class Player : GameObject
{
    public Vector3 Velocity = Vector3.Zero;
    public float Speed { get; set; } = 3.5f;
    public float Yaw { get; set; } = 0f;   
    public float Pitch { get; set; } = 0f; 
    
    public int Ammo { get; set; } = 30;
    public int MaxMagazine { get; set; } = 30;
    public float ReloadTimer { get; set; } = 0f;
    public bool IsReloading => ReloadTimer > 0f;

    public int Energy { get; set; } = 100;
    public float ShootCooldown { get; set; } = 0f;
    public float WeaponRecoil { get; set; } = 0f; 

    public Player() { Radius = 0.35f; }

    public override void Update(double deltaTime)
    {
        Transform.Position += Velocity * (float)deltaTime;

        if (ShootCooldown > 0f) ShootCooldown -= (float)deltaTime;
        if (WeaponRecoil > 0f) WeaponRecoil -= (float)deltaTime * 5f;
        if (WeaponRecoil < 0f) WeaponRecoil = 0f;

        if (ReloadTimer > 0f)
        {
            ReloadTimer -= (float)deltaTime;
            if (ReloadTimer <= 0f)
            {
                Ammo = MaxMagazine;
                ReloadTimer = 0f;
            }
        }
    }
}
