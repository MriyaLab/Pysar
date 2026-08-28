namespace Pysar.Core.Abstractions;

/// <summary>
///     Implemented alongside <see cref="IFileSystem"/> by sources that can be read without awaiting -
///     an application package or a local directory. Callers that must stay synchronous, such as
///     <c>IFontCollection.AddFont</c>, use this instead of blocking on <see cref="IFileSystem.ReadFileAsync"/>.
/// </summary>
public interface ISyncFileSystem
{
    /// <summary>Reads <paramref name="filePath"/>, or returns <c>null</c> when it does not exist.</summary>
    byte[]? ReadFile(string filePath);
}
