using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using CyberEngine.Core;
using Veldrid;

namespace CyberEngine.Logic;

public class World
{
    public Vector3 CameraPosition = Vector3.Zero;
    public float CameraYaw = 0f;
    public float CameraPitch = 0f;
    public float WeaponRecoil = 0f;
    public int PlayerEnergy = 100;
    public bool ShowGameplayHud = false;
    public Vector3 CameraForward = Vector3.UnitZ;
    public Vector2 MouseDelta = Vector2.Zero;
    public bool TriggerMuzzleFlash = false;

    public RenderData Data { get; private set; } = new RenderData();

    // Statystyki wydajności
    public double CurrentFps;
    public double CurrentCpuPercent;
    public double RamUsage;
    private double _lastCpuTime = 0;
    private readonly Stopwatch _cpuStopwatch = Stopwatch.StartNew();
    private double _statTimer = 0;
    private int _frameCount = 0;

    private static readonly Dictionary<char, string[]> Font = new()
    {
        ['0'] = ["███", "█ █", "█ █", "█ █", "███"], ['1'] = ["  █", "  █", "  █", "  █", "  █"],
        ['2'] = ["███", "  █", "███", "█  ", "███"], ['3'] = ["███", "  █", "███", "  █", "███"],
        ['4'] = ["█ █", "█ █", "███", "  █", "  █"], ['5'] = ["███", "█  ", "███", "  █", "███"],
        ['6'] = ["███", "█  ", "███", "█ █", "███"], ['7'] = ["███", "  █", "  █", "  █", "  █"],
        ['8'] = ["███", "█ █", "███", "█ █", "███"], ['9'] = ["███", "█ █", "███", "  █", "███"],
        ['C'] = ["███", "█  ", "█  ", "█  ", "███"], ['P'] = ["███", "█ █", "███", "█  ", "█  "],
        ['U'] = ["█ █", "█ █", "█ █", "█ █", "███"], ['R'] = ["███", "█ █", "███", "██ ", "█ █"],
        ['A'] = ["███", "█ █", "███", "█ █", "█ █"], ['M'] = ["█ █", "███", "█ █", "█ █", "█ █"],
        ['G'] = ["███", "█  ", "█ ██", "█ █", "███"], ['F'] = ["███", "█  ", "███", "█  ", "█  "],
        ['S'] = ["███", "█  ", "███", "  █", "███"], ['B'] = ["██ ", "█ █", "██ ", "█ █", "██ "],
        ['T'] = ["███", " █ ", " █ ", " █ ", " █ "], ['H'] = ["█ █", "█ █", "███", "█ █", "█ █"],
        ['E'] = ["███", "█  ", "███", "█  ", "███"], ['I'] = ["███", " █ ", " █ ", " █ ", "███"],
        ['N'] = ["███", "█ █", "█ █", "█ █", "███"], ['%'] = ["█ █", "  █", " █ ", "█  ", "█ █"],
        [':'] = ["   ", " █ ", "   ", " █ ", "   "], ['.'] = ["   ", "   ", "   ", "   ", " █ "],
        [' '] = ["   ", "   ", "   ", "   ", "   "], ['W'] = ["█ █", "█ █", "█ █", "███", "█ █"],
        ['L'] = ["█  ", "█  ", "█  ", "█  ", "███"], ['V'] = ["█ █", "█ █", "█ █", "█ █", " █ "],
        ['D'] = ["██ ", "█ █", "█ █", "█ █", "██ "], ['K'] = ["█ █", "█ █", "██ ", "█ █", "█ █"],
        ['O'] = ["███", "█ █", "█ █", "█ █", "███"], ['Z'] = ["███", "  █", " █ ", "█  ", "███"],
        ['Y'] = ["█ █", "█ █", "███", "  █", "███"], ['>'] = ["█  ", " █ ", "  █", " █ ", "█  "],
        ['-'] = ["   ", "   ", "███", "   ", "   "], ['J'] = ["  █", "  █", "  █", "█ █", "███"]
    };

    public void UpdateLogicStats(double deltaTime)
    {
        _frameCount++;
        _statTimer += deltaTime;
        if (_statTimer >= 0.25)
        {
            CurrentFps = _frameCount / _statTimer;
            double totalCpuTime = Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds;
            double elapsedWallTime = _cpuStopwatch.Elapsed.TotalSeconds;
            if (elapsedWallTime > 0.0)
            {
                CurrentCpuPercent = ((totalCpuTime - _lastCpuTime) / elapsedWallTime) * 100.0 / Environment.ProcessorCount;
                RamUsage = Process.GetCurrentProcess().WorkingSet64 / (1024.0 * 1024.0);
                _lastCpuTime = totalCpuTime;
            }
            _cpuStopwatch.Restart();
            _frameCount = 0;
            _statTimer = 0;
        }
    }

    public void PrepareNextFrame(double currentTime)
    {
        Data.WorldVertexCount = 0;
        Data.HudVertexCount = 0;
        Data.Lanterns.Clear();
        
        Data.Camera.Position = CameraPosition;
        Data.Camera.Forward = CameraForward;
        Data.Camera.Yaw = CameraYaw;
        Data.Camera.Pitch = CameraPitch;
        
        Data.TriggerMuzzleFlash = TriggerMuzzleFlash;
        Data.CurrentTime = currentTime;
    }

    public void RegisterLantern(Vector3 position) { Data.Lanterns.Add(position); }

    private void AddVertex(float x, float y, float z, float nx, float ny, float nz, float u, float v, float matId)
    {
        int i = Data.WorldVertexCount;
        Data.WorldVertices[i++] = x; Data.WorldVertices[i++] = y; Data.WorldVertices[i++] = z;
        Data.WorldVertices[i++] = nx; Data.WorldVertices[i++] = ny; Data.WorldVertices[i++] = nz;
        Data.WorldVertices[i++] = u; Data.WorldVertices[i++] = v; Data.WorldVertices[i++] = matId;
        Data.WorldVertexCount = i;
    }

    public void DrawHorizontalPlane(float x, float y, float z, float width, float depth, float matId, float nx, float ny, float nz)
    {
        float mx = x + width; float mz = z + depth;
        AddVertex(x, y, z, nx, ny, nz, 0f, 0f, matId); AddVertex(x, y, mz, nx, ny, nz, 0f, 1f, matId); AddVertex(mx, y, mz, nx, ny, nz, 1f, 1f, matId);
        AddVertex(mx, y, mz, nx, ny, nz, 1f, 1f, matId); AddVertex(mx, y, z, nx, ny, nz, 1f, 0f, matId); AddVertex(x, y, z, nx, ny, nz, 0f, 0f, matId);
        Data.GpuVertices += 6;
    }

    public void DrawCube(float x, float y, float z, float width, float height, float depth)
    {
        float mx = x + width; float my = y + height; float mz = z + depth;
        AddVertex(x, y, mz, 0f, 0f, 1f, 0f, 1f, 0f); AddVertex(x, my, mz, 0f, 0f, 1f, 0f, 0f, 0f); AddVertex(mx, my, mz, 0f, 0f, 1f, 1f, 0f, 0f);
        AddVertex(mx, my, mz, 0f, 0f, 1f, 1f, 0f, 0f); AddVertex(mx, y, mz, 0f, 0f, 1f, 1f, 1f, 0f); AddVertex(x, y, mz, 0f, 0f, 1f, 0f, 1f, 0f);
        AddVertex(x, y, z, 0f, 0f, -1f, 1f, 1f, 0f); AddVertex(mx, y, z, 0f, 0f, -1f, 0f, 1f, 0f); AddVertex(mx, my, z, 0f, 0f, -1f, 0f, 0f, 0f);
        AddVertex(mx, my, z, 0f, 0f, -1f, 0f, 0f, 0f); AddVertex(x, my, z, 0f, 0f, -1f, 1f, 0f, 0f); AddVertex(x, y, z, 0f, 0f, -1f, 1f, 1f, 0f);
        AddVertex(x, my, z, 0f, 1f, 0f, 0f, 1f, 0f); AddVertex(mx, my, z, 0f, 1f, 0f, 1f, 1f, 0f); AddVertex(mx, my, mz, 0f, 1f, 0f, 1f, 0f, 0f);
        AddVertex(mx, my, mz, 0f, 1f, 0f, 1f, 0f, 0f); AddVertex(x, my, mz, 0f, 1f, 0f, 0f, 0f, 0f); AddVertex(x, my, z, 0f, 1f, 0f, 0f, 1f, 0f);
        AddVertex(x, y, z, 0f, -1f, 0f, 0f, 0f, 0f); AddVertex(x, y, mz, 0f, -1f, 0f, 0f, 1f, 0f); AddVertex(mx, y, mz, 0f, -1f, 0f, 1f, 1f, 0f);
        AddVertex(mx, y, mz, 0f, -1f, 0f, 1f, 1f, 0f); AddVertex(mx, y, z, 0f, -1f, 0f, 1f, 0f, 0f); AddVertex(x, y, z, 0f, -1f, 0f, 0f, 0f, 0f);
        AddVertex(x, y, z, -1f, 0f, 0f, 0f, 1f, 0f); AddVertex(x, my, z, -1f, 0f, 0f, 0f, 0f, 0f); AddVertex(x, my, mz, -1f, 0f, 0f, 1f, 0f, 0f);
        AddVertex(x, my, mz, -1f, 0f, 0f, 1f, 0f, 0f); AddVertex(x, y, mz, -1f, 0f, 0f, 1f, 1f, 0f); AddVertex(x, y, z, -1f, 0f, 0f, 0f, 1f, 0f);
        AddVertex(mx, y, z, 1f, 0f, 0f, 1f, 1f, 0f); AddVertex(mx, y, mz, 1f, 0f, 0f, 0f, 1f, 0f); AddVertex(mx, my, mz, 1f, 0f, 0f, 0f, 0f, 0f);
        AddVertex(mx, my, mz, 1f, 0f, 0f, 0f, 0f, 0f); AddVertex(mx, my, z, 1f, 0f, 0f, 1f, 0f, 0f); AddVertex(mx, y, z, 1f, 0f, 0f, 1f, 1f, 0f);
        Data.GpuVertices += 36;
    }

    private void AddHudVertex(float x, float y, RgbaFloat c)
    {
        int i = Data.HudVertexCount;
        Data.HudVertices[i++] = x; Data.HudVertices[i++] = y;
        Data.HudVertices[i++] = c.R; Data.HudVertices[i++] = c.G; Data.HudVertices[i++] = c.B; Data.HudVertices[i++] = c.A;
        Data.HudVertexCount = i;
    }

    public void DrawHudRectangle(float screenX, float screenY, float width, float height, RgbaFloat color, float resW, float resH)
    {
        float left = (screenX / resW) * 2f - 1f;
        float right = left + (width / resW) * 2f;
        float top = 1f - (screenY / resH) * 2f;
        float bottom = top - (height / resH) * 2f;
        AddHudVertex(left, top, color); AddHudVertex(left, bottom, color); AddHudVertex(right, top, color);
        AddHudVertex(right, top, color); AddHudVertex(left, bottom, color); AddHudVertex(right, bottom, color);
    }

    public void DrawHudText(string text, float startX, float startY, float pixelSize, RgbaFloat color, float resW, float resH)
    {
        float currentX = startX;
        foreach (char c in text)
        {
            if (Font.TryGetValue(char.ToUpper(c), out string[]? lines) && lines != null)
            {
                for (int r = 0; r < 5; r++)
                    for (int col = 0; col < lines[r].Length; col++)
                        if (lines[r][col] != ' ') 
                            DrawHudRectangle(currentX + col * pixelSize, startY + r * pixelSize, pixelSize, pixelSize, color, resW, resH);
                currentX += (lines[0].Length + 1) * pixelSize;
            }
            else currentX += 4 * pixelSize;
        }
    }
}