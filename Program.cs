#nullable disable

using System;
using System.Linq;
using System.Globalization;
using Veldrid;
using CyberEngine;
using CyberEngine.Scenes;
using CyberEngine.Core;
using System.Runtime.InteropServices;

class Program
{
    static void Main(string[] args)
    {
        try
        {      
            SystemConfig.Load();
            GraphicsBackend api = GraphicsBackend.Vulkan;
            GameEngine engine = new GameEngine();
            
            engine.Initialize("CyberEngine - Matrix Core", SystemConfig.ResolutionWidth, SystemConfig.ResolutionHeight, api);
            engine.Run(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine("\n========================================");
            Console.WriteLine($"KRYTYCZNY BŁĄD SYSTEMU: {ex.Message}");
            
            if (ex.InnerException != null)
            {
                Console.WriteLine($"\n--- UKRYTY BŁĄD NATYWNY (InnerException) ---");
                Console.WriteLine($"Wiadomość: {ex.InnerException.Message}");
                Console.WriteLine($"Typ: {ex.InnerException.GetType().Name}");
                Console.WriteLine($"Ślad: {ex.InnerException.StackTrace}");
                Console.WriteLine($"--------------------------------------------\n");
            }

            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            Console.WriteLine("========================================\n");
            Environment.Exit(1);
        }
    }
}