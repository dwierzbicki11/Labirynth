using System;
using System.Collections.Generic;
using Veldrid;
using CyberEngine;
using CyberEngine.Entities;

Console.WriteLine("[CyberEngine] Uruchamianie silnika w trybie 3D...");

GraphicsBackend api = GraphicsBackend.OpenGL; 
float cellSize = 2.0f; 
int chunkSize = 15; 

HashSet<Key> trzymaneKlawisze = new HashSet<Key>();
Dictionary<(int x, int z), List<Wall>> zaladowaneChunki = new Dictionary<(int, int), List<Wall>>();

GameEngine engine = new GameEngine();
engine.Initialize("CyberEngine 3D v1.0", 1280, 720, api);

Player player = new Player();
player.Transform.Position = new Core.Math.Vector3(cellSize * 7, 0f, cellSize * 7);
engine.GameObjects.Add(player);

void AktualizujLogikeGry(double deltaTime, InputSnapshot snapshot)
{
    foreach (KeyEvent keyEvent in snapshot.KeyEvents)
    {
        if (keyEvent.Down) trzymaneKlawisze.Add(keyEvent.Key);
        else trzymaneKlawisze.Remove(keyEvent.Key);
    }

    Core.Math.Vector3 kierunekRuchu = Core.Math.Vector3.Zero;
    if (trzymaneKlawisze.Contains(Key.W)) kierunekRuchu.Z = -1f; // Do przodu (w głąb ekranu)
    if (trzymaneKlawisze.Contains(Key.S)) kierunekRuchu.Z = 1f;  // Do tyłu
    if (trzymaneKlawisze.Contains(Key.A)) kierunekRuchu.X = -1f; // W lewo
    if (trzymaneKlawisze.Contains(Key.D)) kierunekRuchu.X = 1f;  // W prawo

    if (kierunekRuchu.Length() > 0)
        player.Velocity = kierunekRuchu.Normalize() * player.Speed;
    else
        player.Velocity = Core.Math.Vector3.Zero;

    player.Transform.Position += player.Velocity * (float)deltaTime;

    // Aktualizujemy pozycję kamery (silnik automatycznie podwiesi ją nad graczem w 3D)
    engine.CameraPosition = player.Transform.Position;

    // NIESKOŃCZONY STREAMER CHUNKÓW
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

    // SYSTEM REAKCJI NA KOLIZJE
    for (int i = 0; i < engine.GameObjects.Count; i++)
    {
        var obj = engine.GameObjects[i];
        if (obj is Wall wall)
        {
            float dx = player.Transform.Position.X - wall.Transform.Position.X;
            float dz = player.Transform.Position.Z - wall.Transform.Position.Z;
            float odleglosc = MathF.Sqrt(dx * dx + dz * dz);
            float wymaganyDystans = player.Radius + wall.Radius;

            if (odleglosc < wymaganyDystans && odleglosc > 0.001f)
            {
                float overlap = wymaganyDystans - odleglosc;
                player.Transform.Position += new Core.Math.Vector3(
                    (dx / odleglosc) * overlap,
                    0f,
                    (dz / odleglosc) * overlap
                );
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

engine.Run(AktualizujLogikeGry);