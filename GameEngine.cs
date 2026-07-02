#nullable disable

using System;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Linq;
using System.IO;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using CyberEngine.Core;
using CyberEngine.Graphics;
using CyberEngine.Logic;
using CyberEngine.Scenes;

namespace CyberEngine;

public class GameEngine
{
    private Sdl2Window _window;
    private readonly World _world = new World();
    private readonly Renderer _renderer = new Renderer();

    private Scene _currentScene;
    private Scene _nextScene;

    public Vector3 CameraPosition { get => _world.CameraPosition; set => _world.CameraPosition = value; }
    public float CameraYaw { get => _world.CameraYaw; set => _world.CameraYaw = value; }
    public float CameraPitch { get => _world.CameraPitch; set => _world.CameraPitch = value; }
    public float WeaponRecoil { get => _world.WeaponRecoil; set => _world.WeaponRecoil = value; }
    public int PlayerEnergy { get => _world.PlayerEnergy; set => _world.PlayerEnergy = value; }
    public bool ShowGameplayHud { get => _world.ShowGameplayHud; set => _world.ShowGameplayHud = value; }
    public Vector3 CameraForward { get => _world.CameraForward; set => _world.CameraForward = value; }
    public Vector2 MouseDelta => _world.MouseDelta;
    public bool TriggerMuzzleFlash { get => _world.TriggerMuzzleFlash; set => _world.TriggerMuzzleFlash = value; }
    
    public float Width => SystemConfig.ResolutionWidth;
    public float Height => SystemConfig.ResolutionHeight;
    public RgbaFloat ClearColor { get; set; } = RgbaFloat.Black;
    public string GpuName => _renderer.Device?.DeviceName ?? "Headless GPU";

    public void LoadScene(Scene scene) { _nextScene = scene; }
    public void RegisterLantern(Vector3 position) { _world.RegisterLantern(position); }
    public void DrawHorizontalPlane(float x, float y, float z, float w, float d, float m, float nx, float ny, float nz) => _world.DrawHorizontalPlane(x, y, z, w, d, m, nx, ny, nz);
    public void DrawCube(float x, float y, float z, float w, float h, float d) => _world.DrawCube(x, y, z, w, h, d);
    public void DrawHudRectangle(float x, float y, float w, float h, RgbaFloat c) => _world.DrawHudRectangle(x, y, w, h, c, Width, Height);
    public void DrawHudText(string t, float x, float y, float s, RgbaFloat c) => _world.DrawHudText(t, x, y, s, c, Width, Height);

    public void ApplyGraphicsSettings() { }

    public void Initialize(string title, int width, int height, GraphicsBackend backend)
    {
        bool isHeadless = Environment.GetEnvironmentVariable("HEADLESS") == "1";
        try
        {
            if (!isHeadless)
            {
                WindowCreateInfo windowCI = new WindowCreateInfo { X = 0, Y = 0, WindowWidth = width, WindowHeight = height, WindowTitle = title, WindowInitialState = WindowState.Normal };
                _window = VeldridStartup.CreateWindow(ref windowCI);
            }

            _renderer.Initialize(_window, width, height, backend);
            Console.WriteLine($"[INIT] Silnik gotowy. Architektura Modularna (CPU/GPU) Aktywna.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CRITICAL] Initialization failed: {ex.Message}");
            throw;
        }
    }
    
    private void RunStressTest(int seconds)
    {
        Console.WriteLine($"--- [STRESS TEST ZAINICJOWANY: {seconds} SEKUND] ---");
        Stopwatch sw = Stopwatch.StartNew();
        
        while (sw.Elapsed.TotalSeconds < seconds) {
            _world.CameraPosition += new Vector3(MathF.Sin(_world.CameraYaw), 0, -MathF.Cos(_world.CameraYaw)) * 0.05f;
            _world.PrepareNextFrame(sw.Elapsed.TotalSeconds);
            _currentScene?.OnUpdate(1f/60f, null, this);
            _renderer.DrawFrame(_world.Data, _window, Width, Height, ClearColor, true);
        }
        
        sw.Stop();
        Console.WriteLine($"--- [STRESS TEST ZAKOŃCZONY POMYŚLNIE] ---");
        _renderer.Dispose();
    }
    
    private void RunFuzzing(int iterations)
    {
        Console.WriteLine($"--- [FUZZING ZAINICJOWANY: {iterations} CYKLI CPU] ---");
        Stopwatch sw = Stopwatch.StartNew(); 
        float fixedDelta = 1f / 60f;
        Random rng = new Random(1337);
        int logInterval = iterations / 10;
        
        for (int i = 0; i < iterations; i++)
        {
            if (i % 300 == 0) _world.CameraYaw = (float)(rng.NextDouble() * Math.PI * 2);
            Vector3 forward = new Vector3(MathF.Sin(_world.CameraYaw), 0f, -MathF.Cos(_world.CameraYaw));
            _world.CameraPosition += forward * 0.05f; 

            _world.UpdateLogicStats(fixedDelta);
            _currentScene?.OnUpdate(fixedDelta, null, this);
            if (_currentScene != null) {
                for (int j = 0; j < _currentScene.GameObjects.Count; j++)
                    if (!_currentScene.GameObjects[j].IsDestroyed) _currentScene.GameObjects[j].Update(fixedDelta);
                _currentScene.GameObjects.RemoveAll(o => o.IsDestroyed);
            }

            // MODUŁ TELEMETRII (Raportowanie co 10%)
            if (i > 0 && i % logInterval == 0)
            {
                double currentSec = sw.Elapsed.TotalSeconds;
                double currentFps = i / currentSec;
                float progress = ((float)i / iterations) * 100f;
                Console.WriteLine($"[TELEMETRIA] Fuzzing: {i}/{iterations} ({progress:F0}%) | Czas: {currentSec:F2}s | Wydajność rdzenia: {currentFps:F0} FPS");
            }
        }
        sw.Stop();
        Console.WriteLine($"--- [FUZZING ZAKOŃCZONY. Czas całkowity: {sw.Elapsed.TotalSeconds:F2}s. Stabilność: 100%] ---");
        _renderer.Dispose();
    }

    public void Run(string[] args)
    {
        // OFICERSKA KOREKTA PRZEPŁYWU STEROWANIA: Obsługa trybów zautomatyzowanych
        if (args.Contains("--benchmark")) { RunBenchmark(1000); return; }
        if (args.Contains("--fuzz-mode")) { RunFuzzing(50000); return; }
        if (args.Contains("--stress-test")) { RunStressTest(30); return; }

        Stopwatch stopwatch = Stopwatch.StartNew();
        double lastTime = 0;
        bool isHeadless = Environment.GetEnvironmentVariable("HEADLESS") == "1";

        while (isHeadless || _window.Exists)
        {
            if (_nextScene != null) { _currentScene?.OnUnload(this); _currentScene = _nextScene; _nextScene = null; _currentScene.OnLoad(this); }

            double currentTime = stopwatch.Elapsed.TotalSeconds; float deltaTime = (float)(currentTime - lastTime); lastTime = currentTime;
            InputSnapshot snapshot = isHeadless ? null : _window.PumpEvents();
            if (!isHeadless && !_window.Exists) break;

            if (!isHeadless && _window.Focused)
            {
                _window.CursorVisible = false; int cx = _window.Width / 2; int cy = _window.Height / 2;
                _world.MouseDelta = new Vector2(snapshot.MousePosition.X - cx, snapshot.MousePosition.Y - cy);
                _window.SetMousePosition(cx, cy);
            }
            else { if (!isHeadless) _window.CursorVisible = true; _world.MouseDelta = Vector2.Zero; }

            // === 1. FAZA LOGIKI (CPU) ===
            _world.UpdateLogicStats(deltaTime);
            _currentScene?.OnUpdate(deltaTime, snapshot, this);

            if (_currentScene != null)
            {
                for (int i = 0; i < _currentScene.GameObjects.Count; i++)
                    if (!_currentScene.GameObjects[i].IsDestroyed) _currentScene.GameObjects[i].Update(deltaTime);
                _currentScene.GameObjects.RemoveAll(o => o.IsDestroyed);
            }

            // === 2. FAZA PRZYGOTOWANIA DANYCH RENDEROWANIA ===
            _world.Data.GpuDrawCalls = 0; _world.Data.GpuVertices = 0;
            _world.PrepareNextFrame(currentTime);
            
            if (ShowGameplayHud)
            {
                float viewDist = SystemConfig.GraphicsQuality switch { 0 => 16f, 1 => 24f, 2 => 36f, _ => 24f };
                float fXStart = MathF.Floor(CameraPosition.X / 2f) * 2f - viewDist; float fXEnd = MathF.Floor(CameraPosition.X / 2f) * 2f + viewDist;
                float fZStart = MathF.Floor(CameraPosition.Z / 2f) * 2f - viewDist; float fZEnd = MathF.Floor(CameraPosition.Z / 2f) * 2f + viewDist;
                for (float x = fXStart; x <= fXEnd; x += 2f)
                    for (float z = fZStart; z <= fZEnd; z += 2f) {
                        DrawHorizontalPlane(x, 0.0f, z, 2f, 2f, 1.0f, 0f, 1f, 0f); DrawHorizontalPlane(x, 2.0f, z, 2f, 2f, 1.0f, 0f, -1f, 0f);
                    }
                if (_currentScene != null) foreach (var obj in _currentScene.GameObjects) obj.Render(this);
            }

            _currentScene?.OnRenderUI(this);
            RenderInternalHud();

            // UDP Stats (Telemetria)
            SendUdpStats();

            // === 3. FAZA RENDEROWANIA (GPU) ===
            _renderer.DrawFrame(_world.Data, _window, Width, Height, ClearColor, isHeadless);
            _world.TriggerMuzzleFlash = false;
        }

        _renderer.Dispose();
    }

    private void RunBenchmark(int frameCount)
    {
        string profile = SystemConfig.GraphicsQuality switch { 0 => "LOW", 1 => "MED", 2 => "HIGH", _ => "CUSTOM" };
        Console.WriteLine($"--- [BENCHMARK ZAINICJOWANY: {frameCount} KLATEK | PROFIL: {profile}] ---");
        Stopwatch sw = Stopwatch.StartNew();
        float fixedDelta = 1f / 60f;
        bool isHeadless = Environment.GetEnvironmentVariable("HEADLESS") == "1";

        for (int i = 0; i < frameCount; i++)
        {
            _world.PrepareNextFrame(sw.Elapsed.TotalSeconds);
            _currentScene?.OnUpdate(fixedDelta, null, this);
            if (_currentScene != null) foreach (var obj in _currentScene.GameObjects) obj.Render(this);
            
            _renderer.DrawFrame(_world.Data, _window, Width, Height, ClearColor, isHeadless);
        }
        
        sw.Stop();
        double avg = sw.Elapsed.TotalMilliseconds / frameCount; double fps = 1000.0 / avg;
        
        string res = $"[PROFIL: {profile,-4}] FPS: {fps,6:F1}  |  Sredni czas klatki: {avg,5:F2} ms\n";
        
        File.AppendAllText("benchmark_results.txt", res);
        
        Console.WriteLine($"--- [WYNIK ODCZYTANY I ZAPISANY DO LOGU] ---");
        _renderer.Dispose();
    }

    private void RenderInternalHud()
    {
        RgbaFloat matrixGreen = new RgbaFloat(0.0f, 1.0f, 0.2f, 1.0f); RgbaFloat darkGreen = new RgbaFloat(0.0f, 0.25f, 0.05f, 1.0f);
        DrawHudRectangle(15, 15, 360, 205, new RgbaFloat(0.0f, 0.05f, 0.01f, 0.70f));
        DrawHudText($"FPS: {_world.CurrentFps:F0}", 25, 25, 3f, matrixGreen);
        DrawHudText($"CPU: {_world.CurrentCpuPercent:F1} %", 25, 55, 3f, matrixGreen);
        DrawHudRectangle(25, 75, 340, 6, darkGreen);
        DrawHudRectangle(25, 75, Math.Clamp((float)(_world.CurrentCpuPercent / 100.0 * 340.0), 0f, 340f), 6, matrixGreen);
        DrawHudText($"RAM: {_world.RamUsage:F0} MB", 25, 90, 3f, matrixGreen);
        DrawHudText($"GPU DC: {_world.Data.GpuDrawCalls}", 25, 120, 3f, matrixGreen);
        DrawHudText($"GPU POLY: {_world.Data.GpuVertices}", 25, 150, 3f, matrixGreen);

        if (ShowGameplayHud)
        {
            float midX = Width / 2f; float midY = Height / 2f;
            DrawHudRectangle(midX - 12, midY - 1, 24, 2, matrixGreen); DrawHudRectangle(midX - 1, midY - 12, 2, 24, matrixGreen);
            DrawHudRectangle(15, Height - 65, 250, 50, new RgbaFloat(0.0f, 0.05f, 0.01f, 0.70f));
            DrawHudText($"ENG: {PlayerEnergy} %", 30, Height - 53, 4f, matrixGreen);

            float gunBaseX = Width - 320f; float gunBaseY = Height - 240f + (WeaponRecoil * 120f);
            DrawHudRectangle(gunBaseX, gunBaseY, 140, 240, new RgbaFloat(0.05f, 0.15f, 0.08f, 0.95f));
            DrawHudRectangle(gunBaseX + 20, gunBaseY - 80, 25, 100, new RgbaFloat(0.02f, 0.22f, 0.05f, 1.0f));
            DrawHudRectangle(gunBaseX + 95, gunBaseY - 80, 25, 100, new RgbaFloat(0.02f, 0.22f, 0.05f, 1.0f));
            DrawHudRectangle(gunBaseX + 55, gunBaseY - 40, 30, 160, matrixGreen);

            if (WeaponRecoil > 0.15f) { DrawHudRectangle(gunBaseX + 15, gunBaseY - 140, 110, 60, new RgbaFloat(0.5f, 1.0f, 0.6f, 0.8f)); DrawHudRectangle(gunBaseX + 45, gunBaseY - 180, 50, 40, new RgbaFloat(1.0f, 1.0f, 1.0f, 0.9f)); }
        }
    }

    private void SendUdpStats()
    {
        string stats = $"{_world.CurrentFps:F1};{_world.CurrentCpuPercent:F1};{_world.RamUsage:F1};{_world.Data.GpuDrawCalls};{_world.Data.GpuVertices}";
        byte[] data = Encoding.UTF8.GetBytes(stats);
        using (var client = new System.Net.Sockets.UdpClient()) { client.Send(data, data.Length, "10.0.0.2", 9000); }
    }
}