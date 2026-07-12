using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ScreenAnnotationTool.Drawing;

public static class ShapeFactory
{
    public static Rectangle CreateRectangle(Point start, Point end, Color color, double thickness)
    {
        double x = Math.Min(start.X, end.X);
        double y = Math.Min(start.Y, end.Y);
        var r = new Rectangle
        {
            Width = Math.Abs(end.X - start.X),
            Height = Math.Abs(end.Y - start.Y),
            Stroke = CreateBrush(color),
            StrokeThickness = thickness,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(r, x);
        Canvas.SetTop(r, y);
        return r;
    }

    public static Ellipse CreateEllipse(Point start, Point end, Color color, double thickness)
    {
        double x = Math.Min(start.X, end.X);
        double y = Math.Min(start.Y, end.Y);
        var e = new Ellipse
        {
            Width = Math.Abs(end.X - start.X),
            Height = Math.Abs(end.Y - start.Y),
            Stroke = CreateBrush(color),
            StrokeThickness = thickness,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(e, x);
        Canvas.SetTop(e, y);
        return e;
    }

    public static Path CreateArrow(Point start, Point end, Color color, double thickness)
    {
        var geometry = new GeometryGroup();
        geometry.Children.Add(new LineGeometry(start, end));

        // Arrowhead
        double headLength = Math.Max(12, thickness * 4);
        double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        double spread = Math.PI / 7;

        var p1 = new Point(
            end.X - headLength * Math.Cos(angle - spread),
            end.Y - headLength * Math.Sin(angle - spread));
        var p2 = new Point(
            end.X - headLength * Math.Cos(angle + spread),
            end.Y - headLength * Math.Sin(angle + spread));

        geometry.Children.Add(new LineGeometry(end, p1));
        geometry.Children.Add(new LineGeometry(end, p2));
        geometry.Freeze();

        return new Path
        {
            Data = geometry,
            Stroke = CreateBrush(color),
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
    }

    public static Polyline StartFreehand(Point start, Color color, double thickness)
    {
        var pl = new Polyline
        {
            Stroke = CreateBrush(color),
            StrokeThickness = thickness,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        pl.Points.Add(start);
        return pl;
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
