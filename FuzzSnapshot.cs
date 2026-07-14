using System;
using System.Collections.Generic;
using System.Numerics;
using Veldrid; // Zmień na własną przestrzeń nazw, jeśli używasz autorskiego interfejsu wejść

public class FuzzSnapshot : InputSnapshot
{
    private readonly Random _rng;

    // Wymagane przez interfejs właściwości
    public IReadOnlyList<KeyEvent> KeyEvents { get; }
    public IReadOnlyList<MouseEvent> MouseEvents { get; }
    public IReadOnlyList<char> KeyCharPresses { get; }
    public Vector2 MousePosition { get; }
    public float WheelDelta { get; }

    public FuzzSnapshot(Random rng)
    {
        _rng = rng;

        // 1. ANOMALIE PRZESTRZENNE (Mouse Fuzzing)
        // Generujemy współrzędne drastycznie wykraczające poza granice rozdzielczości (np. -10 000 px)
        float mx = (float)(_rng.NextDouble() * 20000.0 - 10000.0);
        float my = (float)(_rng.NextDouble() * 20000.0 - 10000.0);
        
        // [KRYTYCZNE WSTRZYKNIĘCIE]: 5% szans na wstrzyknięcie wartości NaN (Not a Number) lub Nieskończoności.
        // Jeśli Twój system kamery (np. Quaternion.CreateFromYawPitchRoll) tego nie wyłapie, silnik ulegnie awarii.
        if (_rng.NextDouble() > 0.95) mx = float.NaN;
        if (_rng.NextDouble() > 0.95) my = float.PositiveInfinity;
        
        MousePosition = new Vector2(mx, my);

        // 2. PRZECIĄŻENIE KÓŁKA (Scroll Wheel)
        // Normalny ruch to 1.0 lub -1.0. My wstrzykujemy gigantyczne wartości, by przetestować limity przybliżania (Zoom).
        WheelDelta = (float)(_rng.NextDouble() * 1000.0 - 500.0);

        // 3. SZUM KLAWIATURY (Keyboard Fuzzing)
        // Generujemy od 0 do 10 jednoczesnych zdarzeń wciśnięcia/puszczenia klawiszy w jednej klatce.
        var keyEvents = new List<KeyEvent>();
        int keyPressCount = _rng.Next(0, 11); 
        for (int i = 0; i < keyPressCount; i++)
        {
            // Losujemy klawisz z dostępnej puli (zakładając strukturę Veldrid.Key)
            Key randomKey = (Key)_rng.Next(1, 130); 
            bool isDown = _rng.NextDouble() > 0.5;
            
            // Losowe wciśnięcie modyfikatorów (Ctrl, Shift, Alt)
            ModifierKeys mods = (ModifierKeys)_rng.Next(0, 16); 
            
            keyEvents.Add(new KeyEvent(randomKey, isDown, mods));
        }
        KeyEvents = keyEvents;

        // 4. BEZPIECZEŃSTWO STRUKTURALNE
        // Inicjalizujemy puste listy dla zdarzeń, których nie fuzzujemy, aby uniknąć fałszywych NullReferenceException.
        MouseEvents = new List<MouseEvent>();
        KeyCharPresses = new List<char>();
    }

    // Interfejs wymaga metody sprawdzającej wciśnięcie myszy
    public bool IsMouseDown(MouseButton button)
    {
        // Prawdopodobieństwo 20%, że dowolny sprawdzany przycisk myszy "udaje" wciśnięty
        return _rng.NextDouble() > 0.8;
    }
}