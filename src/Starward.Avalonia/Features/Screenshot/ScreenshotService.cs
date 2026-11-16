using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.GameLauncher;
using Starward.Features.HoYoPlay;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Starward.Features.Screenshot;

public sealed class ScreenshotService
{

    private static readonly string[] Extensions = [".jpg", ".jpeg", ".png", ".webp", ".avif"];

    private readonly ILogger<ScreenshotService> _logger;

    private readonly HoYoPlayService _hoYoPlayService;


    public ScreenshotService(ILogger<ScreenshotService> logger, HoYoPlayService hoYoPlayService)
    {
        _logger = logger;
        _hoYoPlayService = hoYoPlayService;
    }


    public static bool IsSupportedFile(string file)
    {
        return Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase);
    }


    public async Task<(string? BackupFolder, string? ScreenshotFolder)> GetGameScreenshotPathAsync(GameId gameId)
    {
        try
        {
            string? name = GameLauncherService.GetGameExeName(gameId.GameBiz)?.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
            string? relativePath = gameId.GameBiz.Game switch
            {
                GameBiz.hk4e => "ScreenShot",
                GameBiz.hkrpg => Path.Combine("StarRail_Data", "ScreenShots"),
                GameBiz.bh3 => "ScreenShot",
                GameBiz.nap => "ScreenShot",
                _ => null,
            };
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(relativePath))
            {
                var config = await _hoYoPlayService.GetGameConfigAsync(gameId);
                if (config is not null)
                {
                    name ??= config.ExeFileName?.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
                    relativePath ??= config.GameScreenshotDir?.Replace('\\', Path.DirectorySeparatorChar);
                }
            }
            string folder = AppConfig.ScreenshotFolder ?? Path.Combine(AppConfig.UserDataFolder!, "Screenshots");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            folder = Path.Combine(folder, name ?? gameId.GameBiz.Game);
            Directory.CreateDirectory(folder);
            string? backupFolder = folder;
            string? screenshotFolder = null;
            string? installPath = GameLauncherService.GetGameInstallPath(gameId);
            if (!string.IsNullOrWhiteSpace(installPath) && !string.IsNullOrWhiteSpace(relativePath))
            {
                folder = Path.Combine(installPath, relativePath);
                if (Directory.Exists(folder))
                {
                    screenshotFolder = folder;
                }
            }
            return (backupFolder, screenshotFolder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get screenshot path of {gameId}", gameId.Id);
            return (null, null);
        }
    }


    public async Task<List<ScreenshotItem>> GetScreenshotsAsync(GameId gameId)
    {
        (string? backup, string? game) = await GetGameScreenshotPathAsync(gameId);
        List<string> folders = new();
        if (Directory.Exists(backup))
        {
            folders.Add(backup);
        }
        if (Directory.Exists(game))
        {
            folders.Add(game);
        }
        string? external = AppConfig.GetExternalScreenshotFolder(gameId.GameBiz);
        if (Directory.Exists(external))
        {
            folders.Add(external);
        }
        return Scan(folders);
    }


    public List<ScreenshotItem> Scan(IEnumerable<string> folders)
    {
        List<ScreenshotItem> list = new();
        HashSet<string> visited = new(StringComparer.Ordinal);
        foreach (string folder in folders.Where(Directory.Exists).Distinct())
        {
            try
            {
                foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                {
                    if (IsSupportedFile(file) && visited.Add(file))
                    {
                        try
                        {
                            list.Add(new ScreenshotItem(file));
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Scan screenshot folder {folder}", folder);
            }
        }
        return list.OrderByDescending(x => x.CreationTime).ToList();
    }

}
