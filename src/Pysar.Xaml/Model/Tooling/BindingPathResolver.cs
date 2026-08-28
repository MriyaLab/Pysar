using System;
using System.Collections.Generic;

namespace Pysar.Xaml.Model.Tooling;

/// <summary>Resolves a binding path against a starting data type into the candidate
/// members for the final (partial) segment, using a host <see cref="ITypeMemberProvider"/>.</summary>
public sealed class BindingPathResolver
{
    private static readonly IReadOnlyList<BindingMember> None = Array.Empty<BindingMember>();
    private readonly ITypeMemberProvider _provider;

    public BindingPathResolver(ITypeMemberProvider provider)
        => _provider = provider ?? throw new ArgumentNullException(nameof(provider));

    /// <summary>Returns the members of the type reached by walking
    /// <see cref="BindingPath.CompletedSegments"/>, filtered by the partial segment.
    /// Returns an empty list if any completed segment cannot be resolved, or once the path reaches a
    /// dynamic member container: its keys are not known statically, so — matching
    /// <see cref="TryResolvePath"/>, which accepts any segment there — nothing is suggested rather
    /// than the container's own members (<c>Count</c>, <c>Keys</c>, …).</summary>
    public IReadOnlyList<BindingMember> ResolveCandidates(object rootType, BindingPath path)
    {
        var current = rootType;
        foreach (var segment in path.CompletedSegments)
        {
            if (_provider.IsDynamicMemberContainer(current))
                return None;
            if (!TryStep(current, segment, out var next))
                return None;
            current = next;
        }

        if (_provider.IsDynamicMemberContainer(current))
            return None;

        var members = _provider.EnumerateMembers(current);
        if (path.PartialSegment.Length == 0)
            return members;

        var filtered = new List<BindingMember>();
        foreach (var member in members)
            if (member.Name.StartsWith(path.PartialSegment, StringComparison.Ordinal))
                filtered.Add(member);
        return filtered;
    }

    /// <summary>Validates a full dotted binding path against <paramref name="rootType"/>: every
    /// segment must name a member, and every non-final segment's member must expose a resolvable
    /// type to descend into. Returns <c>true</c> when the whole path resolves; otherwise
    /// <c>false</c> with <paramref name="failingSegment"/> set to the first segment that fails.</summary>
    public bool TryResolvePath(object rootType, string path, out string? failingSegment)
    {
        var segments = (path ?? string.Empty).Split('.');
        var current = rootType;
        for (var i = 0; i < segments.Length; i++)
        {
            if (_provider.IsDynamicMemberContainer(current))
            {
                failingSegment = null;
                return true;
            }

            var segment = segments[i];
            var match = FindMember(current, segment);
            if (match is null)
            {
                failingSegment = segment;
                return false;
            }

            if (i < segments.Length - 1)
            {
                if (match.Value.TypeHandle is null)
                {
                    failingSegment = segment;
                    return false;
                }

                current = match.Value.TypeHandle;
            }
        }

        failingSegment = null;
        return true;
    }

    /// <summary>Resolves a dotted path to the type handle of its final member, walking every
    /// segment. Returns <paramref name="rootType"/> for an empty path (a self-binding), or
    /// <c>null</c> if any segment does not resolve or lacks a type to descend into.</summary>
    public object? ResolvePathType(object rootType, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return rootType;

        var current = rootType;
        foreach (var segment in path.Split('.'))
        {
            // A key lookup on a dynamic container yields a value of unknown (object) type; we cannot
            // retarget a child scope to it, so report the type as unresolved.
            if (_provider.IsDynamicMemberContainer(current))
                return null;

            var next = FindMember(current, segment)?.TypeHandle;
            if (next is null)
                return null;
            current = next;
        }

        return current;
    }

    private bool TryStep(object type, string segment, out object next)
    {
        var handle = FindMember(type, segment)?.TypeHandle;
        next = handle!;
        return handle is not null;
    }

    /// <summary>The member of <paramref name="type"/> named <paramref name="segment"/>, or
    /// <c>null</c> when the type has no such member.</summary>
    private BindingMember? FindMember(object type, string segment)
    {
        foreach (var member in _provider.EnumerateMembers(type))
            if (member.Name == segment)
                return member;
        return null;
    }
}
