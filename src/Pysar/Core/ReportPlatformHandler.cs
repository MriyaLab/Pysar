using Pysar.Core.Abstractions;
using Pysar.Core.Enums;

namespace Pysar.Core;

public static class ReportPlatformHandler
{
    private static IReportPlatformHandler? Current;
    
    private static readonly EmptyFontCollection EmptyFonts = new();

    public static IFileSystem FileSystem
    {
        get
        {
            if (Current?.FileSystem == null)
                return EmptyFileSystem.Instance;

            return Current.FileSystem;
        }
    }

    public static IFontCollection FontCollection
    {
        get
        {
            if (Current?.FontCollection == null)
                return EmptyFonts;

            return Current.FontCollection;
        }
    }

    public static void Create(IReportPlatformHandler  handler)
    {
        Current = handler;
    }

    private class EmptyFontCollection : Dictionary<string, object>, IFontCollection
    {
        public EmptyFontCollection() : base() { }
        public IFontCollection AddFont(string filename, string? alias = null, FontStyle fontStyle = FontStyle.Normal) => this;
    }

    private class EmptyFileSystem : IFileSystem
    {
        private static readonly EmptyFileSystem _instance = new();
        public static IFileSystem Instance => _instance;
        public Task<byte[]?> ReadFileAsync(string filePath) => Task.FromResult<byte[]?>(null);
        public bool Exists(string? filePath) => false;
    }
}
