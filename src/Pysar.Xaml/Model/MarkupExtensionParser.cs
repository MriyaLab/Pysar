namespace Pysar.Xaml.Model;

internal static class MarkupExtensionParser
{
    public static XamlValueNode Parse(string value, XamlSourceSpan span)
    {
        if (!IsMarkupExtension(value))
            return new XamlLiteralNode(value, span);

        var body = value.Substring(1, value.Length - 2).Trim();
        if (TryReadExtension(body, "StaticResource", out var resourceBody))
            return new XamlStaticResourceNode(resourceBody.Trim(), span);

        if (!TryReadExtension(body, "Binding", out var bindingBody))
            return new XamlUnknownMarkupExtensionNode(value, span);

        string? path = null;
        string? stringFormat = null;
        string? converterKey = null;
        string? converterParameter = null;
        string? sourceName = null;
        foreach (var argument in SplitArguments(bindingBody))
        {
            var separator = IndexOfUnquoted(argument, '=');
            if (separator < 0)
            {
                if (path is null)
                    path = argument.Trim();
                continue;
            }

            var name = argument.Substring(0, separator).Trim();
            var argumentValue = argument.Substring(separator + 1).Trim();
            switch (name)
            {
                case "Path":
                    path = Unquote(argumentValue);
                    break;
                case "StringFormat":
                    stringFormat = Unquote(argumentValue);
                    break;
                case "Converter":
                    converterKey = ReadStaticResourceKey(argumentValue);
                    break;
                case "ConverterParameter":
                    converterParameter = Unquote(argumentValue);
                    break;
                case "Source":
                    sourceName = ReadReferenceName(argumentValue);
                    break;
            }
        }

        return new XamlBindingNode(path ?? string.Empty, stringFormat, converterKey, converterParameter, span, sourceName);
    }

    private static bool IsMarkupExtension(string value)
        => value.Length >= 2 && value[0] == '{' && value[value.Length - 1] == '}';

    private static bool TryReadExtension(string body, string name, out string arguments)
    {
        if (!body.StartsWith(name, StringComparison.Ordinal)
            || (body.Length > name.Length && !char.IsWhiteSpace(body[name.Length])))
        {
            arguments = string.Empty;
            return false;
        }

        arguments = body.Substring(name.Length).Trim();
        return true;
    }

    private static IEnumerable<string> SplitArguments(string text)
    {
        var start = 0;
        var braceDepth = 0;
        char quote = '\0';

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (quote != '\0')
            {
                if (character == quote)
                    quote = '\0';
                continue;
            }

            if (character == '\'' || character == '"')
            {
                quote = character;
                continue;
            }

            if (character == '{')
                braceDepth++;
            else if (character == '}')
                braceDepth--;
            else if (character == ',' && braceDepth == 0)
            {
                yield return text.Substring(start, index - start);
                start = index + 1;
            }
        }

        if (start <= text.Length)
            yield return text.Substring(start);
    }

    private static int IndexOfUnquoted(string text, char value)
    {
        char quote = '\0';
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (quote != '\0')
            {
                if (character == quote)
                    quote = '\0';
                continue;
            }

            if (character == '\'' || character == '"')
                quote = character;
            else if (character == value)
                return index;
        }

        return -1;
    }

    private static string? ReadStaticResourceKey(string value)
    {
        var trimmed = Unquote(value.Trim());
        if (!IsMarkupExtension(trimmed))
            return null;

        var body = trimmed.Substring(1, trimmed.Length - 2).Trim();
        return TryReadExtension(body, "StaticResource", out var key) ? key.Trim() : null;
    }

    /// <summary>Reads the element name out of <c>{x:Reference name}</c>; returns null for any other form.</summary>
    private static string? ReadReferenceName(string value)
    {
        var trimmed = Unquote(value.Trim());
        if (!IsMarkupExtension(trimmed))
            return null;

        var body = trimmed.Substring(1, trimmed.Length - 2).Trim();
        if (body.StartsWith("x:", StringComparison.Ordinal))
            body = body.Substring(2);

        return TryReadExtension(body, "Reference", out var name) ? name.Trim() : null;
    }

    private static string Unquote(string value)
        => value.Length >= 2
           && (value[0] == '\'' || value[0] == '"')
           && value[value.Length - 1] == value[0]
            ? value.Substring(1, value.Length - 2)
            : value;
}
