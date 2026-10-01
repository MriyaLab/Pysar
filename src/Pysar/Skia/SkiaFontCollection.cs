using System.Diagnostics.CodeAnalysis;
using Pysar.Core.Abstractions;
using Pysar.Core.Enums;
using SkiaSharp;

namespace Pysar.Skia;

/// <summary>
///     An <see cref="IFontCollection"/> that materialises typefaces from an <see cref="IFileSystem"/>,
///     keyed the way <c>FontCache</c> looks them up: family name and style.
/// </summary>
/// <remarks>
///     Typefaces are loaded eagerly, when the font is registered, so <c>AddFont</c> reports a missing
///     or undecodable file at the point the host registers it rather than at the first glyph that
///     needs it. <c>FontCache</c> reads this collection on every miss, so registering a font after
///     rendering has begun still takes effect.
/// </remarks>
public sealed class SkiaFontCollection : Dictionary<string, object>, IFontCollection
{
    private readonly IFileSystem _fileSystem;
    private Func<string, FontStyle, SKTypeface?>? _resolveFallback;

    public SkiaFontCollection(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);

        _fileSystem = fileSystem;
    }

    /// <summary>The key a typeface is stored under: <c>family|style</c>.</summary>
    public static string GetCacheKey(string fontName, FontStyle fontStyle) => $"{fontName}|{fontStyle}";

    public IFontCollection AddFont(string filename, string? alias = null, FontStyle fontStyle = FontStyle.Normal)
    {
        ArgumentException.ThrowIfNullOrEmpty(filename);

        var key = GetCacheKey(alias ?? filename, fontStyle);
        if (ContainsKey(key))
            return this;

        Add(key, LoadTypeface(filename));

        return this;
    }

    /// <summary>
    ///     Called by a host after the platform font manager exists. A hit is a system answer:
    ///     <c>FontCache</c> drops it when <c>AddFont</c> grows this collection.
    /// </summary>
    internal void SetTypefaceFallback(Func<string, FontStyle, SKTypeface?>? resolve)
        => _resolveFallback = resolve;

    internal bool TryResolveFallback(string family, FontStyle style, [NotNullWhen(true)] out SKTypeface? typeface)
    {
        typeface = _resolveFallback?.Invoke(family, style);
        return typeface is not null;
    }

    private SKTypeface LoadTypeface(string filename)
    {
        var content = ReadAllBytes(filename)
            ?? throw new FileNotFoundException($"Font not found: {filename}", filename);

        // SKTypeface.FromStream takes ownership of the stream and disposes it with the typeface,
        // which lives as long as this collection does.
        return SKTypeface.FromStream(new MemoryStream(content))
            ?? throw new InvalidOperationException($"Font could not be decoded: {filename}");
    }

    private byte[]? ReadAllBytes(string filename)
    {
        if (_fileSystem is ISyncFileSystem syncFileSystem)
            return syncFileSystem.ReadFile(filename);

        throw new InvalidOperationException(
            "Font registration requires ISyncFileSystem. " +
            "Implement ISyncFileSystem on the platform file system " +
            "(or preload fonts) so AddFont does not block on ReadFileAsync.");
    }
}
