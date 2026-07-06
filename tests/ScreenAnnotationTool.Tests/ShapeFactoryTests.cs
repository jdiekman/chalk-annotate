using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenAnnotationTool.Drawing;
using Xunit;

namespace ScreenAnnotationTool.Tests;

public class ShapeFactoryTests
{
    [WpfFact]
    public void CreateRectangle_NormalizesSizeAndPosition()
    {
        var r = ShapeFactory.CreateRectangle(new Point(30, 40), new Point(10, 20), Colors.Blue, 3);
        Assert.Equal(20, r.Width);
        Assert.Equal(20, r.Height);
        Assert.Equal(10, Canvas.GetLeft(r));
        Assert.Equal(20, Canvas.GetTop(r));
        Assert.Equal(3, r.StrokeThickness);
        Assert.Equal(Colors.Blue, ((SolidColorBrush)r.Stroke).Color);
    }

    [WpfFact]
    public void CreateEllipse_NormalizesSize()
    {
        var e = ShapeFactory.CreateEllipse(new Point(0, 0), new Point(50, 25), Colors.Green, 2);
        Assert.Equal(50, e.Width);
        Assert.Equal(25, e.Height);
        Assert.Equal(0, Canvas.GetLeft(e));
        Assert.Equal(0, Canvas.GetTop(e));
    }

    [WpfFact]
    public void CreateArrow_ReturnsPathWithColor()
    {
        var p = ShapeFactory.CreateArrow(new Point(0, 0), new Point(100, 0), Colors.Red, 4);
        Assert.NotNull(p.Data);
        Assert.Equal(4, p.StrokeThickness);
        Assert.Equal(Colors.Red, ((SolidColorBrush)p.Stroke).Color);
    }

    [WpfFact]
    public void StartFreehand_SeedsFirstPoint()
    {
        var pl = ShapeFactory.StartFreehand(new Point(5, 6), Colors.Black, 4);
        Assert.Single(pl.Points);
        Assert.Equal(new Point(5, 6), pl.Points[0]);
        Assert.Equal(4, pl.StrokeThickness);
    }
}
