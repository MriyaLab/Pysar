using Pysar.Binding;

namespace Pysar.Elements;

public abstract class ImageSource : BindableObject
{
    public IList<DataTrigger> Triggers { get; } = new List<DataTrigger>();

    public abstract Task<byte[]?> LoadAsync(CancellationToken ct = default);

    public virtual ImageSource Clone()
    {
        var clone = (ImageSource)Activator.CreateInstance(GetType())!;
        CopyStateTo(clone);
        foreach (var trigger in Triggers)
            clone.Triggers.Add(trigger);
        return clone;
    }
}
