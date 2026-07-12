using System.Windows;

namespace ScreenAnnotationTool;

// Single home for Chalk's message boxes so every error/warning dialog stays consistent.
internal static class Dialogs
{
    public static void Show(string message, MessageBoxImage icon = MessageBoxImage.Error) =>
        MessageBox.Show(message, "Chalk", MessageBoxButton.OK, icon);
}
