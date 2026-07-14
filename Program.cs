#nullable disable

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
        NativeLibrary.SetDllImportResolver(typeof(Vulkan.VulkanNative).Assembly, (libraryName, assembly, searchPath) =>
        {
            if (libraryName == "vulkan" || libraryName == "libvulkan.so")
            {
                return NativeLibrary.Load("libvulkan.so.1", assembly, searchPath);
            }
            if (libraryName == "libdl" || libraryName == "dl")
            {
                return NativeLibrary.Load("libdl.so.2", assembly, searchPath);
            }
            return IntPtr.Zero;
        });

        try
        {
            var argsList = args.ToList();

            // MIEJSCE DOCELOWE DLA AI-WORKER: Przechwytujemy go przed właściwym startem silnika!
            if (argsList.Contains("--ai-worker"))
            {
                Console.WriteLine("[AI-WORKER] Uruchamianie hybrydowego węzła obliczeniowego...");
                
                System.Threading.Tasks.Task.Run(() => {
                    try
                    {
                        using var udpServer = new System.Net.Sockets.UdpClient(9000);
                        var remoteEP = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);
                        Console.WriteLine("[AI-WORKER] Serwer UDP nasłuchuje na porcie 9000...");
                        while (true) 
                        {
                            byte[] data = udpServer.Receive(ref remoteEP);
                            Console.WriteLine($"[TELEMETRIA OD {remoteEP.Address}]: {System.Text.Encoding.UTF8.GetString(data)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[OSTRZEŻENIE] Błąd serwera UDP: {ex.Message}");
                    }
                });

                Environment.SetEnvironmentVariable("HEADLESS", "1");
                GameEngine aiEngine = new GameEngine();
                aiEngine.Initialize("CyberEngine - AI Node", 800, 600, GraphicsBackend.Vulkan);
                aiEngine.Run(args);
                return; // Zakończ Main, żeby nie odpalać reszty trybu gracza!
            }
            
            SystemConfig.Load();
            GraphicsBackend api = GraphicsBackend.Vulkan;

            for (int i = 0; i < argsList.Count; i++)
            {
                string arg = argsList[i].ToLower();
                try
                {
                    switch (arg)
                    {
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

                        case "--vsync": SystemConfig.VSync = int.Parse(argsList[++i]) == 1; break;
                        case "--fps-limit": SystemConfig.FpsLimit = int.Parse(argsList[++i]); break;
                        case "--render-scale": SystemConfig.RenderScale = float.Parse(argsList[++i], CultureInfo.InvariantCulture); break;
                        
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

                        case "--motion-blur": SystemConfig.MotionBlurIntensity = float.Parse(argsList[++i], CultureInfo.InvariantCulture); break;
                        case "--dof": SystemConfig.DepthOfFieldEnabled = int.Parse(argsList[++i]) == 1; break;
                        case "--bloom": SystemConfig.BloomEnabled = int.Parse(argsList[++i]) == 1; break;
                        case "--ao": SystemConfig.AmbientOcclusionEnabled = int.Parse(argsList[++i]) == 1; break;
                        case "--aa": SystemConfig.AntiAliasingMode = int.Parse(argsList[++i]); break;
                        case "--hw-upscale": SystemConfig.HardwareUpscale = int.Parse(argsList[++i]) == 1; break;
                        
                        case "--telemetry-target":
                            string ip = argsList[++i];
                            Environment.SetEnvironmentVariable("TELEMETRY_IP", ip);
                            break;
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine($"[OSTRZEŻENIE] Zignorowano uszkodzony parametr CLI: {arg}");
                }
            }

            SystemConfig.Save();

            bool isAutomated = argsList.Any(a => a == "--benchmark" || a == "--fuzz-mode" || a == "--stress-test");

            GameEngine engine = new GameEngine();
            engine.IsBenchmarkMode = isAutomated;
            
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
            
            engine.TelemetryTargetIp = Environment.GetEnvironmentVariable("TELEMETRY_IP") ?? "127.0.0.1";
            engine.Run(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine("\n========================================");
            Console.WriteLine($"KRYTYCZNY BŁĄD SYSTEMU: {ex.Message}");
            
            if (ex.InnerException != null)
            {
                Console.WriteLine($"\n--- UKRYTY BŁĄD NATYWNY (InnerException) ---");
                Console.WriteLine($"Wiadomość: {ex.InnerException.Message}");
                Console.WriteLine($"Typ: {ex.InnerException.GetType().Name}");
                Console.WriteLine($"Ślad: {ex.InnerException.StackTrace}");
                Console.WriteLine($"--------------------------------------------\n");
            }

            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            Console.WriteLine("========================================\n");
            Environment.Exit(1);
        }
    }
}