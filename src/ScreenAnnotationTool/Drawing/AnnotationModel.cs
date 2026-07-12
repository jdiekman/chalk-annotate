using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ScreenAnnotationTool.Drawing;

public class AnnotationModel
{
    private readonly List<UIElement> _annotations = new();

    public ToolKind CurrentTool { get; set; } = ToolKind.Freehand;
    public Color CurrentColor { get; set; } = Colors.Red;
    public double CurrentThickness { get; set; } = 4;

    public IReadOnlyList<UIElement> Annotations => _annotations;

    public event EventHandler<AnnotationChangedEventArgs>? Changed;

    public void Add(UIElement annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        _annotations.Add(annotation);
        Changed?.Invoke(this, new AnnotationChangedEventArgs(AnnotationChangeKind.Added, annotation));
    }

    public UIElement? Undo()
    {
        if (_annotations.Count == 0)
            return null;
        var last = _annotations[^1];
        _annotations.RemoveAt(_annotations.Count - 1);
        Changed?.Invoke(this, new AnnotationChangedEventArgs(AnnotationChangeKind.Removed, last));
        return last;
    }

    public void Clear()
    {
        _annotations.Clear();
        Changed?.Invoke(this, new AnnotationChangedEventArgs(AnnotationChangeKind.Cleared));
    }
}

public enum AnnotationChangeKind
{
    Added,
    Removed,
    Cleared
}

public sealed class AnnotationChangedEventArgs : EventArgs
{
    public AnnotationChangedEventArgs(AnnotationChangeKind kind, UIElement? annotation = null)
    {
        Kind = kind;
        Annotation = annotation;
    }

    public AnnotationChangeKind Kind { get; }
    public UIElement? Annotation { get; }
}
