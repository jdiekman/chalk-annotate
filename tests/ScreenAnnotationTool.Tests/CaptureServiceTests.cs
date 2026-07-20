using System.IO;
using System.Windows;
using ScreenAnnotationTool.Services;
using Xunit;

namespace ScreenAnnotationTool.Tests;

public class CaptureServiceTests
{
    private static readonly Int32Rect Monitor = new(5040, 0, 6720, 2800);

    [Fact]
    public void NormalizeRegion_ReturnsRectForForwardDrag()
    {
        var region = CaptureService.NormalizeRegion(5100, 100, 5500, 400, Monitor);
        Assert.Equal(new Int32Rect(5100, 100, 400, 300), region);
    }

    [Fact]
    public void NormalizeRegion_NormalizesReversedDrag()
    {
        var region = CaptureService.NormalizeRegion(5500, 400, 5100, 100, Monitor);
        Assert.Equal(new Int32Rect(5100, 100, 400, 300), region);
    }

    [Fact]
    public void NormalizeRegion_ClampsToBounds()
    {
        // Drag that overshoots the monitor on every side is clipped to the monitor.
        var region = CaptureService.NormalizeRegion(4000, -50, 12500, 3000, Monitor);
        Assert.Equal(Monitor, region);
    }

    [Fact]
    public void NormalizeRegion_RejectsTinyDrags()
    {
        Assert.Null(CaptureService.NormalizeRegion(5100, 100, 5102, 102, Monitor));
    }

    [Fact]
    public void NormalizeRegion_RejectsRegionsFullyOutsideBounds()
    {
        Assert.Null(CaptureService.NormalizeRegion(0, 0, 400, 300, Monitor));
    }

    [Fact]
    public void GenerateFileName_UsesSortableTimestampWithoutInvalidChars()
    {
        var name = CaptureService.GenerateFileName(new DateTime(2026, 7, 20, 9, 5, 30));
        Assert.Equal("Chalk 2026-07-20 09.05.30.png", name);
        Assert.Equal(-1, name.IndexOfAny(Path.GetInvalidFileNameChars()));
    }

    [Fact]
    public void MakeUniquePath_ReturnsOriginalWhenFree()
    {
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "shot.png");
            Assert.Equal(path, CaptureService.MakeUniquePath(path));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void MakeUniquePath_AppendsCounterWhenTaken()
    {
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "shot.png");
            File.WriteAllText(path, "x");
            File.WriteAllText(Path.Combine(dir, "shot (2).png"), "x");
            Assert.Equal(Path.Combine(dir, "shot (3).png"), CaptureService.MakeUniquePath(path));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
