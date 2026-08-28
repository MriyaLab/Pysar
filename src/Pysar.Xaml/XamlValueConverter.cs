using Pysar.Core.Structs;
using Pysar.Elements;
using CoreValueConverter = Pysar.Core.ValueConverter;

namespace Pysar.Xaml;

/// <summary>
///     Converts a XAML attribute string to a target CLR value. Delegates primitive/struct parsing to
///     <see cref="Pysar.Core.ValueConverter"/> (shared with the trigger engine) and additionally
///     handles the Elements-side grid collection shorthands (e.g. <c>ColumnDefinitions="*, 2*, Auto, 300"</c>).
///     Parse failures surface as <see cref="XamlException"/> for loader diagnostics.
/// </summary>
public static class XamlValueConverter
{
    public static bool IsConvertible(Type target)
        => target == typeof(List<ColumnDefinition>) || target == typeof(List<RowDefinition>)
           || target == typeof(ImageSource)
           || CoreValueConverter.IsConvertible(target);

    public static object? Convert(string text, Type target)
    {
        try
        {
            if (target == typeof(List<ColumnDefinition>))
                return text.Split(',').Select(s => new ColumnDefinition(GridLength.Parse(s))).ToList();
            if (target == typeof(List<RowDefinition>))
                return text.Split(',').Select(s => new RowDefinition(GridLength.Parse(s))).ToList();
            if (target == typeof(ImageSource))
                return ToImageSource(text);
            return CoreValueConverter.Convert(text, target);
        }
        catch (FormatException ex)
        {
            throw new XamlException(ex.Message);
        }
    }

    private static ImageSource ToImageSource(string text)
    {
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            return new UriImageSource(uri);

        return new FileImageSource(text);
    }
}
