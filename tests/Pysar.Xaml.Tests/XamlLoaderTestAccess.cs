using System.Text;
using Pysar.Xaml;

namespace Pysar.Xaml.Tests;

internal sealed class XamlLoaderTestAccess
{
    public XamlLoadResult LoadWithNames(string xaml)
    {
        using var s = new MemoryStream(Encoding.UTF8.GetBytes(xaml));
        return new XamlLoader().Load(s);
    }
}
