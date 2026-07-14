using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using CyberEngine.Core;
using CyberEngine.Entities;

namespace CyberEngine.Logic;

public class CyberBoss : GameObject
{
    private readonly Func<List<GameObject>> _objectsProvider;
    private Player _target;
    
    public int Health { get; private set; } = 2000;
    public float Speed { get; private set; } = 2.5f; 
    
    private float _decisionCooldown = 0f;
    private bool _isThinking = false;
    
    // Krótki i techniczny prompt bez "You are..."
    private const string BOSS_PROMPT = "Choose action: 1=Charge, 2=Shield, 3=Artillery. Reply with a single digit.";

    public CyberBoss(float x, float z, Func<List<GameObject>> objectsProvider, Player target)
    {
        Transform.Position = new Vector3(x, 1.0f, z);
        Transform.Scale = new Vector3(2.0f, 2.0f, 2.0f);
        _objectsProvider = objectsProvider;
        _target = target;
    }

    public override void Update(double deltaTime)
    {
        float dt = (float)deltaTime;
        
        if (Health <= 0) return;

        _decisionCooldown -= dt;
        if (_decisionCooldown <= 0f && !_isThinking)
        {
            _decisionCooldown = 2.0f; 
            MakeTacticalDecision();
        }

        Vector3 dir = Vector3.Normalize(_target.Transform.Position - Transform.Position);
        float distance = Vector3.Distance(Transform.Position, _target.Transform.Position);
        
        if (distance > 3.0f) 
        {
            Transform.Position += dir * Speed * dt;
        }
        else
        {
            // Reset Maszyny Stanów (Zabezpieczenie przed wielokrotnym hitem)
            if (Speed > 4.0f) 
            {
                Console.WriteLine("[BOSS] KRYTYCZNE TRAFIENIE Z SZARŻY! -100 HP");
                _target.Energy -= 100;
                
                // Reset przerywacza (powrót do normalnej prędkości)
                Speed = 2.5f; 
                
                // Odrzut fizyczny (Knockback) gracza/bota
                Vector3 knockbackDir = Vector3.Normalize(_target.Transform.Position - Transform.Position);
                _target.Transform.Position += knockbackDir * 3.0f;
            }
        }
    }

    private void MakeTacticalDecision()
    {
        _isThinking = true;
        
        float hpPercent = (Health / 2000f) * 100f;
        float dist = Vector3.Distance(Transform.Position, _target.Transform.Position);
        
        string context = $"My HP: {MathF.Round(hpPercent)}%. Distance to player: {MathF.Round(dist)}m.";

        Task.Run(async () => 
        {
            try
            {
                string decision = await AgentInferenceService.Instance.AskAgentAsync(BOSS_PROMPT, context);
                
                // Ciche zignorowanie braku odpowiedzi (Model zajęty lub wczytuje się z dysku)
                if (decision == "-1" || decision == "LOADING") 
                {
                    return; 
                }

                ExecuteDecision(decision);
            }
            finally
            {
                _isThinking = false;
            }
        });
    }

    private void ExecuteDecision(string decisionId)
    {
        switch (decisionId)
        {
            case "1": 
                Console.WriteLine("[BOSS] WYKONUJE DECYZJĘ 1: SZARŻA!");
                Speed = 8.0f; 
                _decisionCooldown = 0.5f; 
                break;
            case "2":
                Console.WriteLine("[BOSS] WYKONUJE DECYZJĘ 2: TARCZA WZMACNIAJĄCA.");
                Speed = 1.0f; 
                break;
            case "3":
                Console.WriteLine("[BOSS] WYKONUJE DECYZJĘ 3: CIĘŻKA ARTYLERIA.");
                Speed = 0f; 
                break;
            case "0":
                Console.WriteLine("[BOSS] Odrzucono błędny format z LLM.");
                break;
            default:
                Console.WriteLine($"[BOSS] BŁĄD PARSOWANIA. Odebrano zły ID: {decisionId}");
                Speed = 2.5f;
                break;
        }
    }

    public void TakeDamage(int dmg)
    {
        Health -= dmg;
        if (Health <= 0) Console.WriteLine("[BOSS] Pancerz zniszczony... Obezwładniony.");
    }
}