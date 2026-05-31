using FFMpegCore;
using Microsoft.Extensions.Configuration;

namespace PickleIQ.Infrastructure.Services;

/// <summary>
/// Ensures FFMpegCore knows where to find ffmpeg.exe, resolving in this order:
///   1. FFmpeg:BinaryFolder config key
///   2. WinGet Gyan.FFmpeg package (auto-detected)
///   3. System PATH (FFMpegCore default — no action needed)
/// </summary>
internal static class FFmpegLocator
{
    private static bool _configured;
    private static readonly Lock _lock = new();

    public static void EnsureConfigured(IConfiguration configuration)
    {
        if (_configured) return;
        lock (_lock)
        {
            if (_configured) return;
            var folder = configuration["FFmpeg:BinaryFolder"] ?? DetectWinGet();
            if (!string.IsNullOrEmpty(folder))
                GlobalFFOptions.Configure(new FFOptions { BinaryFolder = folder });
            _configured = true;
        }
    }

    private static string? DetectWinGet()
    {
        var packagesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WinGet", "Packages");

        if (!Directory.Exists(packagesDir)) return null;

        return Directory.EnumerateDirectories(packagesDir, "Gyan.FFmpeg*")
            .SelectMany(d => Directory.EnumerateDirectories(d, "ffmpeg*"))
            .Select(d => Path.Combine(d, "bin"))
            .FirstOrDefault(d => File.Exists(Path.Combine(d, "ffmpeg.exe")));
    }
}
