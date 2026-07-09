using System;
using System.Collections.Generic;
using System.Numerics;
using Veldrid;
using CyberEngine.Core;
using CyberEngine.Entities;
using CyberEngine.Logic;

namespace CyberEngine.Scenes;

public class GameScene : Scene
{
    public enum GameState { Playing, Shop, Settings }
    private GameState _currentState = GameState.Playing;

    private Player _player = null!;
    private readonly HashSet<Key> _trzymaneKlawisze = new();
    private readonly HashSet<Key> _lastKeys = new(); 
    private readonly Dictionary<(int x, int z), List<GameObject>> _zaladowaneChunki = new();

    private readonly float _cellSize = 2.0f;
    private double _czasOdOstatniegoSpadkuTTL = 0;
    private double _akumulatorObrazenPulapki = 0;

    private static int _currentLevel = 1;
    private int _zebranePakiety = 0;
    
    private GodDirector _director;
    private AgentInferenceService _agentService;

    private bool IsJustPressed(Key key)
    {
        return _trzymaneKlawisze.Contains(key) && !_lastKeys.Contains(key);
    }

    public override void OnLoad(GameEngine engine)
    {
        engine.ShowGameplayHud = true;
        engine.ClearColor = RgbaFloat.Black;

        if (engine.IsBenchmarkMode)
        {
            _player = new BotPlayer(() => this.GameObjects);
        }
        else
        {
            _player = new Player();
        }
        
        _director = new GodDirector(() => this.GameObjects, _player);
        
        if (engine.IsBenchmarkMode)
        {
            Console.WriteLine("[CyberEngine] [SYSTEM] Aktywacja wieloagentowej struktury AI...");
            _agentService = new AgentInferenceService();
        }
        
        var defaultRifle = new WeaponArchetype {
            Name = "Karabin Szturmowy 5.56",
            MaxAmmoInMagazine = 30,
            DefaultSpareMagazines = 3,
            ReloadTime = 2.0f,
            BaseRecoil = 0.45f,
            FireCooldown = 0.22f
        };
        _player.CurrentWeapon = new WeaponInstance(defaultRifle);
        
        _player.Transform.Position = new Vector3(_cellSize * 1.0f, 0f, _cellSize * 1.0f);
        GameObjects.Add(_player);
        
        if (engine.IsBenchmarkMode) 
        {
            Console.WriteLine("[CyberEngine] [AI] Zdeployowano CyberBossa (Llama-3.2-1B) na rubieży taktycznej.");
            GameObjects.Add(new CyberBoss(10f, 10f, () => this.GameObjects, _player));
            SpawnStalkers(10);
        }
    }

    // ZMIANA: Zgodna sygnatura (GameEngine engine) i integracja bezpiecznego wyłączania AI
    public override void OnUnload(GameEngine engine)
    {
        Console.WriteLine("[SYSTEM] Inicjowanie procedury bezpiecznego zamykania AI...");
        
        bool zamekUzyskany = false;
        try 
        {
            zamekUzyskany = HardwareLock.AINativeLock.Wait(2000); 
            
            if (zamekUzyskany)
            {
                _director?.Dispose();
                _agentService?.Dispose();
            }
            else
            {
                Console.WriteLine("[OSTRZEŻENIE] Wątek AI jest zajęty generowaniem odpowiedzi. Pomijam destrukcję wskaźników C++ aby zapobiec zakleszczeniu głównej pętli.");
            }
            
            // Wywołanie bazowe - czyści GameObjects
            base.OnUnload(engine);
        }
        finally
        {
            if (zamekUzyskany)
            {
                HardwareLock.AINativeLock.Release();
            }
        }
        Console.WriteLine("[SYSTEM] Scena poprawnie zwolniona z pamięci.");
    }

    public override void OnUpdate(double deltaTime, InputSnapshot snapshot, GameEngine engine)
    {
        _director?.Update((float)deltaTime);

        if (!engine.IsBenchmarkMode)
        {
            engine.ShowGameplayHud = true;
            bool czyStrzelonoWTejKlatce = false;
            
            if (snapshot != null)
            {
                foreach (KeyEvent keyEvent in snapshot.KeyEvents)
                {
                    if (keyEvent.Down) _trzymaneKlawisze.Add(keyEvent.Key);
                    else _trzymaneKlawisze.Remove(keyEvent.Key);
                }

                foreach (MouseEvent mouseEvent in snapshot.MouseEvents)
                {
                    if (mouseEvent.MouseButton == MouseButton.Left && mouseEvent.Down) czyStrzelonoWTejKlatce = true;
                }
            }

            if (IsJustPressed(Key.Escape)) 
            { 
                if (_currentState == GameState.Shop || _currentState == GameState.Settings) 
                    _currentState = GameState.Playing;
                else
                {
                    engine.LoadScene(new MainMenuScene()); 
                    return; 
                }
            }

            if (IsJustPressed(Key.B)) 
            {
                _currentState = (_currentState == GameState.Shop) ? GameState.Playing : GameState.Shop;
            }

            if (_currentState == GameState.Shop)
            {
                if (IsJustPressed(Key.Number1) && _player.Money >= 50) { _player.CurrentWeapon.SpareMagazines++; _player.Money -= 50; }
                if (IsJustPressed(Key.Number2) && _player.Money >= 250) { _player.FireRateModifier = 0.7f; _player.Money -= 250; }
                if (IsJustPressed(Key.Number3) && _player.Money >= 500) { _player.MaxEnergy += 50; _player.Energy += 50; _player.Money -= 500; }
            }

            if (_currentState == GameState.Playing)
            {
                float czuloscMyszy = SystemConfig.MouseSensitivity;
                
                if (_player.ShootCooldown > 0f) _player.ShootCooldown -= (float)deltaTime;
                
                if (_player.WeaponRecoil > 0f)
                {
                    float recovery = (float)deltaTime * 1.5f; 
                    _player.WeaponRecoil = MathF.Max(0, _player.WeaponRecoil - recovery);
                    _player.Pitch -= recovery * 0.08f; 
                }

                if (_player.ReloadTimer > 0f)
                {
                    _player.ReloadTimer -= (float)deltaTime;
                    if (_player.ReloadTimer <= 0f)
                    {
                        _player.CurrentWeapon.CurrentMagazineAmmo = _player.CurrentWeapon.Archetype.MaxAmmoInMagazine;
                        _player.ReloadTimer = 0f;
                        Core.Message.ok("[BROŃ] Zamek zwolniony. Przeładowanie zakończone.");
                    }
                }

                _player.Yaw += engine.MouseDelta.X * czuloscMyszy;
                _player.Pitch -= engine.MouseDelta.Y * czuloscMyszy; 
                _player.Pitch = Math.Clamp(_player.Pitch, -1.4f, 1.4f);

                Vector3 forward = new Vector3(MathF.Sin(_player.Yaw), 0f, -MathF.Cos(_player.Yaw));
                Vector3 right = new Vector3(MathF.Cos(_player.Yaw), 0f, MathF.Sin(_player.Yaw));
                Vector3 kierunekRuchu = Vector3.Zero;

                if (_trzymaneKlawisze.Contains(Key.W)) kierunekRuchu += forward;
                if (_trzymaneKlawisze.Contains(Key.S)) kierunekRuchu -= forward;
                if (_trzymaneKlawisze.Contains(Key.A)) kierunekRuchu -= right;
                if (_trzymaneKlawisze.Contains(Key.D)) kierunekRuchu += right;

                if (kierunekRuchu.LengthSquared() > 0) 
                {
                    _player.Velocity = Vector3.Normalize(kierunekRuchu) * _player.Speed;
                    Vector3 movement = _player.Velocity * (float)deltaTime;
                    _player.Transform.Position.X += movement.X;
                    _player.Transform.Position.Z += movement.Z;
                }
                else _player.Velocity = Vector3.Zero;

                float wallHalfSize = _cellSize / 2.0f;
                for (int i = 0; i < GameObjects.Count; i++)
                {
                    if (GameObjects[i] is Wall wall)
                    {
                        float closestX = Math.Clamp(_player.Transform.Position.X, wall.Transform.Position.X - wallHalfSize, wall.Transform.Position.X + wallHalfSize);
                        float closestZ = Math.Clamp(_player.Transform.Position.Z, wall.Transform.Position.Z - wallHalfSize, wall.Transform.Position.Z + wallHalfSize);
                        float dx = _player.Transform.Position.X - closestX;
                        float dz = _player.Transform.Position.Z - closestZ;
                        
                        if ((dx * dx + dz * dz) < (_player.Radius * _player.Radius))
                        {
                            float dist = MathF.Sqrt(dx * dx + dz * dz);
                            if (dist > 0.001f)
                            {
                                _player.Transform.Position.X += (dx / dist) * (_player.Radius - dist) * 0.5f;
                                _player.Transform.Position.Z += (dz / dist) * (_player.Radius - dist) * 0.5f;
                            }
                        }
                    }
                }
                
                _czasOdOstatniegoSpadkuTTL += deltaTime;
                if (_czasOdOstatniegoSpadkuTTL > 1.0)
                {
                    _player.Energy -= 1; 
                    _czasOdOstatniegoSpadkuTTL = 0;
                    
                    if (_player.Energy <= 0) 
                    {
                        _player.Transform.Position = new Vector3(_cellSize * 1.0f, 0f, _cellSize * 1.0f);
                        _player.Energy = 100;
                    }
                }

                for (int i = GameObjects.Count - 1; i >= 0; i--)
                {
                    var obj = GameObjects[i];
                    
                    if (obj is Honeypot hp && !hp.IsDestroyed)
                    {
                        float distSq = Vector3.DistanceSquared(_player.Transform.Position, hp.Transform.Position);
                        if (distSq < hp.Radius * hp.Radius)
                        {
                            _akumulatorObrazenPulapki += deltaTime;
                            if (_akumulatorObrazenPulapki >= 0.033) 
                            {
                                _player.Energy -= 1;
                                _akumulatorObrazenPulapki = 0;
                            }
                        }
                    }
                    else if (obj is DataNode dn && !dn.IsDestroyed)
                    {
                        float distSq = Vector3.DistanceSquared(_player.Transform.Position, dn.Transform.Position);
                        if (distSq < dn.Radius * dn.Radius)
                        {
                            _player.Energy = Math.Min(100, _player.Energy + 25);
                            dn.Destroy(); 
                            
                            _zebranePakiety++;
                            int wymagane = _currentLevel * 5;
                            
                            if (_zebranePakiety >= wymagane)
                            {
                                _currentLevel++;
                                _zebranePakiety = 0;
                                
                                foreach (var chunk in _zaladowaneChunki.Values) 
                                    foreach (var o in chunk) o.Destroy();
                                
                                _zaladowaneChunki.Clear();
                                GameObjects.Clear();
                                GameObjects.Add(_player);
                                SpawnStalkers(_currentLevel * 2);
                                
                                _player.Transform.Position = new Vector3(_cellSize * 1.0f, 0f, _cellSize * 1.0f);
                                Console.WriteLine($"[SYSTEM] Infiltracja warstwy {_currentLevel}. Złożoność algorytmiczna labiryntu zwiększona.");
                                break; 
                            }
                        }
                    }
                }

                if (IsJustPressed(Key.R) && !_player.IsReloading && _player.CurrentWeapon.SpareMagazines > 0 && _player.CurrentWeapon.CurrentMagazineAmmo < _player.CurrentWeapon.Archetype.MaxAmmoInMagazine)
                {
                    _player.ReloadTimer = _player.CurrentWeapon.Archetype.ReloadTime;
                    _player.CurrentWeapon.CurrentMagazineAmmo = 0; 
                    _player.CurrentWeapon.SpareMagazines--;
                    Core.Message.info("[BROŃ] Zrzut taktyczny. Wprowadzanie nowego magazynka...");
                }

                if (czyStrzelonoWTejKlatce && _player.ShootCooldown <= 0f && !_player.IsReloading)
                {
                    if (_player.CurrentWeapon.CanShoot())
                    {
                        Vector3 trueForwardLocal = new Vector3(MathF.Sin(_player.Yaw) * MathF.Cos(_player.Pitch), MathF.Sin(_player.Pitch), -MathF.Cos(_player.Yaw) * MathF.Cos(_player.Pitch));
                        
                        _player.CurrentWeapon.CurrentMagazineAmmo--; 
                        _player.ShootCooldown = _player.CurrentWeapon.Archetype.FireCooldown * _player.FireRateModifier; 
                        _player.WeaponRecoil += _player.CurrentWeapon.Archetype.BaseRecoil; 
                        _player.Pitch += 0.04f; 
                        
                        engine.TriggerMuzzleFlash = true;

                        Vector3 trueRight = Vector3.Normalize(Vector3.Cross(trueForwardLocal, Vector3.UnitY));
                        if (trueRight.LengthSquared() < 0.001f) trueRight = Vector3.UnitX;
                        Vector3 trueUp = Vector3.Cross(trueRight, trueForwardLocal);
                        
                        Vector3 camPos = new Vector3(_player.Transform.Position.X, _player.Transform.Position.Y + 0.8f + _player.CameraBobOffset, _player.Transform.Position.Z);
                        Vector3 barrelPos = camPos + trueForwardLocal * 0.27f + trueRight * 0.06f - trueUp * 0.06f;

                        GameObjects.Add(new Laser(barrelPos, trueForwardLocal));

                        float closestHitDistance = 20.0f; 
                        GameObject hitObject = null;

                        foreach (var obj in GameObjects)
                        {
                            if (obj == _player || obj.IsDestroyed) continue;

                            if (obj is Honeypot || obj is DataNode || obj is Stalker)
                            {
                                if (RayIntersectsSphere(camPos, trueForwardLocal, obj.Transform.Position, obj.Radius, out float dist))
                                {
                                    if (dist < closestHitDistance)
                                    {
                                        closestHitDistance = dist;
                                        hitObject = obj;
                                    }
                                }
                            }
                        }

                        if (hitObject != null)
                        {
                            if (hitObject is Stalker stalker)
                            {
                                stalker.TakeDamage(new Random().Next(10,50));
                                if (stalker.IsDestroyed) _player.Money += new Random().Next(10,100);
                            }
                            else
                            {
                                hitObject.Destroy();
                                if (hitObject is Honeypot) _player.Energy = Math.Min(100, _player.Energy + 10);
                            }
                        }
                    }
                    else
                    {
                        _player.ShootCooldown = 0.5f;
                        Core.Message.warning("[BROŃ] Klik! Pusty magazynek. Wciśnij [R].");
                    }
                }
            }

            engine.CameraYaw = _player.Yaw;
            engine.CameraPitch = _player.Pitch;
            engine.CameraPosition = new Vector3(_player.Transform.Position.X, _player.Transform.Position.Y + 0.8f + _player.CameraBobOffset, _player.Transform.Position.Z);
            engine.WeaponRecoil = _player.WeaponRecoil;
            engine.PlayerEnergy = _player.Energy;

            Vector3 trueForwardEngine = new Vector3(MathF.Sin(_player.Yaw) * MathF.Cos(_player.Pitch), MathF.Sin(_player.Pitch), -MathF.Cos(_player.Yaw) * MathF.Cos(_player.Pitch));
            engine.CameraForward = trueForwardEngine;

            _lastKeys.Clear();
            foreach (var k in _trzymaneKlawisze) _lastKeys.Add(k);
        }
        else
        {
            engine.ShowGameplayHud = false; 
            engine.CameraYaw = _player.Yaw;
            engine.CameraPitch = _player.Pitch;
            engine.CameraPosition = new Vector3(_player.Transform.Position.X, _player.Transform.Position.Y + 0.8f, _player.Transform.Position.Z);
            
            engine.WeaponRecoil = _player.WeaponRecoil;
            Vector3 trueForwardEngine = new Vector3(MathF.Sin(_player.Yaw) * MathF.Cos(_player.Pitch), MathF.Sin(_player.Pitch), -MathF.Cos(_player.Yaw) * MathF.Cos(_player.Pitch));
            engine.CameraForward = trueForwardEngine;
        }

        int chunkSize = SystemConfig.GraphicsQuality switch { 0 => 9, 1 => 13, 2 => 19, _ => 13 };
        float chunkWorldSize = chunkSize * _cellSize;
        int currentChunkX = (int)MathF.Floor(engine.CameraPosition.X / chunkWorldSize);
        int currentChunkZ = (int)MathF.Floor(engine.CameraPosition.Z / chunkWorldSize);

        HashSet<(int x, int z)> wymaganeChunki = new HashSet<(int, int)>();
        for (int cx = -1; cx <= 1; cx++)
            for (int cz = -1; cz <= 1; cz++)
                wymaganeChunki.Add((currentChunkX + cx, currentChunkZ + cz));

        List<(int x, int z)> doWyladowania = new List<(int, int)>();
        foreach (var coord in _zaladowaneChunki.Keys)
            if (!wymaganeChunki.Contains(coord)) doWyladowania.Add(coord);

        foreach (var coord in doWyladowania)
        {
            foreach (var obj in _zaladowaneChunki[coord]) obj.Destroy();
            _zaladowaneChunki.Remove(coord);
        }

        foreach (var coord in wymaganeChunki)
        {
            if (!_zaladowaneChunki.ContainsKey(coord))
            {
                List<GameObject> obiektyChunku = new List<GameObject>();
                bool[,] grid = GenerujSektorMatematyczny(coord.x, coord.z, chunkSize);
                
                Random popRng = new Random((coord.x * 73856) ^ (coord.z * 19274));

                for (int x = 0; x < chunkSize; x++)
                {
                    for (int z = 0; z < chunkSize; z++)
                    {
                        float worldX = (coord.x * chunkSize + x) * _cellSize;
                        float worldZ = (coord.z * chunkSize + z) * _cellSize;

                        if (grid[x, z])
                        {
                            Wall w = new Wall(worldX, worldZ, _cellSize);
                            obiektyChunku.Add(w); GameObjects.Add(w);
                            if (popRng.NextDouble() < 0.1)
                            {
                                float camYaw = 0f;
                                Vector3 camOffset = Vector3.Zero;
                                
                                int r = popRng.Next(4);
                                if (r == 0) { camYaw = 0f; camOffset = new Vector3(0, 0, -1.0f); } 
                                else if (r == 1) { camYaw = 1.57f; camOffset = new Vector3(1.0f, 0, 0); } 
                                else if (r == 2) { camYaw = 3.14f; camOffset = new Vector3(0, 0, 1.0f); } 
                                else { camYaw = -1.57f; camOffset = new Vector3(-1.0f, 0, 0); } 

                                Vector3 camPos = new Vector3(worldX, 2.2f, worldZ) + camOffset;
                                SecurityCamera cam = new SecurityCamera(camPos, camYaw, _player, () => this.GameObjects, _director);
                                obiektyChunku.Add(cam); 
                                GameObjects.Add(cam);
                            }
                        }
                        else
                        {
                            double chance = popRng.NextDouble();
                            if (chance < 0.03) 
                            {
                                Honeypot hp = new Honeypot(worldX, worldZ);
                                obiektyChunku.Add(hp); GameObjects.Add(hp);
                            }
                            else if (chance < 0.05) 
                            {
                                DataNode dn = new DataNode(worldX, worldZ);
                                obiektyChunku.Add(dn); GameObjects.Add(dn);
                            }
                        }
                    }
                }
                _zaladowaneChunki[coord] = obiektyChunku;
            }
        }
    }

    private void SpawnStalkers(int count)
    {
        Random rng = new Random();
        int spawned = 0;
        
        while (spawned < count)
        {
            float x = rng.Next(2, 30) * _cellSize;
            float z = rng.Next(2, 30) * _cellSize;

            if (World.GetCeilingHeight(x, z) > 2.0f)
            {
                var s = new Stalker(x, z);
                s.Target = _player; 
                GameObjects.Add(s);
                spawned++;
            }
        }
        Core.Message.ok($"[AI] Zdeployowano {count} jednostek typu Stalker.");
    }

    private bool[,] GenerujSektorMatematyczny(int cx, int cz, int size)
    {
        bool[,] grid = new bool[size, size];
        for (int x = 0; x < size; x++) for (int z = 0; z < size; z++) grid[x, z] = true;
        
        int seed = (int)(DateTime.Now.Ticks % int.MaxValue) ^ (cx * 12345) ^ (cz * 67890);
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

        double loopChance = Math.Max(0.05, 0.85 - (_currentLevel * 0.15));

        for (int x = 1; x < size - 1; x += 2)
        {
            for (int z = 1; z < size - 1; z += 2)
            {
                if (!grid[x, z]) 
                {
                    int wallCount = 0;
                    if (grid[x - 1, z]) wallCount++;
                    if (grid[x + 1, z]) wallCount++;
                    if (grid[x, z - 1]) wallCount++;
                    if (grid[x, z + 1]) wallCount++;

                    if (wallCount == 3) 
                    {
                        if (rng.NextDouble() <= loopChance) 
                        {
                            List<(int dx, int dz)> punchVectors = new List<(int, int)>();
                            if (x - 2 > 0 && grid[x - 1, z]) punchVectors.Add((-1, 0));
                            if (x + 2 < size - 1 && grid[x + 1, z]) punchVectors.Add((1, 0));
                            if (z - 2 > 0 && grid[x, z - 1]) punchVectors.Add((0, -1));
                            if (z + 2 < size - 1 && grid[x, z + 1]) punchVectors.Add((0, 1));

                            if (punchVectors.Count > 0)
                            {
                                var punch = punchVectors[rng.Next(punchVectors.Count)];
                                grid[x + punch.dx, z + punch.dz] = false;
                            }
                        }
                    }
                }
            }
        }

        int sparsificationPasses = Math.Max(0, (size / 2) - (_currentLevel * 2));
        
        for (int i = 0; i < sparsificationPasses; i++)
        {
            int rx = rng.Next(1, size - 1);
            int rz = rng.Next(1, size - 1);
            
            if (grid[rx, rz])
            {
                bool isHorizontalPartition = !grid[rx - 1, rz] && !grid[rx + 1, rz];
                bool isVerticalPartition = !grid[rx, rz - 1] && !grid[rx, rz + 1];
                
                if (isHorizontalPartition || isVerticalPartition)
                {
                    grid[rx, rz] = false;
                }
            }
        }

        int cross = size / 2;
        if (cross % 2 == 0) cross++;

        grid[cross, 0] = false;          
        grid[cross, size - 1] = false;   
        grid[0, cross] = false;          
        grid[size - 1, cross] = false;   

        return grid;
    }

    private bool RayIntersectsSphere(Vector3 origin, Vector3 dir, Vector3 center, float radius, out float distance)
    {
        distance = float.MaxValue;
        Vector3 L = center - origin;
        float tca = Vector3.Dot(L, dir);
        
        if (tca < 0) return false; 
        
        float d2 = L.LengthSquared() - (tca * tca);
        float radius2 = radius * radius;
        
        if (d2 > radius2) return false; 
        
        float thc = MathF.Sqrt(radius2 - d2);
        distance = tca - thc; 
        
        return true;
    }

    public override void OnRenderUI(GameEngine engine)
    {
        if (!engine.IsBenchmarkMode)
        {
            engine.DrawHudText($"WARSTWA SIECI: {_currentLevel}", 25, 250, 3f, new RgbaFloat(0.0f, 1.0f, 0.2f, 1.0f));
            engine.DrawHudText($"PAKIETY DANYCH: {_zebranePakiety} / {_currentLevel * 5}", 25, 280, 3f, new RgbaFloat(0.0f, 1.0f, 0.2f, 1.0f));
            
            if (_currentState == GameState.Playing)
            {
                engine.DrawHudText($"BROŃ: {_player.CurrentWeapon.Archetype.Name}", 25, engine.Height - 170, 3f, new RgbaFloat(0.0f, 1.0f, 0.2f, 1.0f));
                
                string ammoStatus = _player.IsReloading ? "PRZEŁADOWYWANIE..." : $"{_player.CurrentWeapon.CurrentMagazineAmmo} / {_player.CurrentWeapon.Archetype.MaxAmmoInMagazine}";
                engine.DrawHudText($"AMUNICJA: {ammoStatus}", 25, engine.Height - 140, 3f, new RgbaFloat(0.0f, 1.0f, 0.2f, 1.0f));
                
                engine.DrawHudText($"MAGAZYNKI: {_player.CurrentWeapon.SpareMagazines}", 25, engine.Height - 110, 3f, new RgbaFloat(0.0f, 1.0f, 0.2f, 1.0f));
            }

            if (_currentState == GameState.Shop)
            {
                float shopX = engine.Width / 2f - 150f; 
                float shopY = engine.Height / 2f - 150f; 

                engine.DrawHudText("--- TERMINAL HANDLOWY [B] ---", shopX, shopY, 4f, new RgbaFloat(1, 1, 1, 1));
                engine.DrawHudText($"Twoje Kredyty: {_player.Money}", shopX, shopY + 40, 3f, new RgbaFloat(0, 1, 0, 1));
                engine.DrawHudText("[1] Magazynek (50kr)", shopX, shopY + 80, 3f, new RgbaFloat(1, 1, 1, 1));
                engine.DrawHudText("[2] Szybkostrzelność (250kr)", shopX, shopY + 110, 3f, new RgbaFloat(1, 1, 1, 1));
                engine.DrawHudText("[3] Max Energia (500kr)", shopX, shopY + 140, 3f, new RgbaFloat(1, 1, 1, 1));
            }
        }
    }
}