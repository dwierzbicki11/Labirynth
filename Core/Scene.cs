using System.Collections.Generic;
using CyberEngine.Entities;
using Veldrid;

namespace CyberEngine.Core;

public abstract class Scene
{
    public List<GameObject> GameObjects { get; } = new();

    public virtual void OnLoad(GameEngine engine) { }
    public virtual void OnUpdate(double deltaTime, InputSnapshot snapshot, GameEngine engine) { }
    public virtual void OnRenderUI(GameEngine engine) { }
    public virtual void OnUnload(GameEngine engine)
    {
        GameObjects.Clear();
    }
}