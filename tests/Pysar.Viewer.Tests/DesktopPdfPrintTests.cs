using Pysar.Export;
using Xunit;

namespace Pysar.Viewer.Tests;

public class DesktopPdfPrintTests
{
    [Fact]
    public void WriteTemporaryPdf_WritesTheBytes()
    {
        var bytes = "%PDF-1.4 test"u8.ToArray();
        var path = DesktopPdfPrint.WriteTemporaryPdf(bytes);
        try
        {
            Assert.True(File.Exists(path));
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteTemporaryPdf_RejectsNull()
        => Assert.Throws<ArgumentNullException>(() => DesktopPdfPrint.WriteTemporaryPdf(null!));

    [Fact]
    public void TryShowMacPrintPanel_EmptyPdf_ReturnsFalse()
        => Assert.False(DesktopPdfPrint.TryShowMacPrintPanel([], "Job", new PrintPaper(595, 842, false, "iso-a4")));

    [Fact]
    public void TryShowMacPrintPanel_RejectsNullBytes()
        => Assert.Throws<ArgumentNullException>(
            () => DesktopPdfPrint.TryShowMacPrintPanel(null!, "Job", new PrintPaper(595, 842, false, null)));

    [Fact]
    public void OpenInShell_RejectsNull()
        => Assert.Throws<ArgumentNullException>(() => DesktopPdfPrint.OpenInShell(null!));
}
