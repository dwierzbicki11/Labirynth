using System;
using Veldrid;
using CyberEngine;
using CyberEngine.Scenes;
using CyberEngine.Core;

Console.WriteLine("[CyberEngine] Inicjalizacja podsystemu VULKAN na układzie ARM64...");

try
{
    SystemConfig.Load();

    // 🔥 Twarde wymuszenie protokołu Vulkan
    GraphicsBackend api = GraphicsBackend.Vulkan; 
    
    GameEngine engine = new GameEngine();
    engine.Initialize("CyberEngine - Matrix Core", 1280, 720, api);
    
    engine.LoadScene(new MainMenuScene());
    engine.Run();
}
catch(Exception ex)
{ 
    Console.WriteLine($"KRYTYCZNY BŁĄD SYSTEMU: {ex.Message}"); 
}
