using System.Collections.Concurrent;

namespace Pysar.Elements;

public class LocalImageCache : IImageCache
{
    private readonly ConcurrentDictionary<string, byte[]> _cache = new();

    public byte[]? Get(string key) => _cache.GetValueOrDefault(key);

    public void Set(string key, byte[] data) => _cache[key] = data;
}