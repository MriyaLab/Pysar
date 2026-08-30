using Pysar.Binding;
using Pysar.Core.Structs;
using Pysar.Elements.Base;
using SkiaSharp.QrCode;

namespace Pysar.Sample.Reports.QRCode;

public class QRCode : ReportContainer<QRCode>
{
    public static BindableProperty ContentProperty { get; } =
        BindableProperty.Create(nameof(Content), typeof(string), typeof(QRCode), string.Empty);
    
    public static BindableProperty ColorProperty { get; } =
        BindableProperty.Create(nameof(Color), typeof(Color), typeof(QRCode), Colors.Black);
    
    public static BindableProperty ECCLevelProperty { get; } =
        BindableProperty.Create(nameof(ECCLevel), typeof(ECCLevel), typeof(QRCode), ECCLevel.H);
    
    public static BindableProperty QuietZoneSizeProperty { get; } =
        BindableProperty.Create(nameof(QuietZoneSize), typeof(int), typeof(QRCode), 4);
        
    public string Content
    {
        get => (string)GetValue(ContentProperty)!;
        set => SetValue(ContentProperty, value);
    }
    
    public Color Color
    {
        get => (Color)GetValue(ColorProperty)!;
        set => SetValue(ColorProperty, value);
    }
    
    public ECCLevel ECCLevel
    {
        get => (ECCLevel)GetValue(ECCLevelProperty)!;
        set => SetValue(ECCLevelProperty, value);
    }
    
    public int QuietZoneSize
    {
        get => (int)GetValue(QuietZoneSizeProperty)!;
        set => SetValue(QuietZoneSizeProperty, value);
    }

    public QRCode()
    {
    }
    
    public QRCode(string content)
    {
        Content = content;
    }
}