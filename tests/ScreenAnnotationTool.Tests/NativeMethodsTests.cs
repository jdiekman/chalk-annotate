using ScreenAnnotationTool.Interop;
using Xunit;

namespace ScreenAnnotationTool.Tests;

public class NativeMethodsTests
{
    [Fact]
    public void MakeClickThrough_RejectsMissingWindowHandle()
    {
        Assert.Throws<ArgumentException>(() => NativeMethods.MakeClickThrough(IntPtr.Zero));
    }

    [Fact]
    public void MakeInteractive_RejectsMissingWindowHandle()
    {
        Assert.Throws<ArgumentException>(() => NativeMethods.MakeInteractive(IntPtr.Zero));
    }
}
