using Xunit;

namespace Pysar.Architecture.Tests;

/// <summary>
///     One pack for every class that asserts on the packages. As a class fixture it was one pack
///     per class, and xUnit runs classes in parallel - two <c>dotnet pack</c> runs of the same
///     project then race on its obj directory, and every assertion in both classes fails at once
///     on a package that was never produced.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PackCollection : ICollectionFixture<PackFixture>
{
    public const string Name = "packed output";
}
