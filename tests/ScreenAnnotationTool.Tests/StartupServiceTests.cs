using Microsoft.Win32;
using ScreenAnnotationTool.Services;
using Xunit;

namespace ScreenAnnotationTool.Tests;

public class StartupServiceTests
{
    private const string ValueName = "SAT_Test";
    private const string ExePath = @"C:\fake folder\app.exe";

    private static string NewTestSubKey() => @"Software\ScreenAnnotationTool_Test_" + Guid.NewGuid().ToString("N");

    private static StartupService Make(string subKey) =>
        new(Registry.CurrentUser, subKey, ValueName, ExePath);

    [Fact]
    public void Default_IsDisabled()
    {
        var subKey = NewTestSubKey();
        try
        {
            Assert.False(Make(subKey).IsEnabled());
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void Enable_ThenIsEnabledTrue()
    {
        var subKey = NewTestSubKey();
        try
        {
            var svc = Make(subKey);
            svc.SetEnabled(true);
            Assert.True(svc.IsEnabled());

            using var key = Registry.CurrentUser.OpenSubKey(subKey);
            Assert.Equal($"\"{ExePath}\"", key?.GetValue(ValueName));

            svc.SetEnabled(false);
            Assert.False(svc.IsEnabled());
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void IsEnabled_AcceptsLegacyUnquotedValue()
    {
        var subKey = NewTestSubKey();
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(subKey))
                key.SetValue(ValueName, ExePath);

            Assert.True(Make(subKey).IsEnabled());
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false);
        }
    }
}
