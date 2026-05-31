using FFMpegCore;
using Microsoft.Extensions.Configuration;

namespace PickleIQ.Infrastructure.Services;

/// <summary>
/// Resolves the FFmpeg binary folder and returns FFOptions for per-call use.
/// Resolution order: FFmpeg:BinaryFolder config → WinGet auto-detect → null (PATH).
/// </summary>
internal static class FFmpegLocator
{
    private static string? _resolvedFolder;
    private static bool _resolved;
    private static readonly Lock _lock = new();

    public static FFOptions GetOptions(IConfiguration configuration)
    {
        if (!_resolved)
        {
            lock (_lock)
            {
                if (!_resolved)
                {
                    _resolvedFolder = configuration["FFmpeg:BinaryFolder"] ?? DetectWinGet();
                    _resolved = true;
                }
            }
        }

        return _resolvedFolder is not null
            ? new FFOptions { BinaryFolder = _resolvedFolder }
            : new FFOptions();
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
