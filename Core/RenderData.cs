using System;
using System.Collections.Generic;
using System.Numerics;

namespace CyberEngine.Core;

public struct CameraInfo
{
    public Vector3 Position;
    public Vector3 Forward;
    public float Yaw;
    public float Pitch;
}

public class RenderData
{
    // Tablice o stałym rozmiarze (Data-Oriented Design) - niezwykle szybkie dla CPU ARM64
    public float[] WorldVertices = new float[2000000];
    public int WorldVertexCount = 0;

    public float[] HudVertices = new float[500000];
    public int HudVertexCount = 0;

    public List<Vector3> Lanterns = new List<Vector3>();

    public void EnsureWorldCapacity(int requiredFloats)
    {
        if (requiredFloats <= WorldVertices.Length) return;
        int newSize = Math.Max(requiredFloats, WorldVertices.Length * 2);
        Array.Resize(ref WorldVertices, newSize);
    }

    public void EnsureHudCapacity(int requiredFloats)
    {
        if (requiredFloats <= HudVertices.Length) return;
        int newSize = Math.Max(requiredFloats, HudVertices.Length * 2);
        Array.Resize(ref HudVertices, newSize);
    }
    
    public CameraInfo Camera;
    public bool TriggerMuzzleFlash;
    public double CurrentTime;

    // Statystyki do zwrotu
    public int GpuDrawCalls;
    public int GpuVertices;
}