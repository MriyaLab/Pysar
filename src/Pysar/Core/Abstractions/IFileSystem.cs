using System.Diagnostics.CodeAnalysis;

namespace Pysar.Core.Abstractions;

public interface IFileSystem
{
    Task<byte[]?> ReadFileAsync(string filePath);

    bool Exists([NotNullWhen(true)] string? filePath);
}
