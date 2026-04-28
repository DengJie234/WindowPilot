using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowPilot.Models;

namespace WindowPilot.Services;

public sealed class AppIconService
{
    private readonly ConcurrentDictionary<string, ImageSource> _iconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<ImageSource>> _loadingTasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ImageSource _defaultIcon;

    public AppIconService()
    {
        _defaultIcon = LoadDefaultIcon();
    }

    public ImageSource DefaultIcon => _defaultIcon;

    public ImageSource GetCachedIcon(WindowInfo window)
    {
        var key = GetCacheKey(window);
        return !string.IsNullOrWhiteSpace(key) && _iconCache.TryGetValue(key, out var icon)
            ? icon
            : _defaultIcon;
    }

    public async Task<ImageSource> GetIconForWindowAsync(WindowInfo window)
    {
        var key = GetCacheKey(window);
        if (string.IsNullOrWhiteSpace(key))
        {
            return _defaultIcon;
        }

        if (_iconCache.TryGetValue(key, out var cachedIcon))
        {
            return cachedIcon;
        }

        var task = _loadingTasks.GetOrAdd(key, _ => Task.Run(() => LoadIcon(window)));
        try
        {
            var icon = await task.ConfigureAwait(false);
            _iconCache[key] = icon;
            return icon;
        }
        finally
        {
            _loadingTasks.TryRemove(key, out _);
        }
    }

    public ImageSource GetIconFromProcessPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return _defaultIcon;
        }

        return _iconCache.GetOrAdd(path, _ => ExtractIconFromPath(path));
    }

    private ImageSource LoadIcon(WindowInfo window)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(window.ProcessPath) && File.Exists(window.ProcessPath))
            {
                return ExtractIconFromPath(window.ProcessPath);
            }
        }
        catch
        {
            // Fall through to default icon. Window processes can exit or deny access at any time.
        }

        return _defaultIcon;
    }

    private ImageSource ExtractIconFromPath(string path)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(path);
            return icon is null ? _defaultIcon : ConvertIconToImageSource(icon);
        }
        catch
        {
            return _defaultIcon;
        }
    }

    private ImageSource LoadDefaultIcon()
    {
        var defaultIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "DefaultAppIcon.ico");
        if (File.Exists(defaultIconPath))
        {
            try
            {
                using var icon = new Icon(defaultIconPath);
                return ConvertIconToImageSource(icon);
            }
            catch
            {
                // Continue to app icon fallback.
            }
        }

        var appIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(appIconPath))
        {
            try
            {
                using var icon = new Icon(appIconPath);
                return ConvertIconToImageSource(icon);
            }
            catch
            {
                // Continue to system fallback.
            }
        }

        return ConvertIconToImageSource(SystemIcons.Application);
    }

    private static ImageSource ConvertIconToImageSource(Icon icon)
    {
        var source = Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromWidthAndHeight(24, 24));
        source.Freeze();
        return source;
    }

    private static string GetCacheKey(WindowInfo window)
    {
        if (!string.IsNullOrWhiteSpace(window.ProcessPath))
        {
            return window.ProcessPath;
        }

        return !string.IsNullOrWhiteSpace(window.ProcessName)
            ? window.ProcessName
            : window.ProcessId.ToString();
    }
}
