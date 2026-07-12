namespace ScreenAnnotationTool.Services;

public class AppSettings
{
    public const double DefaultToolbarLeft = 40;
    public const double DefaultToolbarTop = 40;
    public const double DefaultThickness = 4;
    public const double MinThickness = 1;
    public const double MaxThickness = 32;

    public double ToolbarLeft { get; set; } = DefaultToolbarLeft;
    public double ToolbarTop { get; set; } = DefaultToolbarTop;
    public string ColorHex { get; set; } = "#FFFF0000";
    public double Thickness { get; set; } = DefaultThickness;

    public void Normalize()
    {
        ToolbarLeft = ClampOrDefault(ToolbarLeft, int.MinValue, int.MaxValue, DefaultToolbarLeft);
        ToolbarTop = ClampOrDefault(ToolbarTop, int.MinValue, int.MaxValue, DefaultToolbarTop);
        Thickness = ClampOrDefault(Thickness, MinThickness, MaxThickness, DefaultThickness);
        if (string.IsNullOrWhiteSpace(ColorHex) || ColorHex.Length > 32)
            ColorHex = "#FFFF0000";
    }

    private static double ClampOrDefault(double value, double min, double max, double fallback) =>
        !double.IsFinite(value) || value < min || value > max ? fallback : value;
}
