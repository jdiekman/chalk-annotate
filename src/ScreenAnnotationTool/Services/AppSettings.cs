namespace ScreenAnnotationTool.Services;

public class AppSettings
{
    public double ToolbarLeft { get; set; } = 40;
    public double ToolbarTop { get; set; } = 40;
    public string ColorHex { get; set; } = "#FFFF0000";
    public double Thickness { get; set; } = 4;
    public bool LaunchOnStartup { get; set; } = false;
}
