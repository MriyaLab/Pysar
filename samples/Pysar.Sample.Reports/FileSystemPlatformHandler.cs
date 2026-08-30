using System.Diagnostics.CodeAnalysis;
using Pysar.Core.Abstractions;
using Pysar.Skia;

namespace Pysar.Sample.Reports;

/// <summary>Resolves report assets from the application output directory.</summary>
public class FileSystemPlatformHandler : IReportPlatformHandler
{
    public FileSystemPlatformHandler()
    {
        FileSystem = new LocalFileSystem();
        FontCollection = new SkiaFontCollection(FileSystem);
    }

    public IFileSystem FileSystem { get; }

    public IFontCollection FontCollection { get; }
}

public class LocalFileSystem : IFileSystem, ISyncFileSystem
{
    private readonly string? _applicationDirectoryPath;

    public LocalFileSystem()
    {
        // AppContext.BaseDirectory is the directory of the managed application, which is what
        // relative asset paths are authored against. Process.MainModule points at the executing
        // binary instead, so it resolves to the shared runtime when the assembly is loaded by a
        // host such as `dotnet app.dll` or the design-time preview - and every image goes missing.
        _applicationDirectoryPath = AppContext.BaseDirectory;
    }

    public async Task<byte[]?> ReadFileAsync(string filePath)
    {
        var fixedFilePath = CheckIfExistsAndFixPath(filePath);
        if (fixedFilePath == null)
            return null;

        return await File.ReadAllBytesAsync(fixedFilePath);
    }

    public byte[]? ReadFile(string filePath)
    {
        var fixedFilePath = CheckIfExistsAndFixPath(filePath);
        if (fixedFilePath == null)
            return null;

        return File.ReadAllBytes(fixedFilePath);
    }

    public bool Exists([NotNullWhen(true)] string? filePath)
    {
        return CheckIfExistsAndFixPath(filePath) != null;
    }

    public string? CheckIfExistsAndFixPath([NotNullWhen(true)] string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return filePath;

        if (string.IsNullOrEmpty(_applicationDirectoryPath))
            return null;

        filePath = Path.Combine(_applicationDirectoryPath, filePath);
        if (File.Exists(Path.Combine(filePath)))
            return filePath;

        return null;
    }
}
