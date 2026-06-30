using System;
using System.IO;
using System.Text.Json;

namespace CyberEngine.Core;

public static class SystemConfig
{
    // === PERFORMANCE ===
    public static bool VSync = false;
    public static int FpsLimit = 60;
    public static float RenderScale = 0.5f;

    // === RENDERING & GEOMETRIA ===
    public static int GraphicsQuality = 1; // 0: Low, 1: Med, 2: High
    public static bool ShadowsEnabled = true;
    public static bool AnisotropicFiltering = true;
    public static float DrawDistance = 256f;

    // === POST-PROCESSING ===
    public static float MotionBlurIntensity = 0.0f;
    public static bool DepthOfFieldEnabled = false;
    public static bool BloomEnabled = true;
    public static bool AmbientOcclusionEnabled = true;
    public static int AntiAliasingMode = 1; // 0: Off, 1: FXAA
    public static bool HardwareUpscale = false;

    // === INNE ===
    public static int ResolutionWidth = 1280;
    public static int ResolutionHeight = 720;
    public static float Fov = 75f;
    public static int MasterVolume = 100;
    public static int SfxVolume = 100;
    public static float MouseSensitivity = 0.0025f;

    private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "engine_config.json");

    private class ConfigDto
    {
        public bool VSync { get; set; }
        public int FpsLimit { get; set; }
        public float RenderScale { get; set; }
        public int GraphicsQuality { get; set; }
        public bool ShadowsEnabled { get; set; }
        public bool AnisotropicFiltering { get; set; }
        public float DrawDistance { get; set; }
        public float MotionBlurIntensity { get; set; }
        public bool DepthOfFieldEnabled { get; set; }
        public bool BloomEnabled { get; set; }
        public bool AmbientOcclusionEnabled { get; set; }
        public int AntiAliasingMode { get; set; }
        public int ResolutionWidth { get; set; }
        public int ResolutionHeight { get; set; }
        public float Fov { get; set; }
        public int MasterVolume { get; set; }
        public int SfxVolume { get; set; }
        public float MouseSensitivity { get; set; }
        public bool HardwareUpscale { get; set; }
    }

    public static void Save()
    {
        try 
        {
            var dto = new ConfigDto { 
                VSync = VSync, FpsLimit = FpsLimit, RenderScale = RenderScale, 
                GraphicsQuality = GraphicsQuality, ShadowsEnabled = ShadowsEnabled, 
                AnisotropicFiltering = AnisotropicFiltering, DrawDistance = DrawDistance, 
                MotionBlurIntensity = MotionBlurIntensity, DepthOfFieldEnabled = DepthOfFieldEnabled, 
                BloomEnabled = BloomEnabled, AmbientOcclusionEnabled = AmbientOcclusionEnabled, 
                AntiAliasingMode = AntiAliasingMode, ResolutionWidth = ResolutionWidth, 
                ResolutionHeight = ResolutionHeight, Fov = Fov, MasterVolume = MasterVolume, 
                SfxVolume = SfxVolume, MouseSensitivity = MouseSensitivity,HardwareUpscale = HardwareUpscale
            };
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
        } 
        catch (Exception ex) { Console.WriteLine($"Blad zapisu configu: {ex.Message}"); }
    }

    public static void Load()
    {
        if (!File.Exists(ConfigPath)) { Save(); return; }
        try 
        {
            var dto = JsonSerializer.Deserialize<ConfigDto>(File.ReadAllText(ConfigPath));
            if (dto != null)
            {
                VSync = dto.VSync; FpsLimit = dto.FpsLimit; RenderScale = dto.RenderScale;
                GraphicsQuality = dto.GraphicsQuality; ShadowsEnabled = dto.ShadowsEnabled;
                AnisotropicFiltering = dto.AnisotropicFiltering; DrawDistance = dto.DrawDistance;
                MotionBlurIntensity = dto.MotionBlurIntensity; DepthOfFieldEnabled = dto.DepthOfFieldEnabled;
                BloomEnabled = dto.BloomEnabled; AmbientOcclusionEnabled = dto.AmbientOcclusionEnabled;
                AntiAliasingMode = dto.AntiAliasingMode; ResolutionWidth = dto.ResolutionWidth;
                ResolutionHeight = dto.ResolutionHeight; Fov = dto.Fov; MasterVolume = dto.MasterVolume;
                SfxVolume = dto.SfxVolume; MouseSensitivity = dto.MouseSensitivity;HardwareUpscale = dto.HardwareUpscale;
            }
        } 
        catch (Exception ex) { Console.WriteLine($"Blad odczytu configu: {ex.Message}"); }
    }
}
