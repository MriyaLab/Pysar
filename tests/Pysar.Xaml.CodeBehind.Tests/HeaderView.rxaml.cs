using Pysar.Binding;
using Pysar.Elements;

namespace Pysar.Xaml.CodeBehind.Tests;

public partial class HeaderView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(HeaderView), string.Empty);

    public HeaderView() => InitializeComponent();

    public string Title
    {
        get => (string)GetValue(TitleProperty)!;
        set => SetValue(TitleProperty, value);
    }

    // Proves (at compile time) that the generated strongly-typed x:Name field exists.
    internal Text CaptionField => Caption;
}
