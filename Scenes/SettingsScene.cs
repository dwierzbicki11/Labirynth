using System;
using Veldrid;
using CyberEngine.Core;

namespace CyberEngine.Scenes;

public class SettingsScene : Scene
{
    private readonly RgbaFloat _matrixGreen = new RgbaFloat(0.0f, 1.0f, 0.2f, 1.0f);
    private readonly RgbaFloat _white = new RgbaFloat(1.0f, 1.0f, 1.0f, 1.0f);
    private int _selectedIndex = 0;
    private int _currentTab = 0;

    private readonly string[] _tabs = { "PODSTAWOWE", "GEOMETRIA", "POST-PROCESSING" };

    private readonly (int W, int H)[] _resolutions = {
        (800, 600), (1024, 768), (1280, 720), (1366, 768), (1600, 900), (1920, 1080)
    };

    public override void OnLoad(GameEngine engine)
    {
        engine.ShowGameplayHud = false;
        engine.ClearColor = new RgbaFloat(0.0f, 0.015f, 0.005f, 1.0f);
    }

    public override void OnUpdate(double deltaTime, InputSnapshot snapshot, GameEngine engine)
    {
        foreach (KeyEvent k in snapshot.KeyEvents)
        {
            if (k.Down)
            {
                if (k.Key == Key.Escape) { SystemConfig.Save(); engine.LoadScene(new MainMenuScene()); }
                else if (k.Key == Key.W || k.Key == Key.Up) _selectedIndex = Math.Max(0, _selectedIndex - 1);
                else if (k.Key == Key.S || k.Key == Key.Down) _selectedIndex = Math.Min(GetTabItemCount() - 1, _selectedIndex + 1);
                else if (k.Key == Key.A || k.Key == Key.Left) { if (_currentTab > 0) { _currentTab--; _selectedIndex = 0; } }
                else if (k.Key == Key.D || k.Key == Key.Right)
                {
                    if (_currentTab < _tabs.Length - 1)
                    {
                        _currentTab++; _selectedIndex = 0;
                    }
                    else AdjustValue(1);
                }
                else
                {
                    if (k.Key == Key.Enter) AdjustValue(1);
                    else if (k.Key == Key.LShift) AdjustValue(-1);
                }
            }
        }
        engine.ApplyGraphicsSettings();
    }

    private int GetTabItemCount() => _currentTab switch { 0 => 6, 1 => 5, 2 => 6, _ => 0 };

    private int GetResIndex()
    {
        for (int i = 0; i < _resolutions.Length; i++)
            if (_resolutions[i].W == SystemConfig.ResolutionWidth && _resolutions[i].H == SystemConfig.ResolutionHeight) return i;
        return 2;
    }

    private void AdjustValue(int direction)
    {
        if (_currentTab == 0) // Systemowe
        {
            switch (_selectedIndex)
            {
                case 0:
                    int idx = Math.Clamp(GetResIndex() + direction, 0, _resolutions.Length - 1);
                    SystemConfig.ResolutionWidth = _resolutions[idx].W; SystemConfig.ResolutionHeight = _resolutions[idx].H; break;
                case 1: SystemConfig.MasterVolume = Math.Clamp(SystemConfig.MasterVolume + (direction * 5), 0, 100); break;
                case 2: SystemConfig.SfxVolume = Math.Clamp(SystemConfig.SfxVolume + (direction * 5), 0, 100); break;
                case 3: SystemConfig.MouseSensitivity = Math.Clamp(SystemConfig.MouseSensitivity + (direction * 0.0005f), 0.0005f, 0.01f); break;
                case 4: SystemConfig.Fov = Math.Clamp(SystemConfig.Fov + (direction * 2f), 60f, 110f); break;
                case 5: SystemConfig.VSync = !SystemConfig.VSync; break;
            }
        }
        else if (_currentTab == 1) // Geometria
        {
            switch (_selectedIndex)
            {
                case 0: SystemConfig.GraphicsQuality = Math.Clamp(SystemConfig.GraphicsQuality + direction, 0, 2); break;
                case 1: SystemConfig.RenderScale = Math.Clamp(SystemConfig.RenderScale + (direction * 0.25f), 0.25f, 1.0f); break;
                case 2: SystemConfig.ShadowsEnabled = !SystemConfig.ShadowsEnabled; break;
                case 3: SystemConfig.DrawDistance = Math.Clamp(SystemConfig.DrawDistance + (direction * 32f), 64f, 1024f); break;
                case 4: SystemConfig.AnisotropicFiltering = !SystemConfig.AnisotropicFiltering; break;
            }
        }
        else if (_currentTab == 2) // Post-Processing
        {
            switch (_selectedIndex)
            {
                case 0: SystemConfig.BloomEnabled = !SystemConfig.BloomEnabled; break;
                case 1: SystemConfig.AmbientOcclusionEnabled = !SystemConfig.AmbientOcclusionEnabled; break;
                case 2: SystemConfig.AntiAliasingMode = (SystemConfig.AntiAliasingMode + direction + 2) % 2; break;
                case 3: SystemConfig.MotionBlurIntensity = Math.Clamp(SystemConfig.MotionBlurIntensity + (direction * 0.1f), 0f, 1f); break;
                case 4: SystemConfig.DepthOfFieldEnabled = !SystemConfig.DepthOfFieldEnabled; break;
                case 5: SystemConfig.HardwareUpscale = !SystemConfig.HardwareUpscale; break;
            }
        }
    }

    public override void OnRenderUI(GameEngine engine)
    {
        float midX = engine.Width / 2f; float midY = engine.Height / 2f;
        engine.DrawHudText("HARDWARE MATRIX - KONFIGURACJA", midX - 320, midY - 220, 4f, _matrixGreen);

        // Rysuj zakładki
        for (int t = 0; t < _tabs.Length; t++)
            engine.DrawHudText((_currentTab == t ? ">" : " ") + _tabs[t], midX - 350 + (t * 220), midY - 160, 3f, _currentTab == t ? _white : _matrixGreen);

        // Rysuj listę ustawień dla aktywnej zakładki
        string[] currentItems = _currentTab switch
        {
            0 => new[] { "ROZDZIELCZOSC", "GLOSNOSC GLOWNA", "GLOSNOSC EFEKTOW", "CZULOSC MYSZY", "FOV", "VSYNC" },
            1 => new[] { "JAKOSC GEOMETRII", "RENDER SCALE", "CIENIE", "DRAW DISTANCE", "ANISO FILTERING" },
            2 => new[] { "BLOOM", "AMBIENT OCCLUSION", "ANTI-ALIASING (FXAA)", "MOTION BLUR", "DEPTH OF FIELD", "UPSCALER" },
            _ => new string[0]
        };

        for (int i = 0; i < currentItems.Length; i++)
        {
            RgbaFloat color = _selectedIndex == i ? _white : _matrixGreen;
            string prefix = _selectedIndex == i ? ">> " : "   ";
            string val = GetValueString(_currentTab, i);
            engine.DrawHudText($"{prefix}{currentItems[i]}: {val}", midX - 350, midY - 100 + (i * 35), 2.5f, color);
        }
        engine.DrawHudText("NAWIGACJA: [W/S] | ZAKLADKI: [A/D] | [ESC] ZAPIS", midX - 420, engine.Height - 50, 2.5f, _matrixGreen);
    }

    private string GetValueString(int tab, int item)
    {
        if (tab == 0) return item switch { 0 => $"{SystemConfig.ResolutionWidth}x{SystemConfig.ResolutionHeight}", 1 => $"{SystemConfig.MasterVolume}%", 2 => $"{SystemConfig.SfxVolume}%", 3 => $"{SystemConfig.MouseSensitivity:F4}", 4 => $"{SystemConfig.Fov}", 5 => SystemConfig.VSync ? "WL" : "WYL", _ => "" };
        if (tab == 1) return item switch { 0 => SystemConfig.GraphicsQuality switch { 0 => "NISKA", 1 => "SREDNIA", 2 => "WYSOKA", _ => "X" }, 1 => $"{(int)(SystemConfig.RenderScale * 100)}%", 2 => SystemConfig.ShadowsEnabled ? "WL" : "WYL", 3 => $"{SystemConfig.DrawDistance}", 4 => SystemConfig.AnisotropicFiltering ? "WL" : "WYL", _ => "" };
        if (tab == 2) return item switch { 0 => SystemConfig.BloomEnabled ? "WL" : "WYL", 1 => SystemConfig.AmbientOcclusionEnabled ? "WL" : "WYL", 2 => SystemConfig.AntiAliasingMode == 1 ? "FXAA" : "WYL", 3 => $"{SystemConfig.MotionBlurIntensity:F1}", 4 => SystemConfig.DepthOfFieldEnabled ? "WL" : "WYL", 5 => SystemConfig.HardwareUpscale ? "WL" : "WYL", _ => "" };
        return "";
    }
}
