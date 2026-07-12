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
            Environment.ProcessPath ?? throw new InvalidOperationException("The Chalk executable path is unavailable."));

    public bool IsEnabled()
    {
        using var key = _rootKey.OpenSubKey(_subKeyPath, writable: false);
        var value = key?.GetValue(_valueName) as string;
        if (value == null)
            return false;

        var command = value.Trim();
        return string.Equals(command, _exePath, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(command, QuoteExecutable(_exePath), StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = _rootKey.CreateSubKey(_subKeyPath, writable: true);
        if (key == null)
            throw new InvalidOperationException("Windows did not open the startup registry key.");
        if (enabled)
            key.SetValue(_valueName, QuoteExecutable(_exePath), RegistryValueKind.String);
        else if (key.GetValue(_valueName) != null)
            key.DeleteValue(_valueName);
    }

    private static string QuoteExecutable(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("The Chalk executable path is empty.");
        if (path.Contains('"'))
            throw new InvalidOperationException("The Chalk executable path contains an invalid quote character.");
        return $"\"{path}\"";
    }
}
