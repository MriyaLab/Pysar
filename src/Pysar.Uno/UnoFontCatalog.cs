using System.Reflection;
using System.Runtime.CompilerServices;
using Pysar.Core.Enums;
using SkiaSharp;

namespace Pysar.Uno;

/// <summary>
///     Faces embedded from <c>UnoFont</c> items, keyed by the family name inside the file.
///     A face matches only its own style: a Regular file is not a Bold one.
/// </summary>
internal static class UnoFontCatalog
{
    private static readonly ConditionalWeakTable<Assembly, Lazy<Dictionary<string, SKTypeface>>> Catalogs = new();

    public static SKTypeface? TryResolve(Assembly assembly, string family, FontStyle style)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        if (string.IsNullOrWhiteSpace(family))
            return null;

        var faces = Catalogs.GetValue(assembly, assetAssembly =>
            new Lazy<Dictionary<string, SKTypeface>>(() => LoadFaces(assetAssembly))).Value;
        faces.TryGetValue(Key(family, style), out var typeface);
        return typeface;
    }

    private static Dictionary<string, SKTypeface> LoadFaces(Assembly assembly)
    {
        var faces = new Dictionary<string, SKTypeface>(StringComparer.OrdinalIgnoreCase);

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!IsFontResource(resourceName))
                continue;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
                continue;

            var copy = new MemoryStream();
            stream.CopyTo(copy);
            if (copy.Length == 0)
            {
                copy.Dispose();
                continue;
            }

            copy.Position = 0;
            var typeface = SKTypeface.FromStream(copy);
            if (typeface is null || string.IsNullOrWhiteSpace(typeface.FamilyName))
            {
                typeface?.Dispose();
                copy.Dispose();
                continue;
            }

            var key = Key(typeface.FamilyName, StyleOf(typeface));
            if (!faces.TryAdd(key, typeface))
                typeface.Dispose();
        }

        return faces;
    }

    private static bool IsFontResource(string resourceName)
    {
        var slash = resourceName.Replace('\\', '/');
        return slash.StartsWith("Fonts/", StringComparison.OrdinalIgnoreCase)
            && (slash.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                || slash.EndsWith(".otf", StringComparison.OrdinalIgnoreCase));
    }

    private static FontStyle StyleOf(SKTypeface typeface)
    {
        var weight = typeface.FontStyle.Weight;
        var italic = typeface.FontStyle.Slant is SKFontStyleSlant.Italic or SKFontStyleSlant.Oblique;
        var bold = weight >= (int)SKFontStyleWeight.Bold;

        return (bold, italic) switch
        {
            (true, true) => FontStyle.BoldItalic,
            (true, false) => FontStyle.Bold,
            (false, true) => FontStyle.Italic,
            _ => FontStyle.Normal
        };
    }

    private static string Key(string family, FontStyle style) => $"{family}|{style}";
}
