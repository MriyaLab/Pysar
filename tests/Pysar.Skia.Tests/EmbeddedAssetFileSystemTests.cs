using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Pysar.Skia.Tests;

public sealed class EmbeddedAssetFileSystemTests
{
    /// <summary>Declared as an EmbeddedResource with this exact LogicalName - see the csproj.</summary>
    private const string AssetPath = "Fonts/Embedded-Ubuntu.ttf";

    private static byte[] Expected()
        => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fonts", "Ubuntu-Regular.ttf"));

    [Fact]
    public void ReadFile_ReadsAResourceOfAMarkedAssembly()
    {
        var fileSystem = new EmbeddedAssetFileSystem();

        Assert.Equal(Expected(), fileSystem.ReadFile(AssetPath));
    }

    [Fact]
    public async Task ReadFileAsync_ReadsTheSameBytes()
    {
        var fileSystem = new EmbeddedAssetFileSystem();

        Assert.Equal(Expected(), await fileSystem.ReadFileAsync(AssetPath));
    }

    [Fact]
    public void Exists_AgreesWithReadFile()
    {
        var fileSystem = new EmbeddedAssetFileSystem();

        Assert.True(fileSystem.Exists(AssetPath));
        Assert.False(fileSystem.Exists("Fonts/Missing.ttf"));
    }

    [Fact]
    public void ReadFile_AcceptsTheSeparatorsAndCasingAReportMightUse()
    {
        var fileSystem = new EmbeddedAssetFileSystem();

        Assert.Equal(Expected(), fileSystem.ReadFile(@"Fonts\Embedded-Ubuntu.ttf"));
        Assert.Equal(Expected(), fileSystem.ReadFile("/Fonts/Embedded-Ubuntu.ttf"));
        Assert.Equal(Expected(), fileSystem.ReadFile("fonts/embedded-ubuntu.ttf"));
    }

    [Fact]
    public void ReadFile_IgnoresAnAssemblyWithoutTheMarker()
    {
        // System.Private.CoreLib carries manifest resources of its own and no Pysar marker;
        // an unmarked assembly must not be indexed at all.
        var fileSystem = new EmbeddedAssetFileSystem([typeof(object).Assembly]);

        Assert.Null(fileSystem.ReadFile(AssetPath));
    }

    [Fact]
    public void ReadFile_ReturnsNullForNullOrEmpty()
    {
        var fileSystem = new EmbeddedAssetFileSystem();

        Assert.Null(fileSystem.ReadFile(""));
        Assert.Null(fileSystem.ReadFile(null!));
        Assert.False(fileSystem.Exists(null));
    }

    [Fact]
    public void ReadFile_SeesAnAssemblyLoadedAfterTheFirstRead()
    {
        // Uno and Blazor load assemblies lazily, and the design-time preview host loads the user's
        // assembly after Pysar itself: an index built on the first miss cannot be the final one.
        var fileSystem = new EmbeddedAssetFileSystem();
        Assert.Null(fileSystem.ReadFile("Fonts/Later.ttf"));

        Assembly.Load(BuildAssemblyWithAsset("Fonts/Later.ttf", Expected()));

        Assert.Equal(Expected(), fileSystem.ReadFile("Fonts/Later.ttf"));
    }

    /// <summary>Emits an in-memory assembly carrying the marker and one embedded asset.</summary>
    private static byte[] BuildAssemblyWithAsset(string logicalName, byte[] content)
    {
        var compilation = CSharpCompilation.Create(
            "Pysar.Test.LateAssets",
            [CSharpSyntaxTree.ParseText("[assembly: Pysar.Core.Abstractions.PysarAssetSource]")],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                // Attribute is forwarded from System.Private.CoreLib to System.Runtime on .NET; that
                // reference assembly is always loaded into the current process, so it is located
                // among the already-loaded assemblies rather than copied into this project's output.
                MetadataReference.CreateFromFile(
                    AppDomain.CurrentDomain.GetAssemblies()
                        .First(a => a.GetName().Name == "System.Runtime").Location),
                MetadataReference.CreateFromFile(
                    typeof(Pysar.Core.Abstractions.PysarAssetSourceAttribute).Assembly.Location)
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var peStream = new MemoryStream();
        var result = compilation.Emit(
            peStream,
            manifestResources:
            [
                new ResourceDescription(logicalName, () => new MemoryStream(content), isPublic: true)
            ]);

        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        return peStream.ToArray();
    }
}
