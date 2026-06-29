using System;
using System.IO;
using System.Text.Json;

namespace CyberEngine.Core;

public static class SystemConfig
{
    public static bool VSync = false;
    public static float Fov = 75f;
    public static int MasterVolume = 100;
    public static int SfxVolume = 100;
    public static float MouseSensitivity = 0.0025f;
    public static int GraphicsQuality = 1; 
    public static int ResolutionWidth = 1280;
    public static int ResolutionHeight = 720;
    
    // Mnożnik rozdzielczości (0.5 = 50%, 1.0 = Natywna)
    public static float RenderScale = 0.5f; 

    private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "engine_config.json");

    private class ConfigDto
    {
        public bool VSync { get; set; }
        public float Fov { get; set; }
        public int MasterVolume { get; set; }
        public int SfxVolume { get; set; }
        public float MouseSensitivity { get; set; }
        public int GraphicsQuality { get; set; }
        public int ResolutionWidth { get; set; }
        public int ResolutionHeight { get; set; }
        public float RenderScale { get; set; }
    }

    public static void Load()
    {
        if (!File.Exists(ConfigPath)) { Save(); return; }
        try
        {
            var dto = JsonSerializer.Deserialize<ConfigDto>(File.ReadAllText(ConfigPath));
            if (dto != null)
            {
                VSync = dto.VSync; Fov = dto.Fov; MasterVolume = dto.MasterVolume;
                SfxVolume = dto.SfxVolume; MouseSensitivity = dto.MouseSensitivity; GraphicsQuality = dto.GraphicsQuality;
                ResolutionWidth = dto.ResolutionWidth > 0 ? dto.ResolutionWidth : 1280;
                ResolutionHeight = dto.ResolutionHeight > 0 ? dto.ResolutionHeight : 720;
                RenderScale = dto.RenderScale > 0.1f ? dto.RenderScale : 0.5f;
            }
        }
        catch (Exception ex) { Message.error(ex.Message); }
    }

    public static void Save()
    {
        try
        {
            var dto = new ConfigDto { 
                VSync = VSync, Fov = Fov, MasterVolume = MasterVolume, 
                SfxVolume = SfxVolume, MouseSensitivity = MouseSensitivity, GraphicsQuality = GraphicsQuality,
                ResolutionWidth = ResolutionWidth, ResolutionHeight = ResolutionHeight, RenderScale = RenderScale
            };
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { Message.error(ex.Message); }
    }
}
