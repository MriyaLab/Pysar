using System.Collections;
using Pysar.Core.Abstractions;

namespace Pysar.Elements;

public sealed class BandCollection : IReadOnlyList<Band>
{
    private readonly List<Band> _bands = [];
    private readonly IReportObject _owner;

    internal BandCollection(IReportObject owner) => _owner = owner;

    public int Count => _bands.Count;
    public Band this[int index] => _bands[index];

    public void Add(Band band)
    {
        ArgumentNullException.ThrowIfNull(band);
        if (_bands.Any(b => b.GetType() == band.GetType()))
            throw new InvalidOperationException(
                $"Report already contains a band of type {band.GetType().Name}. Each band type is allowed once.");
        band.ParentElement = _owner;
        _bands.Add(band);
    }

    /// <summary>Adds a band, replacing any existing band of the same runtime type.</summary>
    public void Set(Band band)
    {
        ArgumentNullException.ThrowIfNull(band);
        _bands.RemoveAll(b => b.GetType() == band.GetType());
        band.ParentElement = _owner;
        _bands.Add(band);
    }

    public TBand? GetBand<TBand>() where TBand : Band => _bands.OfType<TBand>().FirstOrDefault();

    public IEnumerator<Band> GetEnumerator() => _bands.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
