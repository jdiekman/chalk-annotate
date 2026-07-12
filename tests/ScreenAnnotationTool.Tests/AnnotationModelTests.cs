using System.Windows.Controls;
using System.Windows.Media;
using ScreenAnnotationTool.Drawing;
using Xunit;

namespace ScreenAnnotationTool.Tests;

public class AnnotationModelTests
{
    [WpfFact]
    public void Defaults_AreFreehandRedThickness4()
    {
        var m = new AnnotationModel();
        Assert.Equal(ToolKind.Freehand, m.CurrentTool);
        Assert.Equal(Colors.Red, m.CurrentColor);
        Assert.Equal(4, m.CurrentThickness);
        Assert.Empty(m.Annotations);
    }

    [WpfFact]
    public void Add_AppendsAnnotation()
    {
        var m = new AnnotationModel();
        var item = new Border();
        m.Add(item);
        Assert.Single(m.Annotations);
        Assert.Same(item, m.Annotations[0]);
    }

    [WpfFact]
    public void Undo_RemovesAndReturnsLast()
    {
        var m = new AnnotationModel();
        var a = new Border();
        var b = new Border();
        m.Add(a);
        m.Add(b);
        Assert.Same(b, m.Undo());
        Assert.Single(m.Annotations);
        Assert.Same(a, m.Annotations[0]);
    }

    [WpfFact]
    public void Undo_OnEmpty_ReturnsNull()
    {
        var m = new AnnotationModel();
        Assert.Null(m.Undo());
    }

    [WpfFact]
    public void Clear_EmptiesList()
    {
        var m = new AnnotationModel();
        m.Add(new Border());
        m.Add(new Border());
        m.Clear();
        Assert.Empty(m.Annotations);
    }

    [WpfFact]
    public void Changed_FiresOnAddUndoClear()
    {
        var m = new AnnotationModel();
        int count = 0;
        m.Changed += (_, _) => count++;
        m.Add(new Border()); // 1
        m.Undo();            // 2
        m.Clear();           // 3
        Assert.Equal(3, count);
    }

    [WpfFact]
    public void Changed_DescribesIncrementalMutation()
    {
        var m = new AnnotationModel();
        var item = new Border();
        var changes = new List<AnnotationChangedEventArgs>();
        m.Changed += (_, e) => changes.Add(e);

        m.Add(item);
        m.Undo();
        m.Clear();

        Assert.Collection(
            changes,
            change =>
            {
                Assert.Equal(AnnotationChangeKind.Added, change.Kind);
                Assert.Same(item, change.Annotation);
            },
            change =>
            {
                Assert.Equal(AnnotationChangeKind.Removed, change.Kind);
                Assert.Same(item, change.Annotation);
            },
            change =>
            {
                Assert.Equal(AnnotationChangeKind.Cleared, change.Kind);
                Assert.Null(change.Annotation);
            });
    }
}
