using Pysar.Skia.Helpers;
using Pysar.Skia.Layout;
using Pysar.Skia.Rendering;
using SkiaSharp;
using SkiaSharp.QrCode;
using SkiaSharp.QrCode.Image;

namespace Pysar.Console.Sample.Reports.CustomControls;

/// <summary>
///     Draws a QR code as vector content onto the page canvas via <see cref="QRCodeRenderer"/>, so it
///     stays crisp at any zoom in the vector PDF instead of blurring like an upscaled bitmap.
/// </summary>
public sealed class QRCodeDrawer : IElementDrawer
{
    public void Draw(LayoutNode node, RenderContext ctx)
    {
        var qr = (QRCode)node.Element;
        if (string.IsNullOrEmpty(qr.Content)) return;

        var rect = node.Bounds.ToSkiaRect(ctx.Scale);
        var side = Math.Min(rect.Width, rect.Height);
        var area = SKRect.Create(rect.Left, rect.Top, side, side);

        var data = QRCodeGenerator.CreateQrCode(qr.Content, qr.ECCLevel, quietZoneSize: qr.QuietZoneSize);
        QRCodeRenderer.Render(
            ctx.Canvas, area, data,
            qr.Color.ToSkiaColor(),
            qr.BackgroundColor.ToSkiaColor(),
            null,
            new RoundedRectangleModuleShape(.0f),
            0.9f);
    }
}
