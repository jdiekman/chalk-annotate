using System;
using System.IO;
using System.Windows;

namespace ScreenAnnotationTool.Services;

// Region screen capture. All geometry is in raw physical pixels — same rule as the
// overlay's positioning: no WPF DIP conversions anywhere near screen coordinates.
public static class CaptureService
{
    // Drags smaller than this (either axis, after clamping) are treated as accidental.
    public const int MinimumRegionSize = 5;

    // Turn a drag between two physical-pixel points (any direction) into a rect
    // clipped to the given bounds, or null when the result is too small to be a
    // deliberate selection.
    public static Int32Rect? NormalizeRegion(int ax, int ay, int bx, int by, Int32Rect bounds)
    {
        int left = Math.Max(Math.Min(ax, bx), bounds.X);
        int top = Math.Max(Math.Min(ay, by), bounds.Y);
        int right = Math.Min(Math.Max(ax, bx), bounds.X + bounds.Width);
        int bottom = Math.Min(Math.Max(ay, by), bounds.Y + bounds.Height);

        int width = right - left;
        int height = bottom - top;
        if (width < MinimumRegionSize || height < MinimumRegionSize)
            return null;
        return new Int32Rect(left, top, width, height);
    }

    public static string GenerateFileName(DateTime timestamp)
        => $"Chalk {timestamp:yyyy-MM-dd HH.mm.ss}.png";

    // "shot.png" -> "shot (2).png" -> "shot (3).png" until a free name is found.
    public static string MakeUniquePath(string path)
    {
        if (!File.Exists(path))
            return path;

        string directory = Path.GetDirectoryName(path) ?? "";
        string stem = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        for (int n = 2; ; n++)
        {
            string candidate = Path.Combine(directory, $"{stem} ({n}){extension}");
            if (!File.Exists(candidate))
                return candidate;
        }
    }

    public static string DefaultDirectory
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Chalk");

    // Capture the given physical-pixel screen region into a PNG under Pictures\Chalk
    // and put the same image on the clipboard. Returns the saved file's path.
    // Clipboard placement is best-effort: another app briefly holding the clipboard
    // open must not turn an already-saved capture into a failure.
    public static string CaptureToFileAndClipboard(Int32Rect region)
    {
        Directory.CreateDirectory(DefaultDirectory);
        string path = MakeUniquePath(Path.Combine(DefaultDirectory, GenerateFileName(DateTime.Now)));

        using (var bitmap = new System.Drawing.Bitmap(region.Width, region.Height))
        {
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(
                    region.X, region.Y, 0, 0,
                    new System.Drawing.Size(region.Width, region.Height));
            }
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }

        try
        {
            var image = new System.Windows.Media.Imaging.BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path);
            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            Clipboard.SetImage(image);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(
                "Chalk saved the capture but couldn't copy it to the clipboard: {0}", ex);
        }

        return path;
    }
}
