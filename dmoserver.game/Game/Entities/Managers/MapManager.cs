namespace dmoserver.game.Managers;

using System.Collections.Concurrent;
using dmoserver.game.Maps;

public static class MapManager
{
    private static readonly ConcurrentDictionary<int, MapInstance> _maps = new();

    public static MapInstance GetOrCreateMap(int mapId)
    {
        return _maps.GetOrAdd(mapId, id => new MapInstance(id));
    }
}