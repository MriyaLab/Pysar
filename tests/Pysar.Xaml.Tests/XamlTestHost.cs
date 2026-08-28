using Pysar.Xaml;

namespace Pysar.Xaml.Tests;

internal static class XamlTestHost
{
    public static T BuildElement<T>(string xaml) => (T)XamlLoader.BuildStandalone(xaml);
}
