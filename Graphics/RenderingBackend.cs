namespace CyberEngine.Graphics;

/// <summary>
/// Rendering API selected independently from gameplay and world simulation.
/// The default remains Veldrid so the existing game path is unchanged.
/// </summary>
public enum RenderingBackend
{
    Veldrid = 0,
    SilkNetOpenGL = 1
}
