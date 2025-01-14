﻿namespace Praxis.Core.ECS;

internal interface IComponentStorage
{
    uint ComponentTypeId { get; }
    Type ComponentType { get; }
    bool Remove(in Entity entity);
    bool Contains(in Entity entity);
}

internal class ComponentStorage<T> : IComponentStorage
    where T : struct
{
    public uint ComponentTypeId => TypeId.GetTypeId<T>();
    public Type ComponentType => typeof(T);

    private Dictionary<Entity, T> _componentData = new Dictionary<Entity, T>();

    public bool Set(in Entity entity, T data)
    {
        bool has = _componentData.ContainsKey(entity);
        _componentData[entity] = data;

        return has;
    }

    public bool Contains(in Entity entity)
    {
        return _componentData.ContainsKey(entity);
    }

    public T Get(in Entity entity)
    {
        return _componentData[entity];
    }

    public bool Remove(in Entity entity)
    {
        if (Contains(entity))
        {
            _componentData.Remove(entity);
            return true;
        }

        return false;
    }
}
