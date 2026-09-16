namespace Pysar.Core.Abstractions;

/// <summary>
///     Marks an assembly that carries report assets as manifest resources, so
///     <c>EmbeddedAssetFileSystem</c> knows to index it.
/// </summary>
/// <remarks>
///     Emitted by <c>Pysar.Assets.targets</c> for any project declaring <c>ReportAsset</c> items
///     that are packaged as embedded resources; the marker is what keeps the index from
///     enumerating the manifest resources of every assembly in the process.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class PysarAssetSourceAttribute : Attribute;
