using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using LLama;
using LLama.Common;
using LLama.Native;

namespace CyberEngine.Logic;

public class AgentInferenceService : IDisposable
{
    public static AgentInferenceService Instance { get; private set; }

    private LLamaWeights _modelWeights;
    private StatelessExecutor _executor;
    private bool _isReady = false;
    
    private static readonly Regex DigitRegex = new Regex(@"[1-3]", RegexOptions.Compiled);

    public AgentInferenceService()
    {
        Instance = this;

        bool isRaspberryPi = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 || 
                             RuntimeInformation.ProcessArchitecture == Architecture.Arm;

        int gpuLayers = isRaspberryPi ? 0 : -1;
        int optimalThreads = isRaspberryPi ? 3 : Math.Max(2, Environment.ProcessorCount / 2);

        Task.Run(() => {
            try {
                NativeApi.llama_log_set((level, message) => { }); 
                
                var modelParams = new ModelParams("Models/Llama-3.2-1B-Instruct-Q4_K_M.gguf") 
                { 
                    ContextSize = 256, 
                    GpuLayerCount = gpuLayers, 
                    Threads = optimalThreads 
                };
                
                _modelWeights = LLamaWeights.LoadFromFile(modelParams);
                _executor = new StatelessExecutor(_modelWeights, modelParams);
                _isReady = true;
                
                Console.WriteLine($"[AgentInference] Mózg Jednostek Mikro załadowany. Akceleracja GPU: {(gpuLayers == -1 ? "TAK" : "NIE")}");
            } catch (Exception ex) { Console.WriteLine($"[AGENT ERROR] {ex.Message}"); }
        });
    }

    public async Task<string> AskAgentAsync(string systemPrompt, string dynamicContext)
    {
        if (!_isReady) return "LOADING"; 
        
        // Krytyczne użycie globalnego zamka - zapobiega natywnemu kraszowi llama.cpp
        if (!await HardwareLock.AINativeLock.WaitAsync(0)) return "-1"; 

        try
        {
            string prompt = 
                $"<|system|>\n{systemPrompt}<|end|>\n" +
                $"<|user|>\nMy HP: 10%. Distance to player: 2m.<|end|>\n" +
                $"<|assistant|>\n1<|end|>\n" +
                $"<|user|>\nMy HP: 80%. Distance to player: 20m.<|end|>\n" +
                $"<|assistant|>\n3<|end|>\n" +
                $"<|user|>\n{dynamicContext}<|end|>\n" +
                $"<|assistant|>\n";
            
            var inferenceParams = new InferenceParams() { 
                MaxTokens = 1, 
                AntiPrompts = new List<string> { "<|end|>", "\n" }
            };
            
            string response = "";
            await foreach (var text in _executor.InferAsync(prompt, inferenceParams)) response += text;
            
            var match = DigitRegex.Match(response);
            string finalDecision = match.Success ? match.Value : "0";

            Console.WriteLine($"[AGENT-LLM] Surowy token: '{response.Replace("\n", "").Trim()}' -> Zinterpretowano jako: {finalDecision}");
            
            return finalDecision;
        }
        catch (Exception ex) 
        { 
            Console.WriteLine($"[AGENT ERROR] Błąd generowania: {ex.Message}");
            return "0"; 
        }
        finally { HardwareLock.AINativeLock.Release(); }
    }

    public void Dispose()
    {
        _modelWeights?.Dispose();
    }
}