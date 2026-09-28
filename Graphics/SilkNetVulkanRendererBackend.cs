using System;
using System.Numerics;
using CyberEngine.Core;
using Silk.NET.Vulkan;

namespace CyberEngine.Graphics;

/// <summary>
/// Native Silk.NET Vulkan backend boundary.
/// The Vulkan loader is initialized here first; swapchain/device/render-pass
/// migration is intentionally staged so the existing Veldrid renderer remains
/// a working fallback while gameplay continues to use the same RenderData.
/// </summary>
public sealed class SilkNetVulkanRendererBackend : IRendererBackend
{
    private Vk? _vk;

    public string BackendName => "Silk.NET Vulkan";
    public string DeviceName => _vk is null ? "Silk.NET Vulkan (not initialized)" : "Vulkan loader initialized";
    public bool IsInitialized { get; private set; }

    public void Initialize(int width, int height, bool headless)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Vulkan render dimensions must be positive.");

        if (headless)
        {
            // Headless tests must stay GPU-independent.
            IsInitialized = true;
            return;
        }

        _vk = Vk.GetApi();
        IsInitialized = true;
    }

    public void Render(RenderData data, float width, float height, Vector4 clearColor)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Silk.NET Vulkan backend has not been initialized.");

        if (data is null)
            throw new ArgumentNullException(nameof(data));

        // Next migration stage:
        // 1. create Vulkan instance + surface from the existing SDL window
        // 2. select physical/logical device and queues
        // 3. create swapchain + image views
        // 4. upload RenderData to GPU buffers
        // 5. render world/HUD and synchronize frames in flight
        //
        // Keeping this explicit is intentional: silently dropping RenderData
        // would make a Vulkan backend appear functional when it is not yet so.
        throw new PlatformNotSupportedException(
            "Silk.NET Vulkan loader is initialized, but the swapchain/render path is not migrated yet. Use Veldrid until Vulkan render parity is complete.");
    }

    public void Dispose()
    {
        _vk = null;
        IsInitialized = false;
    }
}
