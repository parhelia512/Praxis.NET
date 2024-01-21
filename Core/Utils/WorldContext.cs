﻿using System.Diagnostics;
using System.Reflection;
using MoonTools.ECS;

namespace Praxis.Core;

/// <summary>
/// Wrapper around an ECS world and all of its associated systems
/// </summary>
public class WorldContext : IDisposable
{
    public readonly string Tag;
    public readonly PraxisGame Game;
    public readonly World World;

    private List<PraxisSystem> _systems = new List<PraxisSystem>();
    private DependencyResolver<PraxisSystem> _sysGraph = new DependencyResolver<PraxisSystem>();
    private Dictionary<Type, DependencyResolver<PraxisSystem>.Node> _sysCache = new Dictionary<Type, DependencyResolver<PraxisSystem>.Node>();
    private bool _systemsDirty = false;

    public WorldContext(string tag, PraxisGame game)
    {
        Tag = tag;
        Game = game;
        World = new World();
        _systemsDirty = false;
    }

    public void Update(float dt)
    {
        if (_systemsDirty)
        {
            InstallSystems();
        }

        for (int i = 0; i < _systems.Count; i++)
        {
            _systems[i].Update(dt);
        }

        for (int i = 0; i < _systems.Count; i++)
        {
            _systems[i].LateUpdate(dt);
        }

        World.FinishUpdate();
    }

    public void Draw()
    {
        if (_systemsDirty)
        {
            InstallSystems();
        }

        for (int i = 0; i < _systems.Count; i++)
        {
            _systems[i].Draw();
        }
    }
    
    public void Dispose()
    {
        World.Dispose();
    }

    // Installs pending systems that have been registered with this world context
    private void InstallSystems()
    {
        // somewhat goofy hack: just re-register any systems that were previously registered
        // this way, even if we've called InstallSystems, we can later still register new systems & call InstallSystems again

        foreach (var sys in _systems)
        {
            RegisterSystem(sys);
        }
        _systems.Clear();

        // generate links
        foreach (var kvp in _sysCache)
        {
            var execBefore = kvp.Key.GetCustomAttributes<ExecuteBeforeAttribute>();
            var execAfter = kvp.Key.GetCustomAttributes<ExecuteAfterAttribute>();

            foreach (var dep in execBefore)
            {
                // note: if the system this exec-before dependency specifies is not present, then we just ignore the dependency
                if (_sysCache.ContainsKey(dep.systemType))
                {
                    _sysCache[dep.systemType].AddLink(kvp.Value);
                }
            }

            foreach (var dep in execAfter)
            {
                // note: if the system this exec-after dependency specifies is not present, throw an error b/c the dependency cannot be satisfied
                try
                {
                    kvp.Value.AddLink(_sysCache[dep.systemType]);
                }
                catch(KeyNotFoundException)
                {
                    throw new DependencyException<PraxisSystem>(kvp.Value.payload);
                }
            }
        }

        // resolve dependencies & add to system execution list
        _systems.AddRange(_sysGraph.Resolve());

        _systemsDirty = false;

        Console.WriteLine($"{Tag} resolved execution order:");

        foreach (var sys in _systems)
        {
            Console.WriteLine($"\t{sys.GetType().Name}");
        }
    }

    // register a system to be installed
    internal void RegisterSystem(PraxisSystem system)
    {
        Debug.Assert(system.World == World);

        var node = _sysGraph.AddNode(system);
        _sysCache.Add(system.GetType(), node);

        _systemsDirty = true;
    }
}