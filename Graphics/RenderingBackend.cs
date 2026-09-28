namespace CyberEngine.Graphics;

/// <summary>
/// Rendering API selected independently from gameplay and world simulation.
/// Veldrid remains the stable fallback while the native Silk.NET Vulkan backend
/// is migrated in incrementally.
/// </summary>
public enum RenderingBackend
{
    Veldrid = 0,
    SilkNetVulkan = 1
}
