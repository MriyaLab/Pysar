namespace Pysar.Elements;

public interface IImageCache
{
    byte[]? Get(string key);
    void Set(string key, byte[] data);
}