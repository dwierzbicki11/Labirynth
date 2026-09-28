using System;
using System.Numerics;
using CyberEngine.Core;
using Veldrid;
using Veldrid.Sdl2;

namespace CyberEngine.Graphics;

/// <summary>
/// Compatibility adapter around the existing Veldrid renderer.
/// It deliberately does not alter the simulation/render-data path.
/// </summary>
public sealed class VeldridRendererBackend : IRendererBackend
{
    private readonly Renderer _renderer = new();
    private Sdl2Window? _window;
    private bool _headless;

    public string BackendName => "Veldrid";
    public string DeviceName => _renderer.Device?.DeviceName ?? (_headless ? "Headless GPU" : "Unknown");
    public bool IsInitialized { get; private set; }

    public void Initialize(int width, int height, bool headless)
    {
        _headless = headless;
        // The engine continues to own the SDL/Veldrid window for now.
        // This adapter is intentionally lifecycle-only until the window layer
        // is separated from the renderer.
        IsInitialized = true;
    }

    public void AttachWindow(Sdl2Window? window, int width, int height, GraphicsBackend backend)
    {
        _window = window;
        _renderer.Initialize(window, width, height, backend);
        IsInitialized = true;
    }

    public void Render(RenderData data, float width, float height, Vector4 clearColor)
    {
        var color = new RgbaFloat(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);
        _renderer.DrawFrame(data, _window, width, height, color, _headless);
    }

    public void Dispose()
    {
        _renderer.Dispose();
        IsInitialized = false;
    }
}
