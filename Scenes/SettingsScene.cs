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
        "SYNCHRONIZACJA PIONOWA (VSYNC)", "GLOSNOSC GLOWNA", "GLOSNOSC EFEKTOW (SFX)", "CZULOSC SENSORA (MYSZ)", "POLE WIDZENIA (FOV)" 
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
                if (k.Key == Key.Escape) 
                {
                    SystemConfig.Save(); 
                    engine.LoadScene(new MainMenuScene());
                }
                else if (k.Key == Key.W || k.Key == Key.Up) _selectedIndex = (_selectedIndex - 1 + _menuItems.Length) % _menuItems.Length;
                else if (k.Key == Key.S || k.Key == Key.Down) _selectedIndex = (_selectedIndex + 1) % _menuItems.Length;
                else if (k.Key == Key.A || k.Key == Key.Left) AdjustValue(-1);
                else if (k.Key == Key.D || k.Key == Key.Right) AdjustValue(1);
            }
        }
        engine.ApplyGraphicsSettings();
    }

    private void AdjustValue(int direction)
    {
        switch (_selectedIndex)
        {
            case 0: if (direction != 0) SystemConfig.VSync = !SystemConfig.VSync; break;
            case 1: SystemConfig.MasterVolume = Math.Clamp(SystemConfig.MasterVolume + (direction * 5), 0, 100); break;
            case 2: SystemConfig.SfxVolume = Math.Clamp(SystemConfig.SfxVolume + (direction * 5), 0, 100); break;
            case 3: SystemConfig.MouseSensitivity = Math.Clamp(SystemConfig.MouseSensitivity + (direction * 0.0005f), 0.0005f, 0.01f); break;
            case 4: SystemConfig.Fov = Math.Clamp(SystemConfig.Fov + (direction * 2f), 60f, 110f); break;
        }
    }

    public override void OnRenderUI(GameEngine engine)
    {
        float midX = engine.Width / 2f; float midY = engine.Height / 2f;
        engine.DrawHudText("HARDWARE MATRIX - KONFIGURACJA", midX - 320, midY - 180, 4f, _matrixGreen);
        for (int i = 0; i < _menuItems.Length; i++)
        {
            RgbaFloat color = _selectedIndex == i ? _white : _matrixGreen;
            string prefix = _selectedIndex == i ? ">> " : "   ";
            string valueStr = i switch {
                0 => SystemConfig.VSync ? "WLACZONE" : "WYLACZONE",
                1 => $"{SystemConfig.MasterVolume} %",
                2 => $"{SystemConfig.SfxVolume} %",
                3 => $"{SystemConfig.MouseSensitivity:F4}",
                4 => $"{SystemConfig.Fov} STOPNI",
                _ => ""
            };
            engine.DrawHudText($"{prefix}{_menuItems[i]}: {valueStr}", midX - 340, midY - 60 + (i * 45), 2.5f, color);
        }
        engine.DrawHudText("NAWIGACJA: [W/S] | ZMIANA: [A/D] | ZAPIS I POWROT: [ESC]", midX - 420, engine.Height - 50, 2.5f, _matrixGreen);
    }
}
