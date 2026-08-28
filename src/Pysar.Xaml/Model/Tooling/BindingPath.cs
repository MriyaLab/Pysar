using System.Collections.Generic;

namespace Pysar.Xaml.Model.Tooling;

/// <summary>A parsed binding path split into fully-typed leading segments and the
/// final segment being typed (used for completion).</summary>
public sealed class BindingPath
{
    private BindingPath(IReadOnlyList<string> completedSegments, string partialSegment)
    {
        CompletedSegments = completedSegments;
        PartialSegment = partialSegment;
    }

    /// <summary>Segments before the last dot; each must resolve to a member type.</summary>
    public IReadOnlyList<string> CompletedSegments { get; }

    /// <summary>The final, possibly incomplete, segment being completed.</summary>
    public string PartialSegment { get; }

    public static BindingPath Parse(string path)
    {
        var tokens = (path ?? string.Empty).Split('.');
        var completed = new List<string>(tokens.Length - 1);
        for (var i = 0; i < tokens.Length - 1; i++)
            completed.Add(tokens[i]);
        return new BindingPath(completed, tokens[tokens.Length - 1]);
    }
}
