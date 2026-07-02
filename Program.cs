using System;
using System.Linq;
using Veldrid;
using CyberEngine;
using CyberEngine.Scenes;
using CyberEngine.Core;

// Twarde przypisanie klasy Program
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("[CyberEngine] Inicjalizacja podsystemu VULKAN na układzie ARM64...");

        try
        {
            SystemConfig.Load();
            GraphicsBackend api = GraphicsBackend.Vulkan;

            var argsList = args.ToList();
            
            // OFICERSKI MODUŁ CI/CD: Interpretacja wejściowych profili uderzeniowych
            int presetIdx = argsList.IndexOf("--preset");
            if (presetIdx != -1 && presetIdx + 1 < argsList.Count)
            {
                string p = argsList[presetIdx + 1].ToLower();
                switch (p)
                {
                    case "low": SystemConfig.GraphicsQuality = 0; SystemConfig.RenderScale = 0.5f; SystemConfig.ShadowsEnabled = false; SystemConfig.BloomEnabled = false; break;
                    case "med": SystemConfig.GraphicsQuality = 1; SystemConfig.RenderScale = 0.75f; SystemConfig.ShadowsEnabled = true; SystemConfig.BloomEnabled = true; break;
                    case "high": SystemConfig.GraphicsQuality = 2; SystemConfig.RenderScale = 1.0f; SystemConfig.ShadowsEnabled = true; SystemConfig.BloomEnabled = true; break;
                }
                SystemConfig.Save();
                Console.WriteLine($"[SYSTEM] Zdalne nadpisanie parametrów GPU: Profil {p.ToUpper()}");
            }

            // [TWARDE ZABEZPIECZENIE SYSTEMOWE] Wymuszenie trybu Headless na poziomie aplikacji
            // Gwarantuje stabilność, odcinając Vulkan od próby tworzenia okna SDL2
            if (argsList.Contains("--fuzz-mode") || argsList.Contains("--stress-test"))
            {
                Environment.SetEnvironmentVariable("HEADLESS", "1");
                Console.WriteLine("[SYSTEM] Tryb Headless wymuszony programowo.");
            }

            bool isAutomated = argsList.Any(a => a == "--benchmark" || a == "--fuzz-mode" || a == "--stress-test");

            GameEngine engine = new GameEngine();
            engine.Initialize("CyberEngine - Matrix Core", 1280, 720, api);

            if (isAutomated)
            {
                Console.WriteLine("[TRYB OFICERSKI] Ładowanie środowiska poligonowego...");
                engine.LoadScene(new GameScene()); 
            }
            else
            {
                Console.WriteLine("[TRYB GRACZA] Ładowanie menu głównego...");
                engine.LoadScene(new MainMenuScene());
            }

            engine.Run(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine("\n========================================");
            Console.WriteLine($"KRYTYCZNY BŁĄD SYSTEMU: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            Console.WriteLine("========================================\n");
            Environment.Exit(1);
        }
    }
}