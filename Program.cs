using System;
using System.Collections.Generic;
using Veldrid;
using CyberEngine;
using CyberEngine.Entities;

Console.WriteLine("[CyberEngine] Ładowanie zoptymalizowanych systemów uzbrojenia FPS...");

try{
GraphicsBackend api = GraphicsBackend.OpenGL; 
float cellSize = 2.0f; 
int chunkSize = 15; 

HashSet<Key> trzymaneKlawisze = new HashSet<Key>();
Dictionary<(int x, int z), List<Wall>> zaladowaneChunki = new Dictionary<(int, int), List<Wall>>();
List<Laser> aktywnePociski = new List<Laser>();

GameEngine engine = new GameEngine();
Console.WriteLine("Wlaczanie silnika");
engine.Initialize("CyberEngine Project Matrix", 800, 600, api);
Console.WriteLine("jhkjhgkhgkh");
Player player = new Player();
player.Transform.Position = new Core.Math.Vector3(cellSize * 7, 0f, cellSize * 7);
engine.GameObjects.Add(player);

void AktualizujLogikeGry(double deltaTime, InputSnapshot snapshot)
{
    Console.WriteLine("Metoda logika");
    // 🔥 ROZWIĄZANIE PUŁAPKI INPUTU: Zmienna rejestrująca impuls strzału
    bool czyStrzelonoWTejKlatce = false;

    foreach (KeyEvent keyEvent in snapshot.KeyEvents)
    {
        if (keyEvent.Down) 
        {
            trzymaneKlawisze.Add(keyEvent.Key);
            // Przechwytujemy sam moment uderzenia w klawisz, zanim nadejdzie sygnał puszczenia
            if (keyEvent.Key == Key.Space) czyStrzelonoWTejKlatce = true;
        }
        else 
        {
            trzymaneKlawisze.Remove(keyEvent.Key);
        }
    }

    // 1. STEROWANIE MYSZKĄ FPP
    float czuloscMyszy = 0.0025f;
    player.Yaw += engine.MouseDelta.X * czuloscMyszy;
    player.Pitch -= engine.MouseDelta.Y * czuloscMyszy;
    player.Pitch = Math.Clamp(player.Pitch, -1.4f, 1.4f);

    // 2. RESPONSYWNA MECHANIKA STRZELANIA 
    if (czyStrzelonoWTejKlatce && player.ShootCooldown <= 0f && player.Energy > 0)
    {
        player.ShootCooldown = 0.15f; // Czas odgórnego przeładowania (szybkostrzelność działa)
        player.WeaponRecoil = 0.25f;  // Siła fizycznego odrzutu Cyber-Blastera
        player.Energy -= 5;           // Koszt strzału z baterii plazmowej
        if (player.Energy < 0) player.Energy = 0;

        engine.TriggerMuzzleFlash = true; // Komunikat dla shadera GPU o wygenerowaniu błysku gazów

        // Pobieramy sferyczny wektor kierunku spojrzenia bezpośrednio z silnika graficznego
        Core.Math.Vector3 pociskDir = new Core.Math.Vector3(engine.CameraForward.X, engine.CameraForward.Y, engine.CameraForward.Z);
        Laser nowyPocisk = new Laser(player.Transform.Position, pociskDir);
        
        aktywnePociski.Add(nowyPocisk);
        engine.GameObjects.Add(nowyPocisk); 
    }

    // Autoregeneracja ogniw energetycznych broni, gdy nie strzelasz
    if (!trzymaneKlawisze.Contains(Key.Space) && player.Energy < 100)
    {
        player.Energy += 1;
    }

    // 3. AKTUALIZACJA I ERADYKACJA POCISKÓW Z PAMIĘCI GPU
    List<Laser> doUsuniecia = new List<Laser>();
    for (int i = 0; i < aktywnePociski.Count; i++)
    {
        var laser = aktywnePociski[i];
        if (laser.LifeTime <= 0f)
        {
            doUsuniecia.Add(laser);
        }
    }
    foreach (var laser in doUsuniecia)
    {
        aktywnePociski.Remove(laser);
        engine.GameObjects.Remove(laser);
    }

    // 4. KIERUNKOWY RUCH RELATYWNY WSAD
    Core.Math.Vector3 forward = new Core.Math.Vector3(MathF.Sin(player.Yaw), 0f, -MathF.Cos(player.Yaw));
    Core.Math.Vector3 right = new Core.Math.Vector3(MathF.Cos(player.Yaw), 0f, MathF.Sin(player.Yaw));

    Core.Math.Vector3 kierunekRuchu = Core.Math.Vector3.Zero;
    if (trzymaneKlawisze.Contains(Key.W)) kierunekRuchu += forward;
    if (trzymaneKlawisze.Contains(Key.S)) kierunekRuchu -= forward;
    if (trzymaneKlawisze.Contains(Key.A)) kierunekRuchu -= right;
    if (trzymaneKlawisze.Contains(Key.D)) kierunekRuchu += right;

    if (kierunekRuchu.Length() > 0)
        player.Velocity = kierunekRuchu.Normalize() * player.Speed;
    else
        player.Velocity = Core.Math.Vector3.Zero;

    player.Transform.Position += player.Velocity * (float)deltaTime;
    engine.CameraPosition = player.Transform.Position;

    // 5. NIESKOŃCZONY STREAMER SEKTORÓW (CHUNKI)
    float chunkWorldSize = chunkSize * cellSize;
    int currentChunkX = (int)MathF.Floor(player.Transform.Position.X / chunkWorldSize);
    int currentChunkZ = (int)MathF.Floor(player.Transform.Position.Z / chunkWorldSize);

    HashSet<(int x, int z)> wymaganeChunki = new HashSet<(int, int)>();
    for (int cx = -1; cx <= 1; cx++)
        for (int cz = -1; cz <= 1; cz++)
            wymaganeChunki.Add((currentChunkX + cx, currentChunkZ + cz));

    List<(int x, int z)> doWyladowania = new List<(int, int)>();
    foreach (var coord in zaladowaneChunki.Keys)
        if (!wymaganeChunki.Contains(coord)) doWyladowania.Add(coord);

    foreach (var coord in doWyladowania)
    {
        foreach (var wall in zaladowaneChunki[coord]) engine.GameObjects.Remove(wall);
        zaladowaneChunki.Remove(coord);
    }

    foreach (var coord in wymaganeChunki)
    {
        if (!zaladowaneChunki.ContainsKey(coord))
        {
            List<Wall> scianyChunku = new List<Wall>();
            bool[,] grid = GenerujSektorMatematyczny(coord.x, coord.z, chunkSize);

            for (int x = 0; x < chunkSize; x++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    if (grid[x, z])
                    {
                        float worldX = (coord.x * chunkSize + x) * cellSize;
                        float worldZ = (coord.z * chunkSize + z) * cellSize;
                        Wall w = new Wall(worldX, worldZ, cellSize);
                        scianyChunku.Add(w);
                        engine.GameObjects.Add(w);
                    }
                }
            }
            zaladowaneChunki[coord] = scianyChunku;
        }
    }

    // 6. DETEKCJA FIZYKI KOLIZJI (Gracz i Pociski Laserowe)
    for (int i = 0; i < engine.GameObjects.Count; i++)
    {
        var obj = engine.GameObjects[i];
        if (obj is Wall wall)
        {
            // Odpychanie gracza od ścian korytarza
            float dx = player.Transform.Position.X - wall.Transform.Position.X;
            float dz = player.Transform.Position.Z - wall.Transform.Position.Z;
            float odleglosc = MathF.Sqrt(dx * dx + dz * dz);
            float wymaganyDystans = player.Radius + wall.Radius;

            if (odleglosc < wymaganyDystans && odleglosc > 0.001f)
            {
                float overlap = wymaganyDystans - odleglosc;
                player.Transform.Position += new Core.Math.Vector3((dx / odleglosc) * overlap, 0f, (dz / odleglosc) * overlap);
            }

            // Destrukcja lecących laserów po kontakcie z przeszkodą statyczną
            for (int j = 0; j < aktywnePociski.Count; j++)
            {
                var laser = aktywnePociski[j];
                float ldx = laser.Transform.Position.X - wall.Transform.Position.X;
                float ldz = laser.Transform.Position.Z - wall.Transform.Position.Z;
                float lodleglosc = MathF.Sqrt(ldx * ldx + ldz * ldz);
                if (lodleglosc < (laser.Radius + wall.Radius))
                {
                    laser.LifeTime = -1f; // Flaga wygaszenia życia pocisku
                }
            }
        }
    }
}

bool[,] GenerujSektorMatematyczny(int cx, int cz, int size)
{
    bool[,] grid = new bool[size, size];
    for (int x = 0; x < size; x++)
        for (int z = 0; z < size; z++)
            grid[x, z] = true;

    int seed = (cx * 73856093) ^ (cz * 83492791);
    Random rng = new Random(seed);

    Stack<(int x, int z)> stack = new Stack<(int, int)>();
    grid[1, 1] = false;
    stack.Push((1, 1));

    while (stack.Count > 0)
    {
        var curr = stack.Peek();
        List<(int x, int z)> neighbors = new List<(int, int)>();

        if (curr.x - 2 > 0 && grid[curr.x - 2, curr.z]) neighbors.Add((curr.x - 2, curr.z));
        if (curr.x + 2 < size - 1 && grid[curr.x + 2, curr.z]) neighbors.Add((curr.x + 2, curr.z));
        if (curr.z - 2 > 0 && grid[curr.x, curr.z - 2]) neighbors.Add((curr.x, curr.z - 2));
        if (curr.z + 2 < size - 1 && grid[curr.x, curr.z + 2]) neighbors.Add((curr.x, curr.z + 2));

        if (neighbors.Count > 0)
        {
            var next = neighbors[rng.Next(neighbors.Count)];
            grid[curr.x + (next.x - curr.x) / 2, curr.z + (next.z - curr.z) / 2] = false;
            grid[next.x, next.z] = false;
            stack.Push(next);
        }
        else
        {
            stack.Pop();
        }
    }

    int mid = size / 2;
    grid[mid, 0] = false; grid[mid, size - 1] = false;
    grid[0, mid] = false; grid[size - 1, mid] = false;
    grid[mid, 1] = false; grid[mid, size - 2] = false;
    grid[1, mid] = false; grid[size - 2, mid] = false;

    return grid;
}
Console.WriteLine("Uruchamianie...");
engine.Run(AktualizujLogikeGry);
}
catch(Exception ex){ Message.error(ex.Message);}