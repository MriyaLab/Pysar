using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Rendering;
using SkiaSharp;
using Xunit;

namespace Pysar.Skia.Tests;

/// <summary>
///     A session measures once and draws at whatever the viewer's zoom asks for. What it draws must
///     not change with that zoom: text that fits when the page is laid out has to keep fitting, or
///     the report would silently say something different at 500% than at 100%.
/// </summary>
/// <remarks>
///     The widths are swept around the point where the text exactly fills its box, because that is
///     the only place the bug this guards against can show: glyph advances do not scale perfectly
///     linearly, so a string that ends a hair inside its box at one scale ends a hair outside it at
///     another - and picks up an ellipsis, or an extra wrapped line, purely from the zoom.
/// </remarks>
public class SessionZoomStabilityTests
{
    private const string Heading = "Northwind Traders Annual Report";

    [Theory]
    [InlineData(TextTrimming.TailTruncation)]
    [InlineData(TextTrimming.WordWrap)]
    public async Task DrawnText_IsTheSameAtEveryZoom(TextTrimming trimming)
    {
        // A sweep across the fitting boundary: at least one of these widths lands on it.
        for (var width = 120f; width <= 220f; width += 4f)
        {
            var atOne = await InkFractionAsync(width, trimming, scale: 1f);
            var atFive = await InkFractionAsync(width, trimming, scale: 5f);

            Assert.True(
                Math.Abs(atOne - atFive) < 0.02,
                $"{trimming} at width {width}: ink reaches {atOne:P1} of the strip at scale 1 "
                + $"but {atFive:P1} at scale 5 - the text changed with the zoom.");
        }
    }

    /// <summary>
    ///     How far across and down the strip the ink reaches, as a fraction of the strip. Truncation
    ///     shortens the first; an extra wrapped line lengthens the second.
    /// </summary>
    private static async Task<double> InkFractionAsync(float width, TextTrimming trimming, float scale)
    {
        var session = await ReportRenderSession.CreateAsync(BuildReport(width, trimming));

        using var bitmap = await session.RenderRegionAsync(0, new SKRect(0, 0, 595.5f, 200), scale);

        var right = 0;
        var bottom = 0;

        for (var x = 0; x < bitmap.Width; x++)
        for (var y = 0; y < bitmap.Height; y++)
            if (bitmap.GetPixel(x, y) != SKColors.White)
            {
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }

        return (right + 1) / (double)bitmap.Width + (bottom + 1) / (double)bitmap.Height;
    }

    private static Report BuildReport(float width, TextTrimming trimming)
        => ReportBuilder.Create("Zoom stability")
            .WithPageFormat(new PageFormat { Margin = new Thickness(30) })
            .WithDetail(detail => detail.AddElement(new Text
            {
                Content = Heading,
                Font = new Font { Size = 14 },
                TextTrimming = trimming,
                Size = new Size(SizeLength.Fixed(width), SizeLength.Fixed(120))
            }))
            .Build();
}
