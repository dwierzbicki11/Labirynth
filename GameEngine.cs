#nullable disable

using System;
using System.Collections.Generic;
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
    private readonly System.Net.Sockets.UdpClient _telemetryClient = new System.Net.Sockets.UdpClient();
    
    private readonly byte[] _udpBuffer = new byte[256];

    private Scene _currentScene;
    private Scene _nextScene;
    private bool _exitRequested;

    public bool IsBenchmarkMode { get; set; } = false;
    public bool ExitRequested => _exitRequested;

    public void RequestExit() => _exitRequested = true;

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
    public string TelemetryTargetIp { get; set; } = "127.0.0.1";
    

    public void LoadScene(Scene scene) { _nextScene = scene; }
    public void RegisterLantern(Vector3 position) { _world.RegisterLantern(position); }
    public void DrawHorizontalPlane(float x, float y, float z, float w, float d, float m, float nx, float ny, float nz) => _world.DrawHorizontalPlane(x, y, z, w, d, m, nx, ny, nz);
    
    public void DrawSmoothPlane(float x, float z, float w, float d, float y00, float y10, float y01, float y11, Vector3 n00, Vector3 n10, Vector3 n01, Vector3 n11, float m) 
        => _world.DrawSmoothPlane(x, z, w, d, y00, y10, y01, y11, n00, n10, n01, n11, m);
    
    public void DrawQuad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, float m) => _world.DrawQuad(v0, v1, v2, v3, m);
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
                WindowCreateInfo windowCI = new WindowCreateInfo { X = 0, Y = 0, WindowWidth = width, WindowHeight = height, WindowTitle = title, WindowInitialState = WindowState.FullScreen };
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

    private void ExecuteEnginePipeline(double currentTime, float deltaTime, InputSnapshot snapshot)
    {
        if (_nextScene != null) 
        { 
            _currentScene?.OnUnload(this); 
            _currentScene = _nextScene; 
            _nextScene = null; 
            _currentScene.OnLoad(this); 
        }

        _world.UpdateLogicStats(deltaTime);
        
        _currentScene?.OnUpdate(deltaTime, snapshot, this);

        if (_currentScene != null)
        {
            var objs = _currentScene.GameObjects;
            for (int i = objs.Count - 1; i >= 0; i--)
            {
                var obj = objs[i];
                if (obj.IsDestroyed) objs.RemoveAt(i);
                else obj.Update(deltaTime);
            }
        }

        _world.Data.GpuDrawCalls = 0; 
        _world.Data.GpuVertices = 0;
        _world.PrepareNextFrame(currentTime);
        
        float viewDist = SystemConfig.GraphicsQuality switch { 0 => 16f, 1 => 24f, 2 => 36f, _ => 24f };
        float fXStart = MathF.Floor(CameraPosition.X / 2f) * 2f - viewDist; 
        float fXEnd = MathF.Floor(CameraPosition.X / 2f) * 2f + viewDist;
        float fZStart = MathF.Floor(CameraPosition.Z / 2f) * 2f - viewDist; 
        float fZEnd = MathF.Floor(CameraPosition.Z / 2f) * 2f + viewDist;
        
        for (float x = fXStart; x <= fXEnd; x += 2f)
        {
            for (float z = fZStart; z <= fZEnd; z += 2f) 
            {
                DrawHorizontalPlane(x, 0.0f, z, 2f, 2f, 1.0f, 0f, 1f, 0f); 

                float c00 = World.GetCeilingHeight(x, z);
                float c10 = World.GetCeilingHeight(x + 2f, z);
                float c01 = World.GetCeilingHeight(x, z + 2f);
                float c11 = World.GetCeilingHeight(x + 2f, z + 2f);

                Vector3 cn00 = World.GetCeilingNormal(x, z);
                Vector3 cn10 = World.GetCeilingNormal(x + 2f, z);
                Vector3 cn01 = World.GetCeilingNormal(x, z + 2f);
                Vector3 cn11 = World.GetCeilingNormal(x + 2f, z + 2f);

                DrawSmoothPlane(x, z, 2f, 2f, c00, c10, c01, c11, cn00, cn10, cn01, cn11, 1.0f);
            }
        }
        
        if (ShowGameplayHud || IsBenchmarkMode)
        {
            _world.Draw3DWeapon(CameraPosition, CameraYaw, CameraPitch, WeaponRecoil);
        }

        if (_currentScene != null) 
        {
            var objs = _currentScene.GameObjects;
            int count = objs.Count;
            for (int i = 0; i < count; i++) objs[i].Render(this);
        }

        _currentScene?.OnRenderUI(this);
    }
    
    private void RunStressTest(int seconds)
    {
        IsBenchmarkMode = true;
        Console.WriteLine($"--- [STRESS TEST KINEMATYCZNY ZAINICJOWANY: {seconds} SEKUND] ---");
        Stopwatch sw = Stopwatch.StartNew();
        bool isHeadless = Environment.GetEnvironmentVariable("HEADLESS") == "1";
        float fixedDelta = 1f / 60f;

        while (sw.Elapsed.TotalSeconds < seconds) {
            InputSnapshot snapshot = isHeadless ? null : _window.PumpEvents();
            ExecuteEnginePipeline(sw.Elapsed.TotalSeconds, fixedDelta, snapshot);
            _renderer.DrawFrame(_world.Data, _window, Width, Height, ClearColor, isHeadless);
        }
        
        sw.Stop();
        Console.WriteLine($"--- [STRESS TEST ZAKOŃCZONY POMYŚLNIE] ---");
        
        _currentScene?.OnUnload(this); 
        _renderer.Dispose();
        _telemetryClient.Close();
    }
    
    private void RunFuzzing(int iterations)
    {
        IsBenchmarkMode = true;
        Console.WriteLine($"--- [FUZZING ZAINICJOWANY: {iterations} CYKLI CPU (TRYB EKSTREMALNY - BEZ GRAFIKI)] ---");
        Stopwatch sw = Stopwatch.StartNew(); 
        
        iterations = Math.Clamp(iterations, 1, 10_000_000);
        int logInterval = Math.Max(1, iterations / 10);

        for (int i = 0; i < iterations; i++)
        {
            float corruptDelta = (float)((Random.Shared.NextDouble() * 20.0) - 10.0);
            // Fuzzed input intentionally exercises invalid timing, while the engine
            // remains responsible for clamping simulation time at its public boundary.
            InputSnapshot randomSnapshot = new FuzzSnapshot(Random.Shared);

            ExecuteEnginePipeline(sw.Elapsed.TotalSeconds, corruptDelta, randomSnapshot);
            
            if (i > 0 && i % logInterval == 0)
            {
                double currentSec = sw.Elapsed.TotalSeconds;
                double opsPerSec = i / (currentSec > 0.001 ? currentSec : 0.001); 
                float progress = ((float)i / iterations) * 100f;
                
                Console.WriteLine($"[TELEMETRIA] Fuzzing: {i}/{iterations} ({progress:F0}%) | Czas: {currentSec:F2}s | Wydajność pojedynczego rdzenia: {opsPerSec:F0} Op/s");
            }
        }

        sw.Stop();
        Console.WriteLine($"--- [FUZZING ZAKOŃCZONY. Czas całkowity: {sw.Elapsed.TotalSeconds:F2}s. Stabilność: 100%] ---");
        
        _currentScene?.OnUnload(this); 
        _renderer.Dispose();
        _telemetryClient.Close();
    }

    private void RunBenchmark(int frameCount)
    {
        IsBenchmarkMode = true;
        string profile = SystemConfig.GraphicsQuality switch { 0 => "LOW", 1 => "MED", 2 => "HIGH", _ => "CUSTOM" };
        Console.WriteLine($"--- [BENCHMARK KINEMATYCZNY ZAINICJOWANY: {frameCount} KLATEK | PROFIL: {profile}] ---");
        Stopwatch sw = Stopwatch.StartNew();
        float fixedDelta = 1f / 60f;
        bool isHeadless = Environment.GetEnvironmentVariable("HEADLESS") == "1";
        int logInterval = Math.Max(1, frameCount / 10);

        for (int i = 0; i < frameCount; i++)
        {
            InputSnapshot snapshot = isHeadless ? null : _window.PumpEvents();

            float simTime = i * fixedDelta;
            ExecuteEnginePipeline(simTime, fixedDelta, snapshot);
            _renderer.DrawFrame(_world.Data, _window, Width, Height, ClearColor, isHeadless);

            if (i > 0 && i % logInterval == 0)
            {
                double currentSec = sw.Elapsed.TotalSeconds;
                double currentFps = i / currentSec;
                float progress = ((float)i / frameCount) * 100f;
                Console.WriteLine($"[TELEMETRIA GPU] Klatka: {i}/{frameCount} ({progress:F0}%) | Czas: {currentSec:F2}s | Oszacowanie: {currentFps:F0} FPS");
            }
        }
        
        sw.Stop();
        double avg = sw.Elapsed.TotalMilliseconds / frameCount; double fps = 1000.0 / avg;
        
        string res = $"[PROFIL: {profile,-4}] FPS: {fps,6:F1}  |  Sredni czas klatki: {avg,5:F2} ms\n";
        File.AppendAllText("benchmark_results.txt", res);
        
        Console.WriteLine($"--- [WYNIK ODCZYTANY I ZAPISANY DO LOGU] ---");
        
        _currentScene?.OnUnload(this); 
        _renderer.Dispose();
        _telemetryClient.Close();
        return;
    }

    public void Run(string[] args)
    {
        var argsList = args.ToList();

        int benchIdx = argsList.IndexOf("--benchmark");
        if (benchIdx != -1)
        {
            int frames = 1000;
            if (benchIdx + 1 < argsList.Count && int.TryParse(argsList[benchIdx + 1], out int parsed)) frames = parsed;
            RunBenchmark(frames);
            return;
        }

        int fuzzIdx = argsList.IndexOf("--fuzz-mode");
        if (fuzzIdx != -1)
        {
            int iterations = 50000;
            if (fuzzIdx + 1 < argsList.Count && int.TryParse(argsList[fuzzIdx + 1], out int parsed)) iterations = parsed;
            RunFuzzing(iterations);
            return;
        }

        int stressIdx = argsList.IndexOf("--stress-test");
        if (stressIdx != -1)
        {
            int seconds = 30;
            if (stressIdx + 1 < argsList.Count && int.TryParse(argsList[stressIdx + 1], out int parsed)) seconds = parsed;
            RunStressTest(seconds);
            return;
        }

        IsBenchmarkMode = false; 
        Stopwatch stopwatch = Stopwatch.StartNew();
        double lastTime = 0;
        bool isHeadless = Environment.GetEnvironmentVariable("HEADLESS") == "1";

        while (!_exitRequested && (isHeadless || _window.Exists))
        {
            double currentTime = stopwatch.Elapsed.TotalSeconds;
            float deltaTime = Math.Clamp((float)(currentTime - lastTime), 0.0001f, 0.1f);
            lastTime = currentTime;
            InputSnapshot snapshot = isHeadless ? null : _window.PumpEvents();
            if (_exitRequested || (!isHeadless && !_window.Exists)) break;

            if (!isHeadless && _window.Focused)
            {
                _window.CursorVisible = false; int cx = _window.Width / 2; int cy = _window.Height / 2;
                _world.MouseDelta = new Vector2(snapshot.MousePosition.X - cx, snapshot.MousePosition.Y - cy);
                _window.SetMousePosition(cx, cy);
            }
            else { if (!isHeadless) _window.CursorVisible = true; _world.MouseDelta = Vector2.Zero; }

            ExecuteEnginePipeline(currentTime, deltaTime, snapshot);
            SendUdpStats();
            
            _renderer.DrawFrame(_world.Data, _window, Width, Height, ClearColor, isHeadless);
            _world.TriggerMuzzleFlash = false;

            if (!SystemConfig.VSync && SystemConfig.FpsLimit > 0)
            {
                double targetFrameTime = 1.0 / SystemConfig.FpsLimit;
                while (stopwatch.Elapsed.TotalSeconds - lastTime < targetFrameTime) { System.Threading.Thread.Yield(); }
            }
        }

        _currentScene?.OnUnload(this); 
        _renderer.Dispose();
        _telemetryClient.Close();
    }

    

    private void SendUdpStats()
    {
        string stats = $"{_world.CurrentFps:F1};{_world.CurrentCpuPercent:F1};{_world.RamUsage:F1};{_world.Data.GpuDrawCalls};{_world.Data.GpuVertices}";
        int bytesWritten = Encoding.UTF8.GetBytes(stats, 0, stats.Length, _udpBuffer, 0);
        _telemetryClient.Send(_udpBuffer, bytesWritten, TelemetryTargetIp, 9000);
    }
}