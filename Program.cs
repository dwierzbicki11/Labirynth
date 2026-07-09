using System;
using System.Linq;
using System.Globalization;
using Veldrid;
using CyberEngine;
using CyberEngine.Scenes;
using CyberEngine.Core;
using System.Runtime.InteropServices;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine($"[CyberEngine] Inicjalizacja podsystemu VULKAN na układzie {RuntimeInformation.ProcessArchitecture}...");

        try
        {
            // 1. Wczytanie bazowego configu z dysku
            SystemConfig.Load();
            GraphicsBackend api = GraphicsBackend.Vulkan;

            var argsList = args.ToList();
            
            // 2. OFICERSKI MODUŁ CLI: Pełna parametryzacja sprzętowa
            for (int i = 0; i < argsList.Count; i++)
            {
                string arg = argsList[i].ToLower();
                try
                {
                    switch (arg)
                    {
                        // Explicit Headless Mode Override
                        case "--headless":
                            Environment.SetEnvironmentVariable("HEADLESS", "1");
                            Console.WriteLine("[SYSTEM] Tryb bezgłowy (HEADLESS) wymuszony przez CLI.");
                            break;

                        case "--preset":
                            string p = argsList[++i].ToLower();
                            if (p == "low") { SystemConfig.GraphicsQuality = 0; SystemConfig.RenderScale = 0.5f; SystemConfig.ShadowsEnabled = false; SystemConfig.BloomEnabled = false; }
                            else if (p == "med") { SystemConfig.GraphicsQuality = 1; SystemConfig.RenderScale = 0.75f; SystemConfig.ShadowsEnabled = true; SystemConfig.BloomEnabled = true; }
                            else if (p == "high") { SystemConfig.GraphicsQuality = 2; SystemConfig.RenderScale = 1.0f; SystemConfig.ShadowsEnabled = true; SystemConfig.BloomEnabled = true; }
                            Console.WriteLine($"[CLI] Załadowano preset: {p.ToUpper()}");
                            break;

                        // Kontrola wydajności
                        case "--vsync": SystemConfig.VSync = int.Parse(argsList[++i]) == 1; break;
                        case "--fps-limit": SystemConfig.FpsLimit = int.Parse(argsList[++i]); break;
                        case "--render-scale": SystemConfig.RenderScale = float.Parse(argsList[++i], CultureInfo.InvariantCulture); break;
                        
                        // Geometria i Render
                        case "--resolution":
                            var res = argsList[++i].Split('x');
                            SystemConfig.ResolutionWidth = int.Parse(res[0]);
                            SystemConfig.ResolutionHeight = int.Parse(res[1]);
                            break;
                        case "--quality": SystemConfig.GraphicsQuality = int.Parse(argsList[++i]); break;
                        case "--shadows": SystemConfig.ShadowsEnabled = int.Parse(argsList[++i]) == 1; break;
                        case "--af": SystemConfig.AnisotropicFiltering = int.Parse(argsList[++i]) == 1; break;
                        case "--draw-distance": SystemConfig.DrawDistance = float.Parse(argsList[++i], CultureInfo.InvariantCulture); break;
                        case "--fov": SystemConfig.Fov = float.Parse(argsList[++i], CultureInfo.InvariantCulture); break;

                        // Post-Processing
                        case "--motion-blur": SystemConfig.MotionBlurIntensity = float.Parse(argsList[++i], CultureInfo.InvariantCulture); break;
                        case "--dof": SystemConfig.DepthOfFieldEnabled = int.Parse(argsList[++i]) == 1; break;
                        case "--bloom": SystemConfig.BloomEnabled = int.Parse(argsList[++i]) == 1; break;
                        case "--ao": SystemConfig.AmbientOcclusionEnabled = int.Parse(argsList[++i]) == 1; break;
                        case "--aa": SystemConfig.AntiAliasingMode = int.Parse(argsList[++i]); break;
                        case "--hw-upscale": SystemConfig.HardwareUpscale = int.Parse(argsList[++i]) == 1; break;
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine($"[OSTRZEŻENIE] Zignorowano uszkodzony parametr CLI: {arg}");
                }
            }

            SystemConfig.Save();

            // Automatyczne wykrywanie trybów testowych
            bool isAutomated = argsList.Any(a => a == "--benchmark" || a == "--fuzz-mode" || a == "--stress-test");

            GameEngine engine = new GameEngine();
            engine.IsBenchmarkMode = isAutomated;
            
            // Inicjalizacja podsystemu. Jeśli w CLI nie było --headless, okno zostanie utworzone poprawnie.
            engine.Initialize("CyberEngine - Matrix Core", SystemConfig.ResolutionWidth, SystemConfig.ResolutionHeight, api);

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