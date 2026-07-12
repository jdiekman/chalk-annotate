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
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var path = TempFile();
        var svc = new SettingsService(path);
        try
        {
            svc.Save(new AppSettings
            {
                ToolbarLeft = 100,
                ToolbarTop = 200,
                ColorHex = "#FF00FF00",
                Thickness = 8
            });

            var loaded = new SettingsService(path).Load();
            Assert.Equal(100, loaded.ToolbarLeft);
            Assert.Equal(200, loaded.ToolbarTop);
            Assert.Equal("#FF00FF00", loaded.ColorHex);
            Assert.Equal(8, loaded.Thickness);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        var path = TempFile();
        try
        {
            File.WriteAllText(path, "{ not valid json");
            var s = new SettingsService(path).Load();
            Assert.Equal("#FFFF0000", s.ColorHex);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("{\"ToolbarLeft\":1e308}")]
    [InlineData("{\"ToolbarTop\":-1e308}")]
    [InlineData("{\"Thickness\":-10}")]
    [InlineData("{\"Thickness\":1e308}")]
    public void Load_OutOfRangeNumbers_NormalizesToSafeDefaults(string json)
    {
        var path = TempFile();
        try
        {
            File.WriteAllText(path, json);
            var settings = new SettingsService(path).Load();

            Assert.InRange(settings.ToolbarLeft, int.MinValue, int.MaxValue);
            Assert.InRange(settings.ToolbarTop, int.MinValue, int.MaxValue);
            Assert.InRange(settings.Thickness, AppSettings.MinThickness, AppSettings.MaxThickness);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Save_WhenDestinationCannotBeCreated_PropagatesFailure()
    {
        var blocker = TempFile();
        try
        {
            File.WriteAllText(blocker, "not a directory");
            var service = new SettingsService(Path.Combine(blocker, "settings.json"));

            Assert.ThrowsAny<IOException>(() => service.Save(new AppSettings()));
        }
        finally
        {
            File.Delete(blocker);
        }
    }

    [Fact]
    public void Load_OversizedFile_ReturnsDefaultsWithoutParsing()
    {
        var path = TempFile();
        try
        {
            File.WriteAllText(path, new string('x', 65 * 1024));

            var settings = new SettingsService(path).Load();

            Assert.Equal(AppSettings.DefaultThickness, settings.Thickness);
            Assert.Equal("#FFFF0000", settings.ColorHex);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
