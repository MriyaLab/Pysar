using System.Diagnostics.CodeAnalysis;
using Pysar.Core.Abstractions;
using Xunit;

namespace Pysar.Skia.Tests;

public sealed class FallbackFileSystemTests
{
    [Fact]
    public void ReadFile_TakesTheFirstMemberThatHasThePath()
    {
        var fileSystem = new FallbackFileSystem(
            new StubFileSystem(("a.ttf", [1])),
            new StubFileSystem(("a.ttf", [2])));

        Assert.Equal([1], fileSystem.ReadFile("a.ttf"));
    }

    [Fact]
    public void ReadFile_FallsThroughToALaterMember()
    {
        var fileSystem = new FallbackFileSystem(
            new StubFileSystem(),
            new StubFileSystem(("a.ttf", [2])));

        Assert.Equal([2], fileSystem.ReadFile("a.ttf"));
    }

    [Fact]
    public async Task ReadFileAsync_FollowsTheSameOrder()
    {
        var fileSystem = new FallbackFileSystem(
            new StubFileSystem(),
            new StubFileSystem(("a.ttf", [2])));

        Assert.Equal([2], await fileSystem.ReadFileAsync("a.ttf"));
    }

    [Fact]
    public void Exists_AgreesWithReadFile()
    {
        var fileSystem = new FallbackFileSystem(
            new StubFileSystem(),
            new StubFileSystem(("a.ttf", [2])));

        Assert.True(fileSystem.Exists("a.ttf"));
        Assert.False(fileSystem.Exists("b.ttf"));
    }

    [Fact]
    public void ReadFile_SkipsTheSyncPathOfAnAsyncOnlyMember()
    {
        // ISyncFileSystem is what AddFont and the image renderer call; a member without it has
        // nothing to offer them and must be stepped over rather than blocked on.
        var fileSystem = new FallbackFileSystem(
            new AsyncOnlyFileSystem(),
            new StubFileSystem(("a.ttf", [2])));

        Assert.Equal([2], fileSystem.ReadFile("a.ttf"));
    }

    [Fact]
    public void Constructor_RejectsAnEmptyChain()
        => Assert.Throws<ArgumentException>(() => new FallbackFileSystem());

    private sealed class StubFileSystem(params (string Path, byte[] Content)[] files)
        : IFileSystem, ISyncFileSystem
    {
        private readonly Dictionary<string, byte[]> _files =
            files.ToDictionary(f => f.Path, f => f.Content);

        public byte[]? ReadFile(string filePath)
            => _files.TryGetValue(filePath, out var content) ? content : null;

        public Task<byte[]?> ReadFileAsync(string filePath) => Task.FromResult(ReadFile(filePath));

        public bool Exists([NotNullWhen(true)] string? filePath)
            => filePath is not null && _files.ContainsKey(filePath);
    }

    private sealed class AsyncOnlyFileSystem : IFileSystem
    {
        public Task<byte[]?> ReadFileAsync(string filePath) => Task.FromResult<byte[]?>([9]);

        public bool Exists([NotNullWhen(true)] string? filePath) => filePath is not null;
    }
}
