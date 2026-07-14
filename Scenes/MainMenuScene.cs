using System;
using System.Linq;
using Veldrid;
using CyberEngine.Core;

namespace CyberEngine.Scenes;

public class MainMenuScene : Scene
{
    private readonly RgbaFloat _matrixGreen = new RgbaFloat(0.0f, 1.0f, 0.2f, 1.0f);
    private readonly RgbaFloat _white = new RgbaFloat(1.0f, 1.0f, 1.0f, 1.0f);
    private int _selectedIndex = 0;

    public override void OnLoad(GameEngine engine)
    {
        engine.ShowGameplayHud = false;
        engine.ClearColor = new RgbaFloat(0.0f, 0.02f, 0.01f, 1.0f);
    }

    public override void OnUpdate(double deltaTime, InputSnapshot snapshot, GameEngine engine)
    {
        if (snapshot != null)
        {
            foreach (KeyEvent k in snapshot.KeyEvents)
            {
                if (k.Down)
                {
                    if (k.Key == Key.W || k.Key == Key.Up) _selectedIndex = (_selectedIndex - 1 + 3) % 3;
                    else if (k.Key == Key.S || k.Key == Key.Down) _selectedIndex = (_selectedIndex + 1) % 3;
                    else if (k.Key == Key.Enter || k.Key == Key.Space)
                    {
                        if (_selectedIndex == 0) engine.LoadScene(new GameScene());
                        else if (_selectedIndex == 1) engine.LoadScene(new SettingsScene());
                        else if (_selectedIndex == 2) Environment.Exit(0);
                    }
                }
            }
        }
    }

    public override void OnRenderUI(GameEngine engine)
    {
        float midX = engine.Width / 2f;
        float midY = engine.Height / 2f;

        engine.DrawHudText("CYBER ENGINE BOOTLOADER", midX - 250, midY - 120, 4f, _matrixGreen);

        RgbaFloat c0 = _selectedIndex == 0 ? _white : _matrixGreen;
        RgbaFloat c1 = _selectedIndex == 1 ? _white : _matrixGreen;
        RgbaFloat c2 = _selectedIndex == 2 ? _white : _matrixGreen;

        engine.DrawHudText((_selectedIndex == 0 ? ">> " : " ") + "1. LOG IN (LABIRYNT)", midX - 220, midY, 3f, c0);
        engine.DrawHudText((_selectedIndex == 1 ? ">> " : " ") + "2. HARDWARE MATRIX", midX - 220, midY + 50, 3f, c1);
        engine.DrawHudText((_selectedIndex == 2 ? ">> " : " ") + "3. DISCONNECT", midX - 220, midY + 100, 3f, c2);
    }
}
