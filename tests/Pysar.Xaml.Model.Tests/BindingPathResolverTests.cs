using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Pysar.Xaml.Model.Tooling;
using Xunit;

namespace Pysar.Xaml.Model.Tests;

public class BindingPathResolverTests
{
    private sealed class Address { public string City { get; set; } = ""; public string Zip { get; set; } = ""; }
    private sealed class Customer
    {
        public string Name { get; set; } = "";
        public Address Address { get; set; } = new();
        public Dictionary<string, object> Extras { get; set; } = new();
    }

    private sealed class ReflectionProvider : ITypeMemberProvider
    {
        public object? ResolveType(string? prefix, string typeName, IReadOnlyDictionary<string, string> namespaces)
            => typeof(BindingPathResolverTests).GetNestedType(typeName, BindingFlags.NonPublic);

        public IReadOnlyList<BindingMember> EnumerateMembers(object typeHandle)
            => ((Type)typeHandle)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => new BindingMember(p.Name, p.PropertyType))
                .ToList();

        public bool IsDynamicMemberContainer(object typeHandle)
            => typeof(System.Collections.Generic.IDictionary<string, object>)
                .IsAssignableFrom((Type)typeHandle);
    }

    private static readonly IReadOnlyDictionary<string, string> NoNamespaces = new Dictionary<string, string>();

    [Fact]
    public void Resolve_RootPartial_FiltersByPrefix()
    {
        var provider = new ReflectionProvider();
        var root = provider.ResolveType(null, "Customer", NoNamespaces)!;
        var resolver = new BindingPathResolver(provider);

        var members = resolver.ResolveCandidates(root, BindingPath.Parse("Na"));

        Assert.Equal(new[] { "Name" }, members.Select(m => m.Name));
    }

    [Fact]
    public void Resolve_NestedPath_WalksIntoMemberType()
    {
        var provider = new ReflectionProvider();
        var root = provider.ResolveType(null, "Customer", NoNamespaces)!;
        var resolver = new BindingPathResolver(provider);

        var members = resolver.ResolveCandidates(root, BindingPath.Parse("Address.Ci"));

        Assert.Equal(new[] { "City" }, members.Select(m => m.Name));
    }

    [Fact]
    public void Resolve_UnknownSegment_ReturnsNoCandidates()
    {
        var provider = new ReflectionProvider();
        var root = provider.ResolveType(null, "Customer", NoNamespaces)!;
        var resolver = new BindingPathResolver(provider);

        var members = resolver.ResolveCandidates(root, BindingPath.Parse("Nope.Ci"));

        Assert.Empty(members);
    }

    [Fact]
    public void TryResolvePath_ValidNestedPath_Succeeds()
    {
        var provider = new ReflectionProvider();
        var root = provider.ResolveType(null, "Customer", NoNamespaces)!;
        var resolver = new BindingPathResolver(provider);

        var ok = resolver.TryResolvePath(root, "Address.City", out var failing);

        Assert.True(ok);
        Assert.Null(failing);
    }

    [Fact]
    public void TryResolvePath_UnknownLeafMember_FailsAtSegment()
    {
        var provider = new ReflectionProvider();
        var root = provider.ResolveType(null, "Customer", NoNamespaces)!;
        var resolver = new BindingPathResolver(provider);

        var ok = resolver.TryResolvePath(root, "Address.Nope", out var failing);

        Assert.False(ok);
        Assert.Equal("Nope", failing);
    }

    [Fact]
    public void TryResolvePath_UnknownIntermediateMember_FailsAtSegment()
    {
        var provider = new ReflectionProvider();
        var root = provider.ResolveType(null, "Customer", NoNamespaces)!;
        var resolver = new BindingPathResolver(provider);

        var ok = resolver.TryResolvePath(root, "Nope.City", out var failing);

        Assert.False(ok);
        Assert.Equal("Nope", failing);
    }

    [Fact]
    public void TryResolvePath_ValidRootMember_Succeeds()
    {
        var provider = new ReflectionProvider();
        var root = provider.ResolveType(null, "Customer", NoNamespaces)!;
        var resolver = new BindingPathResolver(provider);

        var ok = resolver.TryResolvePath(root, "Name", out var failing);

        Assert.True(ok);
        Assert.Null(failing);
    }

    [Fact]
    public void ResolvePathType_MemberPath_ReturnsMemberType()
    {
        var provider = new ReflectionProvider();
        var root = provider.ResolveType(null, "Customer", NoNamespaces)!;
        var resolver = new BindingPathResolver(provider);

        var type = resolver.ResolvePathType(root, "Address");

        Assert.Same(typeof(Address), type);
    }

    [Fact]
    public void ResolvePathType_EmptyPath_ReturnsRootType()
    {
        var provider = new ReflectionProvider();
        var root = provider.ResolveType(null, "Customer", NoNamespaces)!;
        var resolver = new BindingPathResolver(provider);

        var type = resolver.ResolvePathType(root, "");

        Assert.Same(root, type);
    }

    private sealed class DictionaryProvider : ITypeMemberProvider
    {
        public object? ResolveType(string? prefix, string typeName, IReadOnlyDictionary<string, string> namespaces)
            => typeof(Dictionary<string, object>);

        public IReadOnlyList<BindingMember> EnumerateMembers(object typeHandle)
            => System.Array.Empty<BindingMember>();

        public bool IsDynamicMemberContainer(object typeHandle) => true;
    }

    [Fact]
    public void TryResolvePath_DynamicContainer_AcceptsAnyKey()
    {
        var provider = new DictionaryProvider();
        var resolver = new BindingPathResolver(provider);

        var ok = resolver.TryResolvePath(typeof(Dictionary<string, object>), "AnyKey.Nested", out var failing);

        Assert.True(ok);
        Assert.Null(failing);
    }

    [Fact]
    public void ResolveCandidates_InsideDynamicContainer_SuggestsNothing()
    {
        var provider = new ReflectionProvider();
        var root = provider.ResolveType(null, "Customer", NoNamespaces)!;
        var resolver = new BindingPathResolver(provider);

        // Extras is a string-keyed dictionary: any key is valid at runtime, so its own members
        // (Count, Keys, …) must not be offered as completions.
        var members = resolver.ResolveCandidates(root, BindingPath.Parse("Extras."));

        Assert.Empty(members);
    }

    [Fact]
    public void ResolveCandidates_PastDynamicContainer_SuggestsNothing()
    {
        var provider = new ReflectionProvider();
        var root = provider.ResolveType(null, "Customer", NoNamespaces)!;
        var resolver = new BindingPathResolver(provider);

        // The value behind a key has an unknown type, so nothing can be completed past it —
        // matching TryResolvePath, which accepts any segment there.
        var members = resolver.ResolveCandidates(root, BindingPath.Parse("Extras.AnyKey."));

        Assert.Empty(members);
    }

    [Fact]
    public void ResolvePathType_UnknownMember_ReturnsNull()
    {
        var provider = new ReflectionProvider();
        var root = provider.ResolveType(null, "Customer", NoNamespaces)!;
        var resolver = new BindingPathResolver(provider);

        var type = resolver.ResolvePathType(root, "Nope");

        Assert.Null(type);
    }
}
