using System;
using System.Collections.Generic;
using Veldrid;
using CyberEngine.Core;
using CyberEngine.Entities;
using System.Numerics;

namespace CyberEngine.Scenes;

public class GameScene : Scene
{
    private Player _player = null!;
    private readonly HashSet<Key> _trzymaneKlawisze = new();
    private readonly Dictionary<(int x, int z), List<Wall>> _zaladowaneChunki = new();
    
    private readonly float _cellSize = 2.0f; 
    private readonly int _chunkSize = 15; 

    public override void OnLoad(GameEngine engine)
    {
        engine.ShowGameplayHud = true;
        engine.ClearColor = RgbaFloat.Black;

        _player = new Player();
        _player.Transform.Position = new Vector3(_cellSize * 7, 0f, _cellSize * 7);
        GameObjects.Add(_player);
    }

    public override void OnUpdate(double deltaTime, InputSnapshot snapshot, GameEngine engine)
    {
        bool czyStrzelonoWTejKlatce = false;
        foreach (KeyEvent keyEvent in snapshot.KeyEvents)
        {
            if (keyEvent.Down) 
            {
                _trzymaneKlawisze.Add(keyEvent.Key);
                if (keyEvent.Key == Key.Space) czyStrzelonoWTejKlatce = true;
            }
            else _trzymaneKlawisze.Remove(keyEvent.Key);
        }

        if (_trzymaneKlawisze.Contains(Key.Escape)) { engine.LoadScene(new MainMenuScene()); return; }

        float czuloscMyszy = SystemConfig.MouseSensitivity;
        _player.Yaw += engine.MouseDelta.X * czuloscMyszy;
        _player.Pitch -= engine.MouseDelta.Y * czuloscMyszy;
        _player.Pitch = Math.Clamp(_player.Pitch, -1.4f, 1.4f);

        engine.CameraYaw = _player.Yaw;
        engine.CameraPitch = _player.Pitch;
        engine.CameraPosition = _player.Transform.Position;
        engine.WeaponRecoil = _player.WeaponRecoil;
        engine.PlayerEnergy = _player.Energy;

        if (czyStrzelonoWTejKlatce && _player.ShootCooldown <= 0f && _player.Energy > 0)
        {
            _player.ShootCooldown = 0.15f; 
            _player.WeaponRecoil = 0.25f;  
            _player.Energy -= 5;           
            if (_player.Energy < 0) _player.Energy = 0;
            engine.TriggerMuzzleFlash = true; 

            Vector3 pociskDir = new Vector3(engine.CameraForward.X, engine.CameraForward.Y, engine.CameraForward.Z);
            GameObjects.Add(new Laser(_player.Transform.Position, pociskDir)); 
        }

        if (!_trzymaneKlawisze.Contains(Key.Space) && _player.Energy < 100) _player.Energy += 1;

        Vector3 forward = new Vector3(MathF.Sin(_player.Yaw), 0f, -MathF.Cos(_player.Yaw));
        Vector3 right = new Vector3(MathF.Cos(_player.Yaw), 0f, MathF.Sin(_player.Yaw));
        Vector3 kierunekRuchu = Vector3.Zero;
        
        if (_trzymaneKlawisze.Contains(Key.W)) kierunekRuchu += forward;
        if (_trzymaneKlawisze.Contains(Key.S)) kierunekRuchu -= forward;
        if (_trzymaneKlawisze.Contains(Key.A)) kierunekRuchu -= right;
        if (_trzymaneKlawisze.Contains(Key.D)) kierunekRuchu += right;

        if (kierunekRuchu.LengthSquared() > 0) _player.Velocity = kierunekRuchu.Normalize() * _player.Speed;
        else _player.Velocity = Vector3.Zero;

        _player.Transform.Position += _player.Velocity * (float)deltaTime;

        float chunkWorldSize = _chunkSize * _cellSize;
        int currentChunkX = (int)MathF.Floor(_player.Transform.Position.X / chunkWorldSize);
        int currentChunkZ = (int)MathF.Floor(_player.Transform.Position.Z / chunkWorldSize);

        HashSet<(int x, int z)> wymaganeChunki = new HashSet<(int, int)>();
        for (int cx = -1; cx <= 1; cx++)
            for (int cz = -1; cz <= 1; cz++)
                wymaganeChunki.Add((currentChunkX + cx, currentChunkZ + cz));

        List<(int x, int z)> doWyladowania = new List<(int, int)>();
        foreach (var coord in _zaladowaneChunki.Keys)
            if (!wymaganeChunki.Contains(coord)) doWyladowania.Add(coord);

        foreach (var coord in doWyladowania)
        {
            foreach (var wall in _zaladowaneChunki[coord]) wall.Destroy();
            _zaladowaneChunki.Remove(coord);
        }

        foreach (var coord in wymaganeChunki)
        {
            if (!_zaladowaneChunki.ContainsKey(coord))
            {
                List<Wall> scianyChunku = new List<Wall>();
                bool[,] grid = GenerujSektorMatematyczny(coord.x, coord.z, _chunkSize);

                for (int x = 0; x < _chunkSize; x++)
                {
                    for (int z = 0; z < _chunkSize; z++)
                    {
                        if (grid[x, z])
                        {
                            float worldX = (coord.x * _chunkSize + x) * _cellSize;
                            float worldZ = (coord.z * _chunkSize + z) * _cellSize;
                            Wall w = new Wall(worldX, worldZ, _cellSize);
                            scianyChunku.Add(w);
                            GameObjects.Add(w);
                        }
                    }
                }
                _zaladowaneChunki[coord] = scianyChunku;
            }
        }

        // Optymalizacja CPU: Kolizje z użyciem dystansu do kwadratu
        for (int i = 0; i < GameObjects.Count; i++)
        {
            if (GameObjects[i] is Wall wall)
            {
                float dx = _player.Transform.Position.X - wall.Transform.Position.X;
                float dz = _player.Transform.Position.Z - wall.Transform.Position.Z;
                float distSq = dx * dx + dz * dz;
                float reqDist = _player.Radius + wall.Radius;

                if (distSq < (reqDist * reqDist) && distSq > 0.001f)
                {
                    float odleglosc = MathF.Sqrt(distSq); // Pierwiastek tylko przy samej kolizji
                    float overlap = reqDist - odleglosc;
                    _player.Transform.Position += new MyEngine.Core.Math.Vector3((dx / odleglosc) * overlap, 0f, (dz / odleglosc) * overlap);
                }

                for (int j = 0; j < GameObjects.Count; j++)
                {
                    if (GameObjects[j] is Laser laser && !laser.IsDestroyed)
                    {
                        float ldx = laser.Transform.Position.X - wall.Transform.Position.X;
                        float ldz = laser.Transform.Position.Z - wall.Transform.Position.Z;
                        float lDistSq = ldx * ldx + ldz * ldz;
                        float lReqDist = laser.Radius + wall.Radius;
                        if (lDistSq < (lReqDist * lReqDist)) laser.Destroy();
                    }
                }
            }
        }
    }

    private bool[,] GenerujSektorMatematyczny(int cx, int cz, int size)
    {
        bool[,] grid = new bool[size, size];
        for (int x = 0; x < size; x++)
            for (int z = 0; z < size; z++) grid[x, z] = true;

        int seed = (cx * 73856093) ^ (cz * 83492791);
        Random rng = new Random(seed);

        Stack<(int x, int z)> stack = new Stack<(int, int)>();
        grid[1, 1] = false; stack.Push((1, 1));

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
            else stack.Pop();
        }

        int mid = size / 2;
        grid[mid, 0] = false; grid[mid, size - 1] = false; grid[0, mid] = false; grid[size - 1, mid] = false;
        grid[mid, 1] = false; grid[mid, size - 2] = false; grid[1, mid] = false; grid[size - 2, mid] = false;
        return grid;
    }
}