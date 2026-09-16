namespace Pysar.Uno;

/// <summary>
///     Holds views a cell has finished with so the next cell does not pay to build another.
/// </summary>
internal sealed class RecycledViewPool<T> where T : class
{
    private readonly Stack<T> _spare = new();

    public int Count => _spare.Count;

    public T Take(Func<T> create)
    {
        ArgumentNullException.ThrowIfNull(create);

        return _spare.TryPop(out var spare) ? spare : create();
    }

    public void Release(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _spare.Push(item);
    }

    public void Clear() => _spare.Clear();
}
