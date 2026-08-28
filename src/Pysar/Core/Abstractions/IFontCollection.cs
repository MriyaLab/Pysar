using Pysar.Core.Enums;

namespace Pysar.Core.Abstractions;

public interface IFontCollection : IDictionary<string, object>
{
    IFontCollection AddFont(string filename, string? alias = null, FontStyle fontStyle = FontStyle.Normal);
}
