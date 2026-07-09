using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using CyberEngine.Core;
using CyberEngine.Entities;
using LLama;
using LLama.Common;
using LLama.Native;

namespace CyberEngine.Logic;

public class GodDirector : IDisposable
{
    private readonly Func<List<GameObject>> _objectsProvider;
    private readonly Player _player;
    
    public float ThreatLevel { get; private set; } = 0f;
    private DateTime _lastDetectionLog = DateTime.MinValue;

    private LLamaWeights _modelWeights;
    private StatelessExecutor _executor; 
    private bool _isModelReady = false;
    
    private const string SYSTEM_PROMPT = "<|system|>\nYou are a combat AI. Tactical choices: 1=Assault, 2=Ambush, 3=Lockdown, 4=Cyber-Jam, 5=Patrol. Return ONLY a single number from 1 to 5.<|end|>\n<|user|>\nThreat Level: {0}. Selected ID:<|end|>\n<|assistant|>\n";
    private static readonly Regex DigitRegex = new Regex(@"[1-5]", RegexOptions.Compiled);

    public GodDirector(Func<List<GameObject>> objectsProvider, Player player)
    {
        _objectsProvider = objectsProvider;
        _player = player;
        
        bool isRaspberryPi = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 || 
                             RuntimeInformation.ProcessArchitecture == Architecture.Arm;

        int optimalThreads = isRaspberryPi ? 3 : Math.Max(2, Environment.ProcessorCount / 2);
        int gpuLayers = isRaspberryPi ? 0 : -1;

        Task.Run(() => {
            try {
                NativeApi.llama_log_set((level, message) => { });
                
                var modelParams = new ModelParams("Models/Phi-3-mini-4k-instruct-Q4_K_M.gguf") 
                { 
                    ContextSize = 128, 
                    GpuLayerCount = gpuLayers, 
                    Threads = optimalThreads 
                };
                
                _modelWeights = LLamaWeights.LoadFromFile(modelParams);
                _executor = new StatelessExecutor(_modelWeights, modelParams);
                _isModelReady = true;
                
                string arch = isRaspberryPi ? "ARM64 (Raspberry Pi)" : "x64 (PC/Laptop)";
                Console.WriteLine($"[GodDirector] System Phi-3 Online. Arch: {arch}, Wątki: {optimalThreads}, GPU Layers: {gpuLayers}");
            } catch (Exception ex) { Console.WriteLine($"[L1 ERROR] {ex.Message}"); }
        });
    }

    public void ReportIntrusion(Vector3 cam, Vector3 target)
    {
        ThreatLevel = Math.Min(100f, ThreatLevel + 30f);
        
        if ((DateTime.Now - _lastDetectionLog).TotalSeconds > 3.0)
        {
            Console.WriteLine($"[IDS] Threat Level: {ThreatLevel:F0}");
            _lastDetectionLog = DateTime.Now;
        }

        if (_isModelReady && HardwareLock.AINativeLock.CurrentCount > 0)
        {
            Task.Run(() => RunInference());
        }
    }

    private async Task RunInference()
    {
        // Blokada sprzętowa zapobiegająca kolizji z modelem Llama
        if (!await HardwareLock.AINativeLock.WaitAsync(0)) return;
        
        try
        {
            string prompt = string.Format(SYSTEM_PROMPT, MathF.Round(ThreatLevel, 0));
            
            var inferenceParams = new InferenceParams() { 
                MaxTokens = 1, 
                AntiPrompts = new List<string> { "<|end|>", "\n" }
            };
            
            string response = "";
            await foreach (var text in _executor.InferAsync(prompt, inferenceParams)) response += text;
            
            var match = DigitRegex.Match(response);
            string id = match.Success ? match.Value : "1";
            
            Console.WriteLine($"[AI DECISION] ID: {id} | Otrzymano od Phi-3");
            ExecuteLlmCommand(id, _player.Transform.Position);
        }
        catch (Exception ex) { Console.WriteLine($"[AI ERROR] {ex.Message}"); }
        finally { HardwareLock.AINativeLock.Release(); }
    }

    private void ExecuteLlmCommand(string id, Vector3 pos)
    {
        switch (id)
        {
            case "1": 
                GlobalDispatch();
                SpawnStalkerAmbush(pos, 5);
                foreach(var o in _objectsProvider()) if(o is Stalker s) s.Enrage(5);
                break;
            case "2": 
                SpawnStalkerAmbush(pos, 3);
                break;
            case "3": 
                ExecuteSealDoors(pos);
                break;
            case "4": 
                _player.IsHudOffline = true;
                _player.FlashbangIntensity = 1.0f;
                break;
            case "5": 
                ExecuteSearchPattern();
                break;
            default:
                GlobalDispatch();
                break;
        }
    }

    public void Update(double dt)
    {
        ThreatLevel = MathF.Max(0f, ThreatLevel - ((float)dt * 1.0f));
    }

    private void ExecuteSearchPattern() 
    { 
        foreach(var o in _objectsProvider()) 
            if(o is Stalker s && !s.IsDestroyed) 
            { 
                s.CurrentState = Stalker.AIState.Search; 
                s.Alertness = 0.5f; 
            } 
    }

    private void ExecuteSealDoors(Vector3 p) 
    { 
        _objectsProvider().Add(new Wall(p.X + 3f, p.Z, 2f)); 
        _objectsProvider().Add(new Wall(p.X - 3f, p.Z, 2f)); 
    }

    private void GlobalDispatch() 
    { 
        foreach(var o in _objectsProvider()) 
            if(o is Stalker s && !s.IsDestroyed && s.Target == null) s.Target = _player; 
    }

    private void SpawnStalkerAmbush(Vector3 p, int c) 
    { 
        Random rng = new Random(); 
        for(int i = 0; i < c; i++) 
            _objectsProvider().Add(new Stalker(p.X + (float)rng.NextDouble() * 4 - 2, p.Z + (float)rng.NextDouble() * 4 - 2)); 
    }

    public void Dispose() 
    { 
        _modelWeights?.Dispose(); 
    }
}