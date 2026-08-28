using Xunit;

namespace Pysar.Export.Tests;

public class ExportFormatTests
{
    [Fact]
    public void Pdf_HasThePdfId()
    {
        Assert.Equal("pdf", ExportFormat.Pdf.Id);
    }

    [Fact]
    public void SameId_ComparesEqual_SoItCanKeyAnExporterLookup()
    {
        Assert.Equal(ExportFormat.Pdf, new ExportFormat("pdf"));
        Assert.Equal(ExportFormat.Pdf.GetHashCode(), new ExportFormat("pdf").GetHashCode());
    }

    [Fact]
    public void Id_IsCaseInsensitive_SoCallersCannotRegisterTwoSpellingsOfOneFormat()
    {
        Assert.Equal(ExportFormat.Pdf, new ExportFormat("PDF"));
    }

    [Fact]
    public void DifferentIds_CompareUnequal()
    {
        Assert.NotEqual(ExportFormat.Pdf, new ExportFormat("docx"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankId_IsRejected(string? id)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ExportFormat(id!));
    }

    [Fact]
    public void Default_IsNotAnyRealFormat_AndDoesNotThrowWhenPrinted()
    {
        var uninitialised = default(ExportFormat);

        Assert.NotEqual(ExportFormat.Pdf, uninitialised);
        Assert.Equal("", uninitialised.ToString());
    }

    [Fact]
    public void ToString_IsTheId_SoErrorMessagesReadWell()
    {
        Assert.Equal("pdf", ExportFormat.Pdf.ToString());
    }
}
