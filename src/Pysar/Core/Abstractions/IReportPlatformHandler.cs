namespace Pysar.Core.Abstractions;

public interface IReportPlatformHandler
{
    public IFileSystem FileSystem { get; }
    public IFontCollection FontCollection { get; }
}
