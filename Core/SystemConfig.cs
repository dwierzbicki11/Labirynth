using System;
using System.IO;
using System.Text.Json;

namespace CyberEngine.Core;

public static class SystemConfig
{
    // DOTYCHCZASOWE
    public static bool VSync = false;
    public static float Fov = 75f;
    public static int MasterVolume = 100;
    public static int SfxVolume = 100;
    public static float MouseSensitivity = 0.0025f;
    public static int ResolutionWidth = 1280;
    public static int ResolutionHeight = 720;
    
    // NOWE USTAWIENIA
    public static float RenderScale = 0.5f; 
    public static int GraphicsQuality = 1; 
    public static bool ShadowsEnabled = true;
    public static bool AnisotropicFiltering = true;
    public static float MotionBlurIntensity = 0.0f;
    public static bool DepthOfFieldEnabled = false;
    public static bool BloomEnabled = true;
    public static bool AmbientOcclusionEnabled = true;
    public static int AntiAliasingMode = 1; // 0: Off, 1: FXAA

    private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "engine_config.json");

    private class ConfigDto
    {
        public bool VSync { get; set; }
        public float Fov { get; set; }
        public int MasterVolume { get; set; }
        public int SfxVolume { get; set; }
        public float MouseSensitivity { get; set; }
        public int ResolutionWidth { get; set; }
        public int ResolutionHeight { get; set; }
        public float RenderScale { get; set; }
        public int GraphicsQuality { get; set; }
        public bool ShadowsEnabled { get; set; }
        public bool BloomEnabled { get; set; }
        public bool AmbientOcclusionEnabled { get; set; }
        public int AntiAliasingMode { get; set; }
    }

    public static void Save()
    {
        var dto = new ConfigDto { 
            VSync = VSync, Fov = Fov, MasterVolume = MasterVolume, 
            SfxVolume = SfxVolume, MouseSensitivity = MouseSensitivity, 
            ResolutionWidth = ResolutionWidth, ResolutionHeight = ResolutionHeight, 
            RenderScale = RenderScale, GraphicsQuality = GraphicsQuality, 
            ShadowsEnabled = ShadowsEnabled, BloomEnabled = BloomEnabled, 
            AmbientOcclusionEnabled = AmbientOcclusionEnabled, AntiAliasingMode = AntiAliasingMode 
        };
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void Load()
    {
        if (!File.Exists(ConfigPath)) { Save(); return; }
        try {
            var dto = JsonSerializer.Deserialize<ConfigDto>(File.ReadAllText(ConfigPath));
            if (dto != null) {
                VSync = dto.VSync; Fov = dto.Fov; MasterVolume = dto.MasterVolume;
                SfxVolume = dto.SfxVolume; MouseSensitivity = dto.MouseSensitivity;
                ResolutionWidth = dto.ResolutionWidth; ResolutionHeight = dto.ResolutionHeight;
                RenderScale = dto.RenderScale; GraphicsQuality = dto.GraphicsQuality;
                ShadowsEnabled = dto.ShadowsEnabled; BloomEnabled = dto.BloomEnabled;
                AmbientOcclusionEnabled = dto.AmbientOcclusionEnabled; AntiAliasingMode = dto.AntiAliasingMode;
            }
        } catch { }
    }
}
