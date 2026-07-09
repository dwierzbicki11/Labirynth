#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using CyberEngine.Core;
using Veldrid;

namespace CyberEngine.Logic;

public class World
{
    // Optymalizacja przeliczeń trygonometrycznych dla środowiska
    public static float GetCeilingHeight(float x, float z) => 3.0f + MathF.Sin(x * 0.2f + z * 0.4f) * 0.9f;
    
    public static Vector3 GetCeilingNormal(float x, float z)
    {
        // Cache'owanie wartości kosinusa redukuje obciążenie ALU o 50%
        float cosVal = MathF.Cos(x * 0.2f + z * 0.4f);
        return Vector3.Normalize(new Vector3(0.18f * cosVal, -1.0f, 0.36f * cosVal)); 
    }

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

    public double CurrentFps;
    public double CurrentCpuPercent;
    public double RamUsage;
    private double _lastCpuTime = 0;
    private readonly Stopwatch _cpuStopwatch = Stopwatch.StartNew();
    private double _statTimer = 0;
    private int _frameCount = 0;
    
    // Utrzymanie jednej referencji do procesu chroni przed ciągłą alokacją na stercie i obciążeniem Garbage Collectora
    private static readonly Process _currentProcess = Process.GetCurrentProcess();

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
            double totalCpuTime = _currentProcess.TotalProcessorTime.TotalSeconds;
            double elapsedWallTime = _cpuStopwatch.Elapsed.TotalSeconds;
            if (elapsedWallTime > 0.0)
            {
                CurrentCpuPercent = ((totalCpuTime - _lastCpuTime) / elapsedWallTime) * 100.0 / Environment.ProcessorCount;
                RamUsage = _currentProcess.WorkingSet64 / (1024.0 * 1024.0);
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

    // Usunięcie wywołań AddVertex dla podstawowych figur. Optymalizacja poprzez bezpośredni dostęp do tablicy.
    public void DrawHorizontalPlane(float x, float y, float z, float width, float depth, float matId, float nx, float ny, float nz)
    {
        float mx = x + width; float mz = z + depth;
        int i = Data.WorldVertexCount;
        var v = Data.WorldVertices;

        v[i++] = x; v[i++] = y; v[i++] = z; v[i++] = nx; v[i++] = ny; v[i++] = nz; v[i++] = 0f; v[i++] = 0f; v[i++] = matId;
        v[i++] = x; v[i++] = y; v[i++] = mz; v[i++] = nx; v[i++] = ny; v[i++] = nz; v[i++] = 0f; v[i++] = 1f; v[i++] = matId;
        v[i++] = mx; v[i++] = y; v[i++] = mz; v[i++] = nx; v[i++] = ny; v[i++] = nz; v[i++] = 1f; v[i++] = 1f; v[i++] = matId;
        
        v[i++] = mx; v[i++] = y; v[i++] = mz; v[i++] = nx; v[i++] = ny; v[i++] = nz; v[i++] = 1f; v[i++] = 1f; v[i++] = matId;
        v[i++] = mx; v[i++] = y; v[i++] = z; v[i++] = nx; v[i++] = ny; v[i++] = nz; v[i++] = 1f; v[i++] = 0f; v[i++] = matId;
        v[i++] = x; v[i++] = y; v[i++] = z; v[i++] = nx; v[i++] = ny; v[i++] = nz; v[i++] = 0f; v[i++] = 0f; v[i++] = matId;

        Data.WorldVertexCount = i;
        Data.GpuVertices += 6;
    }

    public void DrawSmoothPlane(float x, float z, float width, float depth, 
        float y00, float y10, float y01, float y11, 
        Vector3 n00, Vector3 n10, Vector3 n01, Vector3 n11, float matId)
    {
        float mx = x + width; 
        float mz = z + depth;
        int i = Data.WorldVertexCount;
        var v = Data.WorldVertices;

        v[i++] = x; v[i++] = y00; v[i++] = z; v[i++] = n00.X; v[i++] = n00.Y; v[i++] = n00.Z; v[i++] = 0f; v[i++] = 0f; v[i++] = matId;
        v[i++] = x; v[i++] = y01; v[i++] = mz; v[i++] = n01.X; v[i++] = n01.Y; v[i++] = n01.Z; v[i++] = 0f; v[i++] = 1f; v[i++] = matId;
        v[i++] = mx; v[i++] = y11; v[i++] = mz; v[i++] = n11.X; v[i++] = n11.Y; v[i++] = n11.Z; v[i++] = 1f; v[i++] = 1f; v[i++] = matId;

        v[i++] = mx; v[i++] = y11; v[i++] = mz; v[i++] = n11.X; v[i++] = n11.Y; v[i++] = n11.Z; v[i++] = 1f; v[i++] = 1f; v[i++] = matId;
        v[i++] = mx; v[i++] = y10; v[i++] = z; v[i++] = n10.X; v[i++] = n10.Y; v[i++] = n10.Z; v[i++] = 1f; v[i++] = 0f; v[i++] = matId;
        v[i++] = x; v[i++] = y00; v[i++] = z; v[i++] = n00.X; v[i++] = n00.Y; v[i++] = n00.Z; v[i++] = 0f; v[i++] = 0f; v[i++] = matId;

        Data.WorldVertexCount = i;
        Data.GpuVertices += 6;
    }

    public void DrawQuad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, float matId)
    {
        Vector3 normal = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));
        int i = Data.WorldVertexCount;
        var v = Data.WorldVertices;

        v[i++] = v0.X; v[i++] = v0.Y; v[i++] = v0.Z; v[i++] = normal.X; v[i++] = normal.Y; v[i++] = normal.Z; v[i++] = 0f; v[i++] = 0f; v[i++] = matId;
        v[i++] = v1.X; v[i++] = v1.Y; v[i++] = v1.Z; v[i++] = normal.X; v[i++] = normal.Y; v[i++] = normal.Z; v[i++] = 0f; v[i++] = 1f; v[i++] = matId;
        v[i++] = v2.X; v[i++] = v2.Y; v[i++] = v2.Z; v[i++] = normal.X; v[i++] = normal.Y; v[i++] = normal.Z; v[i++] = 1f; v[i++] = 1f; v[i++] = matId;
        
        v[i++] = v2.X; v[i++] = v2.Y; v[i++] = v2.Z; v[i++] = normal.X; v[i++] = normal.Y; v[i++] = normal.Z; v[i++] = 1f; v[i++] = 1f; v[i++] = matId;
        v[i++] = v3.X; v[i++] = v3.Y; v[i++] = v3.Z; v[i++] = normal.X; v[i++] = normal.Y; v[i++] = normal.Z; v[i++] = 1f; v[i++] = 0f; v[i++] = matId;
        v[i++] = v0.X; v[i++] = v0.Y; v[i++] = v0.Z; v[i++] = normal.X; v[i++] = normal.Y; v[i++] = normal.Z; v[i++] = 0f; v[i++] = 0f; v[i++] = matId;

        Data.WorldVertexCount = i;
        Data.GpuVertices += 6;
    }

    public void DrawMesh(float[] meshData, Matrix4x4 transform, float matIdOverride = -1f, Vector2 uvScroll = default, float uvScale = 1.0f)
    {
        if (meshData == null || meshData.Length == 0) return;
        
        int i = Data.WorldVertexCount;
        var v = Data.WorldVertices;
        int vertexCount = meshData.Length / 9;
        
        Matrix4x4.Invert(transform, out Matrix4x4 normalMatrix);
        normalMatrix = Matrix4x4.Transpose(normalMatrix);

        for (int m = 0; m < meshData.Length; m += 9)
        {
            Vector3 pos = new Vector3(meshData[m], meshData[m+1], meshData[m+2]);
            Vector3 norm = new Vector3(meshData[m+3], meshData[m+4], meshData[m+5]);
            
            pos = Vector3.Transform(pos, transform);
            norm = Vector3.Normalize(Vector3.TransformNormal(norm, normalMatrix));

            v[i++] = pos.X; v[i++] = pos.Y; v[i++] = pos.Z;
            v[i++] = norm.X; v[i++] = norm.Y; v[i++] = norm.Z;
            
            // MATH HACK: Zniekształcamy współrzędne UV modelu i dodajemy wektor przesunięcia w czasie
            v[i++] = (meshData[m+6] * uvScale) + uvScroll.X; 
            v[i++] = (meshData[m+7] * uvScale) + uvScroll.Y;
            
            v[i++] = matIdOverride >= 0f ? matIdOverride : meshData[m+8];
        }
        
        Data.WorldVertexCount = i;
        Data.GpuVertices += vertexCount;
    }

    public void Draw3DWeapon(Vector3 camPos, float yaw, float pitch, float recoil)
    {
        float[] blasterMesh = CyberEngine.Graphics.AssetManager.LoadObj("Models/blaster-g.obj", 2.0f);
        if (blasterMesh == null) return;

        Vector3 forward = new Vector3(MathF.Sin(yaw) * MathF.Cos(pitch), MathF.Sin(pitch), -MathF.Cos(yaw) * MathF.Cos(pitch));
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        if (right.LengthSquared() < 0.001f) right = Vector3.UnitX;
        Vector3 up = Vector3.Cross(right, forward);

        // Zbliżamy broń jeszcze lekko do kamery na wypadek, gdyby ten blaster był krótki
        Vector3 renderCamPos = new Vector3(camPos.X, 0.8f, camPos.Z);
        float offsetForward = 0.15f - (recoil * 0.04f); 
        float offsetRight = 0.07f;
        float offsetDown = 0.06f;

        Vector3 weaponWorldPos = renderCamPos + (right * offsetRight) - (up * offsetDown) + (forward * offsetForward);

        Matrix4x4 transform = Matrix4x4.Identity;
        transform *= Matrix4x4.CreateScale(0.4f); 
        //transform *= Matrix4x4.CreateRotationY(MathF.PI); 

        Matrix4x4 rotMatrix = new Matrix4x4(
            right.X,    right.Y,    right.Z,    0,
            up.X,       up.Y,       up.Z,       0,
            -forward.X, -forward.Y, -forward.Z, 0,
            0,          0,          0,          1
        );
        transform *= rotMatrix;
        transform *= Matrix4x4.CreateTranslation(weaponWorldPos);

        // --- SYSTEM SKINÓW W OPARCIU O ATLAS ---
        // Skalujemy UV modelu do 0.5, aby siatka zmieściła się w dokładnie jednym z 4 kwadrantów.
        float uvScale = 0.5f;

        // Wybór skina: Szary Gunmetal (Zalecany do realizmu broni)
        Vector2 skinOffset = new Vector2(1f, 0.5f);

        DrawMesh(blasterMesh, transform, 1.0f, skinOffset, uvScale);
    }

    public void DrawCube(float x, float y, float z, float width, float height, float depth)
    {
        float mx = x + width; float my = y + height; float mz = z + depth;
        int i = Data.WorldVertexCount;
        var v = Data.WorldVertices;

        v[i++] = x; v[i++] = y; v[i++] = mz; v[i++] = 0f; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f;
        v[i++] = x; v[i++] = my; v[i++] = mz; v[i++] = 0f; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f; v[i++] = 0f; v[i++] = 0f;
        v[i++] = mx; v[i++] = my; v[i++] = mz; v[i++] = 0f; v[i++] = 0f; v[i++] = 1f; v[i++] = 1f; v[i++] = 0f; v[i++] = 0f;
        v[i++] = mx; v[i++] = my; v[i++] = mz; v[i++] = 0f; v[i++] = 0f; v[i++] = 1f; v[i++] = 1f; v[i++] = 0f; v[i++] = 0f;
        v[i++] = mx; v[i++] = y; v[i++] = mz; v[i++] = 0f; v[i++] = 0f; v[i++] = 1f; v[i++] = 1f; v[i++] = 1f; v[i++] = 0f;
        v[i++] = x; v[i++] = y; v[i++] = mz; v[i++] = 0f; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f;

        v[i++] = x; v[i++] = y; v[i++] = z; v[i++] = 0f; v[i++] = 0f; v[i++] = -1f; v[i++] = 1f; v[i++] = 1f; v[i++] = 0f;
        v[i++] = mx; v[i++] = y; v[i++] = z; v[i++] = 0f; v[i++] = 0f; v[i++] = -1f; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f;
        v[i++] = mx; v[i++] = my; v[i++] = z; v[i++] = 0f; v[i++] = 0f; v[i++] = -1f; v[i++] = 0f; v[i++] = 0f; v[i++] = 0f;
        v[i++] = mx; v[i++] = my; v[i++] = z; v[i++] = 0f; v[i++] = 0f; v[i++] = -1f; v[i++] = 0f; v[i++] = 0f; v[i++] = 0f;
        v[i++] = x; v[i++] = my; v[i++] = z; v[i++] = 0f; v[i++] = 0f; v[i++] = -1f; v[i++] = 1f; v[i++] = 0f; v[i++] = 0f;
        v[i++] = x; v[i++] = y; v[i++] = z; v[i++] = 0f; v[i++] = 0f; v[i++] = -1f; v[i++] = 1f; v[i++] = 1f; v[i++] = 0f;

        v[i++] = x; v[i++] = my; v[i++] = z; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f;
        v[i++] = mx; v[i++] = my; v[i++] = z; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f; v[i++] = 1f; v[i++] = 1f; v[i++] = 0f;
        v[i++] = mx; v[i++] = my; v[i++] = mz; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f; v[i++] = 0f;
        v[i++] = mx; v[i++] = my; v[i++] = mz; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f; v[i++] = 0f;
        v[i++] = x; v[i++] = my; v[i++] = mz; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f; v[i++] = 0f; v[i++] = 0f; v[i++] = 0f;
        v[i++] = x; v[i++] = my; v[i++] = z; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f;

        v[i++] = x; v[i++] = y; v[i++] = z; v[i++] = 0f; v[i++] = -1f; v[i++] = 0f; v[i++] = 0f; v[i++] = 0f; v[i++] = 0f;
        v[i++] = x; v[i++] = y; v[i++] = mz; v[i++] = 0f; v[i++] = -1f; v[i++] = 0f; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f;
        v[i++] = mx; v[i++] = y; v[i++] = mz; v[i++] = 0f; v[i++] = -1f; v[i++] = 0f; v[i++] = 1f; v[i++] = 1f; v[i++] = 0f;
        v[i++] = mx; v[i++] = y; v[i++] = mz; v[i++] = 0f; v[i++] = -1f; v[i++] = 0f; v[i++] = 1f; v[i++] = 1f; v[i++] = 0f;
        v[i++] = mx; v[i++] = y; v[i++] = z; v[i++] = 0f; v[i++] = -1f; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f; v[i++] = 0f;
        v[i++] = x; v[i++] = y; v[i++] = z; v[i++] = 0f; v[i++] = -1f; v[i++] = 0f; v[i++] = 0f; v[i++] = 0f; v[i++] = 0f;

        v[i++] = x; v[i++] = y; v[i++] = z; v[i++] = -1f; v[i++] = 0f; v[i++] = 0f; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f;
        v[i++] = x; v[i++] = my; v[i++] = z; v[i++] = -1f; v[i++] = 0f; v[i++] = 0f; v[i++] = 0f; v[i++] = 0f; v[i++] = 0f;
        v[i++] = x; v[i++] = my; v[i++] = mz; v[i++] = -1f; v[i++] = 0f; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f; v[i++] = 0f;
        v[i++] = x; v[i++] = my; v[i++] = mz; v[i++] = -1f; v[i++] = 0f; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f; v[i++] = 0f;
        v[i++] = x; v[i++] = y; v[i++] = mz; v[i++] = -1f; v[i++] = 0f; v[i++] = 0f; v[i++] = 1f; v[i++] = 1f; v[i++] = 0f;
        v[i++] = x; v[i++] = y; v[i++] = z; v[i++] = -1f; v[i++] = 0f; v[i++] = 0f; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f;

        v[i++] = mx; v[i++] = y; v[i++] = z; v[i++] = 1f; v[i++] = 0f; v[i++] = 0f; v[i++] = 1f; v[i++] = 1f; v[i++] = 0f;
        v[i++] = mx; v[i++] = y; v[i++] = mz; v[i++] = 1f; v[i++] = 0f; v[i++] = 0f; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f;
        v[i++] = mx; v[i++] = my; v[i++] = mz; v[i++] = 1f; v[i++] = 0f; v[i++] = 0f; v[i++] = 0f; v[i++] = 0f; v[i++] = 0f;
        v[i++] = mx; v[i++] = my; v[i++] = mz; v[i++] = 1f; v[i++] = 0f; v[i++] = 0f; v[i++] = 0f; v[i++] = 0f; v[i++] = 0f;
        v[i++] = mx; v[i++] = my; v[i++] = z; v[i++] = 1f; v[i++] = 0f; v[i++] = 0f; v[i++] = 1f; v[i++] = 0f; v[i++] = 0f;
        v[i++] = mx; v[i++] = y; v[i++] = z; v[i++] = 1f; v[i++] = 0f; v[i++] = 0f; v[i++] = 1f; v[i++] = 1f; v[i++] = 0f;

        Data.WorldVertexCount = i;
        Data.GpuVertices += 36;
    }

    public void DrawHudRectangle(float screenX, float screenY, float width, float height, RgbaFloat color, float resW, float resH)
    {
        float left = (screenX / resW) * 2f - 1f;
        float right = left + (width / resW) * 2f;
        float top = 1f - (screenY / resH) * 2f;
        float bottom = top - (height / resH) * 2f;
        
        int i = Data.HudVertexCount;
        var v = Data.HudVertices;

        v[i++] = left; v[i++] = top; v[i++] = color.R; v[i++] = color.G; v[i++] = color.B; v[i++] = color.A;
        v[i++] = left; v[i++] = bottom; v[i++] = color.R; v[i++] = color.G; v[i++] = color.B; v[i++] = color.A;
        v[i++] = right; v[i++] = top; v[i++] = color.R; v[i++] = color.G; v[i++] = color.B; v[i++] = color.A;
        v[i++] = right; v[i++] = top; v[i++] = color.R; v[i++] = color.G; v[i++] = color.B; v[i++] = color.A;
        v[i++] = left; v[i++] = bottom; v[i++] = color.R; v[i++] = color.G; v[i++] = color.B; v[i++] = color.A;
        v[i++] = right; v[i++] = bottom; v[i++] = color.R; v[i++] = color.G; v[i++] = color.B; v[i++] = color.A;

        Data.HudVertexCount = i;
    }

    public void DrawHudText(string text, float startX, float startY, float pixelSize, RgbaFloat color, float resW, float resH)
    {
        float currentX = startX;
        foreach (char c in text)
        {
            if (Font.TryGetValue(char.ToUpper(c), out string[] lines) && lines != null)
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