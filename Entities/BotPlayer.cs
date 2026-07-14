using System;
using System.Collections.Generic;
using System.Numerics;
using CyberEngine.Core;
using CyberEngine.Entities;

namespace CyberEngine.Logic;

public class BotPlayer : Player
{
    private GameObject _currentTarget;
    private Random _rng = new Random();
    private Func<List<GameObject>> _objectsProvider;
    
    private float _targetYaw = 0f;
    private bool _isTurning = false;
    private float _turnCooldown = 0f;
    
    // ZMIENNE REGULATORA PID
    private float _integralError = 0f;
    private float _previousError = 0f;
    
    private readonly float Kp = 8.0f;  
    private readonly float Ki = 0.5f;  
    private readonly float Kd = 0.8f;  

    // Watchdog
    private Vector3 _watchdogStartPos;
    private float _watchdogTimer = 0f;
    private float _watchdogPathLength = 0f;

    public BotPlayer(Func<List<GameObject>> objectsProvider) : base() 
    {
        _objectsProvider = objectsProvider;
        this.MaxEnergy = 500;
        this.Energy = 500;
        this.Radius = 0.4f; 
        this.Speed = 4.0f; 
        _watchdogStartPos = this.Transform.Position;
    }

    public override void Update(double deltaTime)
    {
        float dt = (float)deltaTime;
        var objs = _objectsProvider();

        // 1. ZEGARY SYSTEMOWE
        if (_turnCooldown > 0f) _turnCooldown -= dt;
        if (ShootCooldown > 0f) ShootCooldown -= dt;
        if (WeaponRecoil > 0f) WeaponRecoil = MathF.Max(0f, WeaponRecoil - dt * 1.5f);

        // 2. WATCHDOG
        _watchdogTimer += dt;
        _watchdogPathLength += Speed * dt; 

        if (_watchdogTimer > 3.0f) 
        {
            float displacement = Vector3.Distance(_watchdogStartPos, Transform.Position); 
            if (displacement < 2.5f && _watchdogPathLength > 5.0f)
            {
                float randomOffset = (float)(_rng.NextDouble() * 1.5 - 0.75); 
                _targetYaw += 3.1415f + randomOffset;
                _targetYaw = NormalizeAngle(_targetYaw);
                _turnCooldown = 2.0f; 
                
                ResetTarget(); // Zmiana dla pewności rozłączenia w stanie zablokowania
            }
            _watchdogStartPos = Transform.Position;
            _watchdogPathLength = 0f;
            _watchdogTimer = 0f;
        }

        // 3. SKANOWANIE CELU (Z histerezą i resetem PID)
        FindTarget(objs);

        if (_currentTarget != null)
        {
            Vector3 dir = Vector3.Normalize(_currentTarget.Transform.Position - Transform.Position);
            _targetYaw = MathF.Atan2(dir.X, -dir.Z);
            
            float aimDiff = MathF.Abs(GetSignedAngleDiff(Yaw, _targetYaw));
            if (aimDiff < 0.15f && CurrentWeapon != null && CurrentWeapon.CanShoot() && ShootCooldown <= 0f)
            {
                ShootWeapon();
                if (_currentTarget is Stalker s) s.TakeDamage(25); 
            }
        }
        else
        {
            Pitch = LerpAngle(Pitch, 0f, dt * 5.0f);

            if (_turnCooldown <= 0f)
            {
                float frontClearance = GetClearance(_targetYaw, objs);
                if (frontClearance < 1.8f)
                {
                    float left = GetClearance(_targetYaw - 1.5708f, objs);
                    float right = GetClearance(_targetYaw + 1.5708f, objs);

                    if (left > right && left > 1.2f) _targetYaw -= 1.5708f;
                    else if (right >= left && right > 1.2f) _targetYaw += 1.5708f;
                    else _targetYaw += 3.1415f;

                    _targetYaw = NormalizeAngle(_targetYaw);
                    _turnCooldown = 1.0f; 
                }
            }
        }

        // 4. MATEMATYKA KINEMATYCZNA: KONTROLER PID DLA ROTACJI
        float error = GetSignedAngleDiff(Yaw, _targetYaw);
        
        _integralError += error * dt;
        _integralError = Math.Clamp(_integralError, -1.5f, 1.5f);
        
        float derivative = (error - _previousError) / dt;
        _previousError = error;

        float angularVelocity = (Kp * error) + (Ki * _integralError) + (Kd * derivative);
        angularVelocity = Math.Clamp(angularVelocity, -10.0f, 10.0f);

        Yaw += angularVelocity * dt;
        Yaw = NormalizeAngle(Yaw);

        // 5. RUCH FIZYCZNY
        float angleDiffForSpeed = MathF.Abs(error);
        _isTurning = angleDiffForSpeed > 0.3f;
        float currentSpeed = _isTurning ? Speed * 0.4f : Speed;

        Vector3 forward = new Vector3(MathF.Sin(Yaw), 0, -MathF.Cos(Yaw));
        Transform.Position += forward * currentSpeed * dt;

        ResolveCollisions(objs);
        
        if (CurrentWeapon != null && CurrentWeapon.CurrentMagazineAmmo <= 0 && CurrentWeapon.SpareMagazines > 0)
        {
            CurrentWeapon.CurrentMagazineAmmo = CurrentWeapon.Archetype.MaxAmmoInMagazine;
            CurrentWeapon.SpareMagazines--;
        }
    }

    private void FindTarget(List<GameObject> objs)
    {
        GameObject oldTarget = _currentTarget;

        // ETAP A: Obsługa i weryfikacja obecnego celu (Sticky Target z Histerezą)
        if (_currentTarget != null && _currentTarget is Stalker currentStalker && !currentStalker.IsDestroyed)
        {
            float currentDist = Vector3.Distance(Transform.Position, _currentTarget.Transform.Position);
            
            if (currentDist < 20.0f && HasLineOfSight(_currentTarget.Transform.Position, objs))
            {
                // THREAT OVERRIDE: Sprawdź czy inny wróg nie jest krytycznie blisko (w promieniu 3m)
                bool emergencyOverride = false;
                foreach (var obj in objs)
                {
                    if (obj is Stalker s && !s.IsDestroyed && s != _currentTarget)
                    {
                        if (Vector3.Distance(Transform.Position, s.Transform.Position) < 3.0f && HasLineOfSight(s.Transform.Position, objs))
                        {
                            _currentTarget = s;
                            emergencyOverride = true;
                            break; 
                        }
                    }
                }
                
                // Jeśli nikt nam nie podszedł na nóż, trzymamy się dotychczasowego celu
                if (!emergencyOverride) return; 
            }
            else
            {
                _currentTarget = null; // Cel poza zasięgiem lub uciekł za ścianę
            }
        }

        // ETAP B: Klasyczne skanowanie (uruchamia się tylko jeśli nie mamy celu)
        if (_currentTarget == null)
        {
            float minDist = 20.0f;
            foreach (var obj in objs) 
            {
                if (obj is Stalker s && !s.IsDestroyed)
                {
                    float dist = Vector3.Distance(Transform.Position, s.Transform.Position);
                    if (dist < minDist && HasLineOfSight(s.Transform.Position, objs))
                    {
                        minDist = dist; 
                        _currentTarget = s;
                    }
                }
            }
        }

        // ETAP C: TWARDY RESET PAMIĘCI PID
        // Rozwiązuje to problem "Derivative Kick" kręcący botem w kółko przy szybkiej zmianie wroga
        if (_currentTarget != oldTarget)
        {
            _integralError = 0f;
            _previousError = 0f;
        }
    }

    private void ResetTarget()
    {
        _currentTarget = null;
        _integralError = 0f;
        _previousError = 0f;
    }

    private void ShootWeapon()
    {
        CurrentWeapon.CurrentMagazineAmmo--;
        ShootCooldown = CurrentWeapon.Archetype.FireCooldown;
        WeaponRecoil += CurrentWeapon.Archetype.BaseRecoil;
        Pitch = Math.Clamp(Pitch + 0.05f, -1.4f, 1.4f);
    }

    private float GetSignedAngleDiff(float current, float target)
    {
        float diff = target - current;
        while (diff < -MathF.PI) diff += MathF.PI * 2.0f;
        while (diff > MathF.PI) diff -= MathF.PI * 2.0f;
        return diff;
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > MathF.PI) angle -= MathF.PI * 2;
        while (angle < -MathF.PI) angle += MathF.PI * 2;
        return angle;
    }

    private void ResolveCollisions(List<GameObject> objs)
    {
        float wallHalfSize = 1.0f; 
        foreach (var obj in objs)
        {
            if (obj is Wall wall)
            {
                float closestX = Math.Clamp(Transform.Position.X, wall.Transform.Position.X - wallHalfSize, wall.Transform.Position.X + wallHalfSize);
                float closestZ = Math.Clamp(Transform.Position.Z, wall.Transform.Position.Z - wallHalfSize, wall.Transform.Position.Z + wallHalfSize);
                float dx = Transform.Position.X - closestX;
                float dz = Transform.Position.Z - closestZ;
                
                if ((dx * dx + dz * dz) < (Radius * Radius))
                {
                    float dist = MathF.Sqrt(dx * dx + dz * dz);
                    if (dist > 0.001f)
                    {
                        Transform.Position.X += (dx / dist) * (Radius - dist) * 0.5f;
                        Transform.Position.Z += (dz / dist) * (Radius - dist) * 0.5f;
                    }
                }
            }
        }
    }

    private float GetClearance(float checkYaw, List<GameObject> objs)
    {
        Vector3 dir = new Vector3(MathF.Sin(checkYaw), 0, -MathF.Cos(checkYaw));
        float minClearance = 5.0f;
        foreach(var obj in objs)
        {
            if (obj is Wall w)
            {
                for (float d = 0.5f; d <= 5.0f; d += 0.5f)
                {
                    Vector3 p = Transform.Position + dir * d;
                    if (MathF.Abs(p.X - w.Transform.Position.X) < 1.2f && MathF.Abs(p.Z - w.Transform.Position.Z) < 1.2f) 
                    {
                        if (d < minClearance) minClearance = d;
                        break; 
                    }
                }
            }
        }
        return minClearance; 
    }

    private bool HasLineOfSight(Vector3 targetPos, List<GameObject> objs)
    {
        Vector3 dir = targetPos - Transform.Position;
        float maxDist = dir.Length();
        dir = Vector3.Normalize(dir);

        for (float d = 0.5f; d < maxDist - 0.5f; d += 0.5f)
        {
            Vector3 p = Transform.Position + dir * d;
            foreach(var obj in objs)
            {
                if (obj is Wall w)
                {
                    if (MathF.Abs(p.X - w.Transform.Position.X) < 1.0f && MathF.Abs(p.Z - w.Transform.Position.Z) < 1.0f) 
                        return false; 
                }
            }
        }
        return true;
    }

    private float LerpAngle(float current, float target, float t)
    {
        float delta = NormalizeAngle(target - current);
        return current + delta * Math.Clamp(t, 0f, 1f);
    }
}