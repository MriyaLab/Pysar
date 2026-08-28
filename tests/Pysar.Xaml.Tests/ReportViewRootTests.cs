using Pysar.Elements;
using Xunit;

namespace Pysar.Xaml.Tests;

public class ReportViewRootTests
{
    private const string Root =
        "xmlns=\"https://mriyalab.com/pysar\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    [Fact]
    public void ReportView_Children_AreAdded()
    {
        var view = XamlTestHost.BuildElement<ReportView>(
            $"<ReportView {Root} Height=\"Auto\"><Text Content=\"hi\"/><Text Content=\"there\"/></ReportView>");

        Assert.Equal(2, view.Children.Count);
    }

    [Fact]
    public void ReportView_Resources_AreOwnedByTheView()
    {
        var view = XamlTestHost.BuildElement<ReportView>(
            $"<ReportView {Root}>" +
            "<ReportView.Resources><ResourceDictionary>" +
            "<Color x:Key=\"Accent\">#FF0000</Color>" +
            "</ResourceDictionary></ReportView.Resources>" +
            "<Text Content=\"hi\"/></ReportView>");

        Assert.True(view.Resources.ContainsKey("Accent"));
    }

    [Fact]
    public void ReportView_StaticResource_ResolvesInsideTheComponent()
    {
        var view = XamlTestHost.BuildElement<ReportView>(
            $"<ReportView {Root}>" +
            "<ReportView.Resources><ResourceDictionary>" +
            "<Color x:Key=\"Accent\">#FF0000</Color>" +
            "</ResourceDictionary></ReportView.Resources>" +
            "<Text Content=\"hi\" FontColor=\"{StaticResource Accent}\"/></ReportView>");

        var text = Assert.IsType<Text>(view.Children[0]);
        Assert.Equal(Pysar.Core.Structs.Color.FromHex("#FF0000"), text.FontColor);
    }
}
