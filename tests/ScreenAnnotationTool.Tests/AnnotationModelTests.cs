using System.Windows.Media;
using ScreenAnnotationTool.Drawing;
using Xunit;

namespace ScreenAnnotationTool.Tests;

public class AnnotationModelTests
{
    [Fact]
    public void Defaults_AreFreehandRedThickness4()
    {
        var m = new AnnotationModel();
        Assert.Equal(ToolKind.Freehand, m.CurrentTool);
        Assert.Equal(Colors.Red, m.CurrentColor);
        Assert.Equal(4, m.CurrentThickness);
        Assert.Empty(m.Annotations);
    }

    [Fact]
    public void Add_AppendsAnnotation()
    {
        var m = new AnnotationModel();
        var item = new object();
        m.Add(item);
        Assert.Single(m.Annotations);
        Assert.Same(item, m.Annotations[0]);
    }

    [Fact]
    public void Undo_RemovesAndReturnsLast()
    {
        var m = new AnnotationModel();
        var a = new object();
        var b = new object();
        m.Add(a);
        m.Add(b);
        Assert.Same(b, m.Undo());
        Assert.Single(m.Annotations);
        Assert.Same(a, m.Annotations[0]);
    }

    [Fact]
    public void Undo_OnEmpty_ReturnsNull()
    {
        var m = new AnnotationModel();
        Assert.Null(m.Undo());
    }

    [Fact]
    public void Clear_EmptiesList()
    {
        var m = new AnnotationModel();
        m.Add(new object());
        m.Add(new object());
        m.Clear();
        Assert.Empty(m.Annotations);
    }

    [Fact]
    public void Changed_FiresOnAddUndoClear()
    {
        var m = new AnnotationModel();
        int count = 0;
        m.Changed += (_, _) => count++;
        m.Add(new object()); // 1
        m.Undo();            // 2
        m.Clear();           // 3
        Assert.Equal(3, count);
    }
}
