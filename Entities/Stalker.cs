using System;
using System.Numerics;
using CyberEngine.Core;
using CyberEngine.Logic;

namespace CyberEngine.Entities;

public class Stalker : GameObject
{
    // FSM: Rozszerzone o stan ataku i poszukiwania
    public enum AIState { Patrol, Search, Chase, Attack }
    public AIState CurrentState = AIState.Patrol;
    
    public GameObject Target { get; set; }
    public int Health { get; set; } = 3;

    // Parametry AI
    public float Alertness = 0.0f;        // 0.0 do 1.0 (pasek wykrycia)
    public float DetectionRange = 18.0f;  // Zwiększony zasięg
    public float HearingRange = 12.0f;    // Słyszy wystrzały
    public float AttackCooldown = 0.0f;
    public float FOV = 0.3f;              // Węższy stożek = bardziej "skupiony" wzrok
    
    private float _animationTimer = 0f;
    private float _yaw = 0f;
    public float Speed { get; set; } = 2.5f;
    private Vector3 _lastKnownPosition;
    private Vector3 _flankOffset = Vector3.Zero; 

    public Stalker(float x, float z) { 
        Transform.Position = new Vector3(x, 0.5f, z); 
        Radius = 0.55f; 
        Health = 3;
    }

    public override void Update(double deltaTime)
    {
        float dt = Math.Clamp((float)deltaTime, 0.0001f, 0.1f);
        _animationTimer += dt;

        if (Target == null || Target.IsDestroyed) return;

        // 1. Zmysły: Wzrok + Słuch
        bool canSee = CanSeeTarget(out float dist);
        bool canHear = CanHearTarget(dist);
        
        // Logika czujności (słyszenie zwiększa alert)
        if (canSee) Alertness += dt * 1.5f;
        else if (canHear) Alertness += dt * 0.8f;
        else Alertness -= dt * 0.4f;
        Alertness = Math.Clamp(Alertness, 0.0f, 1.0f);

        // 2. FSM - Logika stanów
        if (Alertness > 0.7f) { CurrentState = AIState.Chase; _lastKnownPosition = Target.Transform.Position; }
        else if (Alertness > 0.2f && CurrentState == AIState.Patrol) CurrentState = AIState.Search;
        else if (Alertness <= 0.0f) CurrentState = AIState.Patrol;

        if (CurrentState == AIState.Chase && dist < 1.4f) CurrentState = AIState.Attack;
        else if (CurrentState == AIState.Attack && dist > 1.8f) CurrentState = AIState.Chase;

        // 3. Akcje
        switch (CurrentState)
        {
            case AIState.Patrol:
                _yaw += dt * 0.3f;
                break;
            case AIState.Search:
                Vector3 searchDelta = _lastKnownPosition - Transform.Position;
                if (searchDelta.LengthSquared() > 0.000001f)
                {
                    Vector3 searchDir = Vector3.Normalize(searchDelta);
                    _yaw = MathF.Atan2(searchDir.X, searchDir.Z);
                }
                break;
            case AIState.Chase:
                // FLANKOWANIE: Lawirowanie
                Vector3 toTargetDelta = _lastKnownPosition - Transform.Position;
                if (toTargetDelta.LengthSquared() < 0.000001f) break;
                Vector3 toTarget = Vector3.Normalize(toTargetDelta);
                Vector3 perp = new Vector3(-toTarget.Z, 0, toTarget.X); 
                _flankOffset = perp * MathF.Sin(_animationTimer * 2.0f) * 2.0f;
                MoveTowards(_lastKnownPosition + _flankOffset, 2.5f, dt);
                break;
            case AIState.Attack:
                HandleCombat(dt);
                // "Teleportacja" / Glitch przy ataku
                if (Rng.NextDouble() < 0.05)
                {
                    Vector3 attackDelta = Target.Transform.Position - Transform.Position;
                    if (attackDelta.LengthSquared() > 0.000001f)
                        Transform.Position += Vector3.Normalize(attackDelta) * 0.5f;
                }
                break;
        }
    }

    private bool CanHearTarget(float dist)
    {
        if (Target is Player p && p.WeaponRecoil > 0.1f && dist < HearingRange) return true;
        return false;
    }

    private void HandleCombat(double dt)
    {
        if (AttackCooldown <= 0)
        {
            Core.Message.warning("[ULTIMATE STALKER] Atak krytyczny!");
            AttackCooldown = 1.0f;
        }
        AttackCooldown = MathF.Max(0f, AttackCooldown - (float)dt);
    }

    private bool CanSeeTarget(out float dist)
    {
        Vector3 targetDelta = Target.Transform.Position - Transform.Position;
        dist = targetDelta.Length();
        if (dist < 0.000001f) return true;
        Vector3 dirToTarget = targetDelta / dist;
        Vector3 forward = new Vector3(MathF.Sin(_yaw), 0, MathF.Cos(_yaw));
        if (dist > DetectionRange) return false;
        if (Vector3.Dot(forward, dirToTarget) < FOV) return false;

        for (float f = 0; f < dist; f += 0.5f)
        {
            Vector3 checkPos = Transform.Position + dirToTarget * f;
            if (World.GetCeilingHeight(checkPos.X, checkPos.Z) < 1.0f) return false;
        }
        return true;
    }

    private void MoveTowards(Vector3 targetPos, float speed, double dt)
    {
        Vector3 delta = targetPos - Transform.Position;
        if (delta.LengthSquared() < 0.000001f) return;
        Vector3 dir = Vector3.Normalize(delta);
        Transform.Position += dir * speed * (float)dt;
        _yaw = MathF.Atan2(dir.X, dir.Z);
    }
    
    public override void Render(GameEngine engine)
    {
        // 1. Drżenie (Jitter)
        float jitterIntensity = (CurrentState == AIState.Attack) ? 0.08f : 0.02f;
        float jitterX = (float)(Rng.NextDouble() - 0.5) * jitterIntensity;
        float jitterZ = (float)(Rng.NextDouble() - 0.5) * jitterIntensity;
        
        float hover = MathF.Sin(_animationTimer * 3.0f) * 0.1f;
        Vector3 basePos = Transform.Position + new Vector3(jitterX, 0.5f + hover, jitterZ);

        // 2. Kolory (Migający stroboskop w ataku)
        float matId = 4.5f; 
        if (CurrentState == AIState.Search) matId = 3.5f; 
        else if (CurrentState == AIState.Chase) matId = 2.5f; 
        else if (CurrentState == AIState.Attack) matId = (MathF.Sin(_animationTimer * 20.0f) > 0) ? 0.5f : 6.0f;

        Vector3 fwd = new Vector3(MathF.Sin(_yaw), 0, MathF.Cos(_yaw));
        Vector3 rgt = new Vector3(MathF.Cos(_yaw), 0, -MathF.Sin(_yaw));
        
        // 3. Korpus
        DrawBoxOriented(engine, basePos, fwd, rgt, Vector3.UnitY, 0.25f, matId);

        // 4. Wirujący Shell (części)
        for (int i = 0; i < 4; i++)
        {
            float angle = _animationTimer * 2.0f + (i * MathF.PI / 2.0f);
            Vector3 offset = (fwd * MathF.Cos(angle) + rgt * MathF.Sin(angle)) * 0.4f;
            DrawBoxOriented(engine, basePos + offset, fwd, rgt, Vector3.UnitY, 0.1f, matId + 1.0f);
        }

        // 5. Oko
        DrawBoxOriented(engine, basePos + fwd * 0.2f, fwd, rgt, Vector3.UnitY, 0.05f, 7.0f);
    }

    private void DrawBoxOriented(GameEngine engine, Vector3 p, Vector3 fwd, Vector3 rgt, Vector3 up, float s, float id)
    {
        Vector3 tr_f = p + fwd * s + rgt * s + up * s; Vector3 tl_f = p + fwd * s - rgt * s + up * s;
        Vector3 br_f = p + fwd * s + rgt * s - up * s; Vector3 bl_f = p + fwd * s - rgt * s - up * s;
        Vector3 tr_b = p - fwd * s + rgt * s + up * s; Vector3 tl_b = p - fwd * s - rgt * s + up * s;
        Vector3 br_b = p - fwd * s + rgt * s - up * s; Vector3 bl_b = p - fwd * s - rgt * s - up * s;

        engine.DrawQuad(bl_f, br_f, tr_f, tl_f, id); engine.DrawQuad(br_b, bl_b, tl_b, tr_b, id);
        engine.DrawQuad(br_f, br_b, tr_b, tr_f, id); engine.DrawQuad(bl_b, bl_f, tl_f, tl_b, id);
        engine.DrawQuad(tl_f, tr_f, tr_b, tl_b, id); engine.DrawQuad(bl_b, br_b, br_f, bl_f, id);
    }

    public void TakeDamage(int amount)
    {
        Health -= amount;
        if (Health <= 0)
        {
            this.Destroy();
            Core.Message.ok("[SYSTEM] Stalker wyeliminowany. Nagroda: +100 kr.");
        }
    }
    public void Enrage(int healthBuff)
    {
        Health += healthBuff;
        Alertness = 1.0f;
        CurrentState = AIState.Chase;
        Core.Message.warning("[STALKER] Przeciążenie systemów bojowych. Agresja 100%.");
    }
}