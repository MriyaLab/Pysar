using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;

namespace Pysar.Console.Sample.Reports;

public class BusinessReport
{
    public Report Build()
    {
        var report = new Report();
        report.PageFormat = new PageFormat()
        {
            Margin = new Thickness(50, 0),
            Size = PageSize.A4
        };
        var pageHeaderBand = new PageHeaderBand()
        {
            IsClippedToBounds = false,
        };
        var grid = new Grid()
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fill),
            IsClippedToBounds = false,
            RowDefinitions = new List<RowDefinition>
            {
                new RowDefinition(GridLength.Fixed(250)),
                new RowDefinition(GridLength.Fixed(150)),
                new RowDefinition(GridLength.Star())
            }
        };

        var logo = new Image()
        {
            Size = new Size(100,100),
            Source = new FileImageSource("Images/world.svg"),
            VerticalAlignment = Alignment.Center,
            HorizontalAlignment = Alignment.Start
        };

        var frame = new Frame()
            {
                IsClippedToBounds = false,
                BackgroundColor = Colors.Chocolate,
                Margin = new Thickness(-50,0),
                Size = new Size(SizeLength.Fill, SizeLength.Fill),
            };
        frame.AddElement(new Text()
        {
            Position = new Position(50, -10),
            Content = "BUSINESS REPORT",
            Size = new Size(SizeLength.Fixed(200), SizeLength.Fill),
            LineHeight = 1f,
            Font = new Font("Kanit", 38, Colors.White, FontStyle.Bold)
        });

        var stack = new StackPanel
        {
            Orientation = StackOrientation.Vertical,
            Size = new Size(SizeLength.Fill, SizeLength.Fill),
            Margin = new Thickness(0, 50),
            Spacing = 5,
        };
        stack.AddElements(new []
        {
            new Text()
            {
                Content = "YOUR COMPANY'S NAME",
                Font = new Font("Kanit", 24, Colors.Black, FontStyle.Bold),
                Margin = new Thickness(0,0,0,10)
            },
            new Text()
            {
                Content = "PREPARED FOR:",
                Font = new Font("Kanit", 18, Colors.Black, FontStyle.Bold),
                Margin = new Thickness(0,20,0,10)
            },
            new Text()
            {
                Content = "John Smith",
                Font = new Font("Kanit", 14, Colors.Black)
            },
            new Text()
            {
                Content = "Chief Executive Officer",
                Font = new Font("Kanit", 14, Colors.Black)
            },
            new Text()
            {
                Content = "PREPARED BY:",
                Font = new Font("Kanit", 18, Colors.Black, FontStyle.Bold),
                Margin = new Thickness(0,20,0,10)
            },
             new Text()
            {
                Content = "Adam Brown",
                Font = new Font("Kanit", 14, Colors.Black)
            },
            new Text()
            {
                Content = "Chief Information Officer",
                Font = new Font("Kanit", 14, Colors.Black)
            },
        });

        grid.AddElement(logo, 0, 0);
        grid.AddElement(frame, 1, 0);
        grid.AddElement(stack, 2, 0);
        pageHeaderBand.AddElement(grid);
        report.Bands.Add(pageHeaderBand);
        return report.Build();
    }
}
