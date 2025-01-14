﻿#if DEBUG
// uncomment this to fall back to safe casts in debug mode
// #define DEBUG_CAST
#endif

namespace Praxis.Core.ECS;

using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public class World
{
    public ReverseSpanEnumerator<Entity> AllEntities => new ReverseSpanEnumerator<Entity>(_allEntities.AsSpan);
    public List<Filter> AllFilters => _filterList;

    private uint _nextId = 0;
    private Stack<uint> _idPool = new Stack<uint>();

    private List<IComponentStorage> _componentDepotList = new List<IComponentStorage>();
    private Dictionary<uint, IComponentStorage> _componentDepot = new Dictionary<uint, IComponentStorage>();
    private List<IRelationStorage> _relationDepotList = new List<IRelationStorage>();
    private Dictionary<uint, IRelationStorage> _relationDepot = new Dictionary<uint, IRelationStorage>();

    private List<IList> _messageDepotList = new List<IList>();
    private Dictionary<uint, IList> _messageDepot = new Dictionary<uint, IList>();

    private Dictionary<uint, object> _singletonComponentStorage = new Dictionary<uint, object>();

    private Dictionary<FilterSignature, Filter> _filters = new Dictionary<FilterSignature, Filter>();
    private List<Filter> _filterList = new List<Filter>();

    private IndexableSet<Entity> _allEntities = new IndexableSet<Entity>();

    public void GetComponentTypes(in Entity entity, IndexableSet<Type> componentTypes)
    {
        foreach (var depot in _componentDepotList)
        {
            if (depot.Contains(entity))
            {
                componentTypes.Add(depot.ComponentType);
            }
        }
    }

    public void SetSingleton<T>(in T data)
        where T : struct
    {
        uint typeId = TypeId.GetTypeId<T>();
        if (_singletonComponentStorage.ContainsKey(typeId))
        {
            Unsafe.Unbox<T>(_singletonComponentStorage[typeId]) = data;
        }
        else
        {
            _singletonComponentStorage[typeId] = data;
        }
    }

    public bool HasSingleton<T>()
        where T : struct
    {
        uint typeId = TypeId.GetTypeId<T>();
        return _singletonComponentStorage.ContainsKey(typeId);
    }

    public T GetSingleton<T>()
        where T : struct
    {
        uint typeId = TypeId.GetTypeId<T>();
        return Unsafe.Unbox<T>(_singletonComponentStorage[typeId]);
    }

    public void Send<T>(in T message)
    {
        GetMessageStorage<T>().Add(message);
    }

    public Entity? FindTaggedEntityInChildren(string tag, in Entity root)
    {
        if (root.Tag == tag) return root;

        if (HasInRelations<ChildOf>(root))
        {
            foreach (var child in GetInRelations<ChildOf>(root))
            {
                var result = FindTaggedEntityInChildren(tag, child);
                if (result != null) return result;
            }
        }

        if (HasInRelations<BelongsTo>(root))
        {
            foreach (var child in GetInRelations<BelongsTo>(root))
            {
                var result = FindTaggedEntityInChildren(tag, child);
                if (result != null) return result;
            }
        }

        return null;
    }

    public Entity CreateEntity(string? tag = null)
    {
        uint id;
        if (_idPool.Count > 0)
        {
            id = _idPool.Pop();
        }
        else
        {
            id = _nextId++;
        }

        var entity = new Entity(id, tag);
        _allEntities.Add(entity);
        return entity;
    }

    public void DestroyEntity(in Entity entity)
    {
        foreach (var comp in _componentDepotList)
        {
            comp.Remove(entity);
        }

        foreach (var relation in _relationDepotList)
        {
            relation.RemoveEntity(entity);
        }

        foreach (var filter in _filterList)
        {
            filter.entitySet.Remove(entity);
        }

        _allEntities.Remove(entity);
        _idPool.Push(entity.ID);
    }

    public void Set<T>(in Entity entity, T data)
        where T : struct
    {
        if (!GetComponentStorage<T>().Set(entity, data))
        {
            UpdateFilters(entity);
        }
    }

    public bool Has<T>(in Entity entity)
        where T : struct
    {
        return GetComponentStorage<T>().Contains(entity);
    }

    public T Get<T>(in Entity entity)
        where T : struct
    {
        return GetComponentStorage<T>().Get(entity);
    }

    public bool Remove<T>(in Entity entity)
        where T : struct
    {
        if (GetComponentStorage<T>().Remove(entity))
        {
            UpdateFilters(entity);
            return true;
        }

        return false;
    }

    public void Relate<T>(in Entity from, in Entity to, in T data)
        where T : struct
    {
        GetRelationStorage<T>().Set(from, to, data);
    }

    public bool HasRelation<T>(in Entity from, in Entity to)
        where T : struct
    {
        return GetRelationStorage<T>().Has(from, to);
    }

    public T GetRelation<T>(in Entity from, in Entity to)
        where T : struct
    {
        return GetRelationStorage<T>().Get(from, to);
    }

    public bool HasOutRelations<T>(in Entity from)
        where T : struct
    {
        return GetRelationStorage<T>().HasOutRelations(from);
    }

    public bool HasInRelations<T>(in Entity to)
        where T : struct
    {
        return GetRelationStorage<T>().HasInRelations(to);
    }

    public ReverseSpanEnumerator<Entity> GetOutRelations<T>(in Entity from)
        where T : struct
    {
        return GetRelationStorage<T>().GetOutRelations(from);
    }

    public ReverseSpanEnumerator<Entity> GetInRelations<T>(in Entity to)
        where T : struct
    {
        return GetRelationStorage<T>().GetInRelations(to);
    }

    public Entity GetFirstOutRelation<T>(in Entity from)
        where T : struct
    {
        return GetRelationStorage<T>().GetFirstOutRelation(from);
    }

    public Entity GetFirstInRelation<T>(in Entity to)
        where T : struct
    {
        return GetRelationStorage<T>().GetFirstInRelation(to);
    }

    public ReverseSpanEnumerator<T> GetMessages<T>()
    {
        var span = CollectionsMarshal.AsSpan(GetMessageStorage<T>());
        return new ReverseSpanEnumerator<T>(span);
    }

    public void PostUpdate()
    {
        foreach (var storage in _messageDepotList)
        {
            storage.Clear();
        }
    }

    internal Filter GetFilter(FilterSignature signature, string? tag = null)
    {
        if (_filters.ContainsKey(signature))
        {
            return _filters[signature];
        }

        var filter = new Filter(this, signature)
        {
            tag = tag
        };
        _filters[signature] = filter;
        _filterList.Add(filter);

        return filter;
    }

    private void UpdateFilters(in Entity entity)
    {
        foreach (var filter in _filterList)
        {
            filter.Check(entity);
        }
    }

    internal bool Has(in Entity entity, uint typeId)
    {
        if (!_componentDepot.ContainsKey(typeId))
        {
            return false;
        }

        return _componentDepot[typeId].Contains(entity);
    }

    private List<T> GetMessageStorage<T>()
    {
        uint typeId = TypeId.GetTypeId<T>();

        if (!_messageDepot.ContainsKey(typeId))
        {
            var storage = new List<T>();
            _messageDepot.Add(typeId, storage);
            _messageDepotList.Add(storage);
        }

        #if DEBUG_CAST
        return (List<T>)_messageDepot[typeId];
        #else
        return Unsafe.As<List<T>>(_messageDepot[typeId]);
        #endif
    }

    private RelationStorage<T> GetRelationStorage<T>()
        where T : struct
    {
        uint typeId = TypeId.GetTypeId<T>();

        if (!_relationDepot.ContainsKey(typeId))
        {
            var storage = new RelationStorage<T>();
            _relationDepot.Add(typeId, storage);
            _relationDepotList.Add(storage);
        }

        #if DEBUG_CAST
        return (RelationStorage<T>)_relationDepot[typeId];
        #else
        return Unsafe.As<RelationStorage<T>>(_relationDepot[typeId]);
        #endif
    }

    private ComponentStorage<T> GetComponentStorage<T>()
        where T : struct
    {
        uint typeId = TypeId.GetTypeId<T>();

        if (!_componentDepot.ContainsKey(typeId))
        {
            var storage = new ComponentStorage<T>();
            _componentDepot.Add(typeId, storage);
            _componentDepotList.Add(storage);
        }

        #if DEBUG_CAST
        return (ComponentStorage<T>)_componentDepot[typeId];
        #else
        return Unsafe.As<ComponentStorage<T>>(_componentDepot[typeId]);
        #endif
    }
}