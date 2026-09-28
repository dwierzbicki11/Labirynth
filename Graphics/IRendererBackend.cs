using System;
using System.Numerics;
using CyberEngine.Core;

namespace CyberEngine.Graphics;

/// <summary>
/// Backend-neutral renderer contract. Gameplay only supplies RenderData;
/// API-specific resources stay inside the backend implementation.
/// </summary>
public interface IRendererBackend : IDisposable
{
    string BackendName { get; }
    string DeviceName { get; }
    bool IsInitialized { get; }

    void Initialize(int width, int height, bool headless);
    void Render(RenderData data, float width, float height, Vector4 clearColor);
}
