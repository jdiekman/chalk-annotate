using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace ScreenAnnotationTool.Drawing;

public class AnnotationModel
{
    private readonly List<object> _annotations = new();

    public ToolKind CurrentTool { get; set; } = ToolKind.Freehand;
    public Color CurrentColor { get; set; } = Colors.Red;
    public double CurrentThickness { get; set; } = 4;

    public IReadOnlyList<object> Annotations => _annotations;

    public event EventHandler? Changed;

    public void Add(object annotation)
    {
        _annotations.Add(annotation);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public object? Undo()
    {
        if (_annotations.Count == 0)
            return null;
        var last = _annotations[^1];
        _annotations.RemoveAt(_annotations.Count - 1);
        Changed?.Invoke(this, EventArgs.Empty);
        return last;
    }

    public void Clear()
    {
        _annotations.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
