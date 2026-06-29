using System;
using Veldrid;
using CyberEngine;
using CyberEngine.Scenes;
using CyberEngine.Scene;

Console.WriteLine("[CyberEngine] Inicjalizacja FSM...");

try
{
    SystemConfig.Load();
    // Dla środowiska RPi4 domyślnie OpenGLES/OpenGL
    GraphicsBackend api = GraphicsBackend.OpenGL; 
    
    GameEngine engine = new GameEngine();
    engine.Initialize("CyberEngine - Matrix Core", 1280, 720, api);
    
    // Załadowanie maszyny stanów (Ekranu Głównego)
    engine.LoadScene(new MainMenuScene());
    
    // Odpalenie silnika 
    engine.Run();
}
catch(Exception ex)
{ 
    Console.WriteLine($"KRYTYCZNY BLAD: {ex.Message}"); 
}