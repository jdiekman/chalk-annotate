using System.IO;
using ScreenAnnotationTool.Services;
using Xunit;

namespace ScreenAnnotationTool.Tests;

public class SettingsServiceTests
{
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), "sat_test_" + Path.GetRandomFileName() + ".json");

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var svc = new SettingsService(TempFile());
        var s = svc.Load();
        Assert.Equal("#FFFF0000", s.ColorHex);
        Assert.Equal(4, s.Thickness);
        Assert.False(s.LaunchOnStartup);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var path = TempFile();
        var svc = new SettingsService(path);
        svc.Save(new AppSettings
        {
            ToolbarLeft = 100, ToolbarTop = 200,
            ColorHex = "#FF00FF00", Thickness = 8, LaunchOnStartup = true
        });

        var loaded = new SettingsService(path).Load();
        Assert.Equal(100, loaded.ToolbarLeft);
        Assert.Equal(200, loaded.ToolbarTop);
        Assert.Equal("#FF00FF00", loaded.ColorHex);
        Assert.Equal(8, loaded.Thickness);
        Assert.True(loaded.LaunchOnStartup);

        File.Delete(path);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        var path = TempFile();
        File.WriteAllText(path, "{ not valid json");
        var s = new SettingsService(path).Load();
        Assert.Equal("#FFFF0000", s.ColorHex);
        File.Delete(path);
    }
}
