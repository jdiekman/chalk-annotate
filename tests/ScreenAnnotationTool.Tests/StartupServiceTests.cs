using Microsoft.Win32;
using ScreenAnnotationTool.Services;
using Xunit;

namespace ScreenAnnotationTool.Tests;

public class StartupServiceTests
{
    private const string TestSubKey = @"Software\ScreenAnnotationTool_Test";

    private static StartupService Make() =>
        new StartupService(Registry.CurrentUser, TestSubKey, "SAT_Test", @"C:\fake\app.exe");

    [Fact]
    public void Default_IsDisabled()
    {
        Registry.CurrentUser.DeleteSubKeyTree(TestSubKey, throwOnMissingSubKey: false);
        var svc = Make();
        Assert.False(svc.IsEnabled());
    }

    [Fact]
    public void Enable_ThenIsEnabledTrue()
    {
        var svc = Make();
        svc.SetEnabled(true);
        Assert.True(svc.IsEnabled());
        svc.SetEnabled(false);
        Assert.False(svc.IsEnabled());
        Registry.CurrentUser.DeleteSubKeyTree(TestSubKey, throwOnMissingSubKey: false);
    }
}
