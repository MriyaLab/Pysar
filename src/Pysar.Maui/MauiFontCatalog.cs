using System.Reflection;
using Pysar.Core.Abstractions;
using Pysar.Core.Enums;
using SkiaSharp;

namespace Pysar.Maui;

/// <summary>
///     Alias to font-file map captured from MAUI's <c>ConfigureFonts</c> registrations.
///     A hit is only <see cref="FontStyle.Normal"/>: MAUI registers one file per alias, so Bold
///     against that alias would be a fake bold.
/// </summary>
internal sealed class MauiFontCatalog
{
    private readonly Dictionary<string, MauiFontRegistration> _fonts = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public void Register(string filename, string? alias, Assembly? assembly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);

        var registration = new MauiFontRegistration(filename, assembly);
        lock (_gate)
        {
            _fonts[filename] = registration;
            if (!string.IsNullOrWhiteSpace(alias))
                _fonts[alias] = registration;
        }
    }

    public SKTypeface? TryResolve(string family, FontStyle style, ISyncFileSystem package)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (style != FontStyle.Normal || string.IsNullOrWhiteSpace(family))
            return null;

        MauiFontRegistration registration;
        lock (_gate)
        {
            if (!_fonts.TryGetValue(family, out registration!))
                return null;
        }

        var bytes = Read(registration, package);
        if (bytes is not { Length: > 0 })
            return null;

        return SKTypeface.FromStream(new MemoryStream(bytes));
    }

    private static byte[]? Read(MauiFontRegistration registration, ISyncFileSystem package)
    {
        if (registration.Assembly is null)
            return package.ReadFile(registration.Filename);

        return ReadEmbedded(registration.Assembly, registration.Filename);
    }

    private static byte[]? ReadEmbedded(Assembly assembly, string filename)
    {
        var suffix = "." + filename;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            return null;

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return null;

        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private sealed record MauiFontRegistration(string Filename, Assembly? Assembly);
}
