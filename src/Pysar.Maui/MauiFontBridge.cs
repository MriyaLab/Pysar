using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui;
using Pysar.Core.Abstractions;
using Pysar.Skia;

namespace Pysar.Maui;

/// <summary>
///     Lets a report use a font <c>ConfigureFonts</c> already registered, by the alias, without a
///     second <c>AddFont</c>. Installed by <see cref="MauiAppBuilderExtensions.UsePysar"/>.
/// </summary>
internal static class MauiFontBridge
{
    public static void Install(IServiceCollection services, SkiaFontCollection fonts, ISyncFileSystem package)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(package);

        var catalog = new MauiFontCatalog();
        ReplaceRegistrar(services, catalog);
        fonts.SetTypefaceFallback((family, style) => catalog.TryResolve(family, style, package));
    }

    /// <summary>
    ///     <c>ConfigureFonts</c> uses <c>TryAddSingleton</c>, so the registrar installed here is the
    ///     one <c>FontInitializer</c> registers aliases on, whether <c>UsePysar</c> ran first or second.
    /// </summary>
    private static void ReplaceRegistrar(IServiceCollection services, MauiFontCatalog catalog)
    {
        var existing = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(IFontRegistrar));
        services.RemoveAll<IFontRegistrar>();
        services.AddSingleton<IFontRegistrar>(provider =>
            new RecordingFontRegistrar(CreateInner(provider, existing), catalog));
    }

    private static IFontRegistrar CreateInner(IServiceProvider provider, ServiceDescriptor? existing)
    {
        if (existing?.ImplementationInstance is IFontRegistrar instance)
            return instance;

        if (existing?.ImplementationFactory is not null)
            return (IFontRegistrar)existing.ImplementationFactory(provider);

        if (existing?.ImplementationType is not null)
            return (IFontRegistrar)ActivatorUtilities.CreateInstance(provider, existing.ImplementationType);

        return new FontRegistrar(provider.GetRequiredService<IEmbeddedFontLoader>(), provider);
    }

    private sealed class RecordingFontRegistrar : IFontRegistrar
    {
        private readonly IFontRegistrar _inner;
        private readonly MauiFontCatalog _catalog;

        public RecordingFontRegistrar(IFontRegistrar inner, MauiFontCatalog catalog)
        {
            _inner = inner;
            _catalog = catalog;
        }

        public void Register(string filename, string? alias, System.Reflection.Assembly assembly)
        {
            _inner.Register(filename, alias, assembly);
            Remember(filename, alias, assembly);
        }

        public void Register(string filename, string? alias)
        {
            _inner.Register(filename, alias);
            Remember(filename, alias, assembly: null);
        }

        public string? GetFont(string font) => _inner.GetFont(font);

        private void Remember(string filename, string? alias, System.Reflection.Assembly? assembly)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return;

            _catalog.Register(filename, alias, assembly);
        }
    }
}
