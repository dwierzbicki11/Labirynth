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
    
    // 0: Niska (RPi4 bez OC), 1: Średnia (RPi4 OC), 2: Wysoka (PC)
    public static int GraphicsQuality = 1; 

    private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "engine_config.json");

    private class ConfigDto
    {
        public bool VSync { get; set; }
        public float Fov { get; set; }
        public int MasterVolume { get; set; }
        public int SfxVolume { get; set; }
        public float MouseSensitivity { get; set; }
        public int GraphicsQuality { get; set; }
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
            }
        }
        catch { }
    }

    public static void Save()
    {
        try
        {
            var dto = new ConfigDto { 
                VSync = VSync, Fov = Fov, MasterVolume = MasterVolume, 
                SfxVolume = SfxVolume, MouseSensitivity = MouseSensitivity, GraphicsQuality = GraphicsQuality 
            };
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
