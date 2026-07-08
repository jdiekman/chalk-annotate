using System;
using Microsoft.Win32;

namespace ScreenAnnotationTool.Services;

public class StartupService
{
    private readonly RegistryKey _rootKey;
    private readonly string _subKeyPath;
    private readonly string _valueName;
    private readonly string _exePath;

    public StartupService(RegistryKey rootKey, string subKeyPath, string valueName, string exePath)
    {
        _rootKey = rootKey;
        _subKeyPath = subKeyPath;
        _valueName = valueName;
        _exePath = exePath;
    }

    public static StartupService CreateDefault() =>
        new StartupService(
            Registry.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Run",
            "Chalk",
            Environment.ProcessPath ?? "");

    public bool IsEnabled()
    {
        using var key = _rootKey.OpenSubKey(_subKeyPath, writable: false);
        var value = key?.GetValue(_valueName) as string;
        return string.Equals(value, _exePath, StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = _rootKey.CreateSubKey(_subKeyPath, writable: true);
        if (key == null) return;
        if (enabled)
            key.SetValue(_valueName, _exePath);
        else if (key.GetValue(_valueName) != null)
            key.DeleteValue(_valueName);
    }
}
