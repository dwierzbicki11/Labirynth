using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

// ====================================================================
// 🕹️ PANEL STEROWANIA BENCHMARKIEM
// Zmieniaj tę wartość, uruchamiaj projekt i zapisuj wyniki FPS!
// Opcje dla RPi4: GraphicsBackend.Vulkan LUB GraphicsBackend.OpenGLES
// Opcje dla PC: GraphicsBackend.Vulkan LUB GraphicsBackend.Direct3D11 LUB Direct3D12
// ====================================================================
GraphicsBackend wybraneAPI = GraphicsBackend.Vulkan; 

// Liczba wymuszonych operacji na klatkę (zwiększaj, aż FPS zacznie drastycznie spadać!)
int liczbaZadanDlaSterownika = 500000; 

Console.WriteLine($"[CyberBenchmark] Uruchamianie testu dla API: {wybraneAPI}...");

WindowCreateInfo windowCI = new WindowCreateInfo
{
    X = 100, Y = 100,
    WindowWidth = 960, WindowHeight = 540,
    WindowTitle = $"CyberBenchmark - {wybraneAPI}"
};

Sdl2Window window = VeldridStartup.CreateWindow(ref windowCI);

GraphicsDeviceOptions option = new GraphicsDeviceOptions
{
    Debug = false,
    HasMainSwapchain = true,
    SyncToVerticalBlank = false // V-Sync MUSI być wyłączony do benchmarku!
};

// Inicjalizujemy urządzenie dokładnie z wybranym przez nas API
GraphicsDevice device = VeldridStartup.CreateGraphicsDevice(window, option, wybraneAPI);
Console.WriteLine($"[CyberBenchmark] Urządzenie graficzne zainicjalizowane pomyślnie!");

Stopwatch stopwatch = Stopwatch.StartNew();
double lastTime = 0;
int frameCount = 0;
double fpsTimer = 0;

while (window.Exists)
{
    double currentTime = stopwatch.Elapsed.TotalSeconds;
    double deltaTime = currentTime - lastTime;
    lastTime = currentTime;

    InputSnapshot snapshot = window.PumpEvents();
    if (!window.Exists) break;

    // Licznik FPS
    frameCount++;
    fpsTimer += deltaTime;
    if (fpsTimer >= 1.0)
    {
        window.Title = $"[BENCHMARK] API: {device.BackendType} | Obiektów/Klatkę: {liczbaZadanDlaSterownika} | FPS: {frameCount}";
        frameCount = 0;
        fpsTimer = 0;
    }

    // Dynamiczna zmiana koloru bazowego
    float pulse = (float)Math.Sin(currentTime * 3.0) * 0.5f + 0.5f;
    RgbaFloat baseColor = new RgbaFloat(pulse * 0.1f, pulse * 0.4f, pulse * 0.2f, 1.0f);

    // --- START RENDEROWANIA I STRESS-TESTU ---
    CommandList cl = device.ResourceFactory.CreateCommandList();
    cl.Begin();
    cl.SetFramebuffer(device.MainSwapchain.Framebuffer);
    cl.ClearColorTarget(0, baseColor);

    // 🔥 BRUTALNY TEST API OVERHEAD
    // Bombardujemy sterownik tysiącami instrukcji zmiany okna przycinania (Scissor)
    // Starsze API (OpenGL) zaczną tutaj potężnie obciążać jeden wątek procesora.
    // Nowoczesne API (Vulkan) powinny przetworzyć tę pętlę znacznie sprawniej.
    uint w = (uint)window.Width;
    uint h = (uint)window.Height;
    for (uint i = 0; i < liczbaZadanDlaSterownika; i++)
    {
        // Generujemy pseudo-losowe, zmienne w czasie prostokąty
        uint offset = i % 20;
        cl.SetScissorRect(0, offset, offset, w - (offset * 2), h - (offset * 2));
    }

    cl.End();
    
    // Wysyłamy gigantyczną paczkę rozkazów do GPU
    device.SubmitCommands(cl);
    device.SwapBuffers(device.MainSwapchain);
    
    cl.Dispose();
}

device.Dispose();
Console.WriteLine("[CyberBenchmark] Test zakończony.");
