using System;
using Veldrid;
using CyberEngine.Core;

namespace CyberEngine.Scenes;

public class SettingsScene : Scene
{
    private readonly RgbaFloat _matrixGreen = new RgbaFloat(0.0f, 1.0f, 0.2f, 1.0f);
    private readonly RgbaFloat _white = new RgbaFloat(1.0f, 1.0f, 1.0f, 1.0f);
    private int _selectedIndex = 0;
    
    private readonly string[] _menuItems = { 
        "JAKOSC GEOMETRII", "UPSCALING (SKALA RENDEROWANIA)", "SYNCHRONIZACJA PIONOWA (VSYNC)", 
        "ROZDZIELCZOSC EKRANU", "GLOSNOSC GLOWNA", "GLOSNOSC EFEKTOW (SFX)", 
        "CZULOSC SENSORA (MYSZ)", "POLE WIDZENIA (FOV)" 
    };

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
                else if (k.Key == Key.W || k.Key == Key.Up) _selectedIndex = (_selectedIndex - 1 + _menuItems.Length) % _menuItems.Length;
                else if (k.Key == Key.S || k.Key == Key.Down) _selectedIndex = (_selectedIndex + 1) % _menuItems.Length;
                else if (k.Key == Key.A || k.Key == Key.Left) AdjustValue(-1);
                else if (k.Key == Key.D || k.Key == Key.Right) AdjustValue(1);
            }
        }
        engine.ApplyGraphicsSettings();
    }

    private int GetResIndex()
    {
        for (int i = 0; i < _resolutions.Length; i++)
            if (_resolutions[i].W == SystemConfig.ResolutionWidth && _resolutions[i].H == SystemConfig.ResolutionHeight) return i;
        return 2; 
    }

    private void AdjustValue(int direction)
    {
        switch (_selectedIndex)
        {
            case 0: SystemConfig.GraphicsQuality = Math.Clamp(SystemConfig.GraphicsQuality + direction, 0, 2); break;
            case 1: SystemConfig.RenderScale = Math.Clamp(SystemConfig.RenderScale + (direction * 0.25f), 0.25f, 1.0f); break;
            case 2: if (direction != 0) SystemConfig.VSync = !SystemConfig.VSync; break;
            case 3: 
                int idx = Math.Clamp(GetResIndex() + direction, 0, _resolutions.Length - 1);
                SystemConfig.ResolutionWidth = _resolutions[idx].W;
                SystemConfig.ResolutionHeight = _resolutions[idx].H;
                break;
            case 4: SystemConfig.MasterVolume = Math.Clamp(SystemConfig.MasterVolume + (direction * 5), 0, 100); break;
            case 5: SystemConfig.SfxVolume = Math.Clamp(SystemConfig.SfxVolume + (direction * 5), 0, 100); break;
            case 6: SystemConfig.MouseSensitivity = Math.Clamp(SystemConfig.MouseSensitivity + (direction * 0.0005f), 0.0005f, 0.01f); break;
            case 7: SystemConfig.Fov = Math.Clamp(SystemConfig.Fov + (direction * 2f), 60f, 110f); break;
        }
    }

    public override void OnRenderUI(GameEngine engine)
    {
        float midX = engine.Width / 2f; float midY = engine.Height / 2f;
        engine.DrawHudText("HARDWARE MATRIX - KONFIGURACJA", midX - 320, midY - 220, 4f, _matrixGreen);
        
        for (int i = 0; i < _menuItems.Length; i++)
        {
            RgbaFloat color = _selectedIndex == i ? _white : _matrixGreen;
            string prefix = _selectedIndex == i ? ">> " : "   ";
            string valueStr = i switch {
                0 => SystemConfig.GraphicsQuality switch { 0 => "NISKA", 1 => "SREDNIA", 2 => "WYSOKA", _ => "SREDNIA" },
                1 => $"{(int)(SystemConfig.RenderScale * 100)} %",
                2 => SystemConfig.VSync ? "WLACZONE" : "WYLACZONE",
                3 => $"{SystemConfig.ResolutionWidth} X {SystemConfig.ResolutionHeight}",
                4 => $"{SystemConfig.MasterVolume} %",
                5 => $"{SystemConfig.SfxVolume} %",
                6 => $"{SystemConfig.MouseSensitivity:F4}",
                7 => $"{SystemConfig.Fov} STOPNI",
                _ => ""
            };
            engine.DrawHudText($"{prefix}{_menuItems[i]}: {valueStr}", midX - 350, midY - 110 + (i * 35), 2.5f, color);
        }
        engine.DrawHudText("NAWIGACJA: [W/S] | ZMIANA: [A/D] | ZAPIS I POWROT: [ESC]", midX - 420, engine.Height - 50, 2.5f, _matrixGreen);
    }
}
