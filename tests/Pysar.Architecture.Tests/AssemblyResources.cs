using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Pysar.Architecture.Tests;

/// <summary>
///     Reads an assembly's manifest resource names out of its metadata. Loading it instead would
///     make this project a consumer of what it verifies, and these assemblies are read straight out
///     of a .nupkg or a probe's output directory anyway.
/// </summary>
internal static class AssemblyResources
{
    public static string[] NamesIn(byte[] assembly)
    {
        using var stream = new MemoryStream(assembly);
        using var reader = new PEReader(stream);
        var metadata = reader.GetMetadataReader();

        return metadata.ManifestResources
            .Select(h => metadata.GetString(metadata.GetManifestResource(h).Name))
            .ToArray();
    }
}
