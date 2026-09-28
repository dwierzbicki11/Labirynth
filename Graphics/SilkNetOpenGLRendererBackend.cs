using System;
using System.Numerics;
using CyberEngine.Core;

namespace CyberEngine.Graphics;

/// <summary>
/// Reserved Silk.NET/OpenGL backend boundary.
/// No gameplay code depends on this class; implementation is intentionally
/// staged after the Veldrid adapter so rendering can be migrated incrementally.
/// </summary>
public sealed class SilkNetOpenGLRendererBackend : IRendererBackend
{
    public string BackendName => "Silk.NET OpenGL";
    public string DeviceName => "Silk.NET OpenGL (not initialized)";
    public bool IsInitialized { get; private set; }

    public void Initialize(int width, int height, bool headless)
    {
        if (headless)
        {
            IsInitialized = true;
            return;
        }

        throw new PlatformNotSupportedException(
            "Silk.NET OpenGL backend is staged but not active yet. Keep Veldrid selected until render parity is implemented.");
    }

    public void Render(RenderData data, float width, float height, Vector4 clearColor)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Silk.NET OpenGL backend has not been initialized.");

        // Intentionally empty during the migration phase.
        // RenderData remains the stable contract between gameplay and graphics.
    }

    public void Dispose() => IsInitialized = false;
}
