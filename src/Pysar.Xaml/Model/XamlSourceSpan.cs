namespace Pysar.Xaml.Model;

internal readonly struct XamlSourceSpan
{
    public XamlSourceSpan(int line, int column)
    {
        Line = line;
        Column = column;
    }

    public int Line { get; }
    public int Column { get; }
}
