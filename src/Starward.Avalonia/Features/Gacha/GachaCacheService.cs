using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Core.Gacha;
using Starward.Features.Compatibility;
using Starward.Features.GameLauncher;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Starward.Features.Gacha;

public sealed record GachaCacheResult(string Url, string FilePath, DateTime LastWriteTime, string GameRoot);

/// <summary>
/// Locates the game's Chromium web cache, including game folders inside Wine/Proton prefixes.
/// </summary>
public sealed class GachaCacheService
{

    private readonly ILogger<GachaCacheService> _logger;

    private readonly CompatLayerService _compatLayerService;

    private readonly SteamLibraryService _steamLibraryService;


    public GachaCacheService(
        ILogger<GachaCacheService> logger,
        CompatLayerService compatLayerService,
        SteamLibraryService steamLibraryService)
    {
        _logger = logger;
        _compatLayerService = compatLayerService;
        _steamLibraryService = steamLibraryService;
    }


    public GachaCacheResult? Find(GameBiz gameBiz)
    {
        string dataFolder = GetDataFolderName(gameBiz);
        foreach (string root in GetCandidateGameRoots(gameBiz, dataFolder).Distinct(StringComparer.Ordinal))
        {
            try
            {
                foreach (string file in GachaLogClient.GetGachaCacheFiles(gameBiz, root))
                {
                    string? url = GachaLogClient.FindGachaUrlInCacheFile(file);
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        DateTime time = File.GetLastWriteTime(file);
                        _logger.LogInformation("Found gacha URL in game web cache {file}", file);
                        return new GachaCacheResult(url, file, time, root);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Read gacha cache under {root}", root);
            }
        }
        return null;
    }


    private IEnumerable<string> GetCandidateGameRoots(GameBiz gameBiz, string dataFolder)
    {
        string? installPath = GameLauncherService.GetGameInstallPath(gameBiz);
        if (!string.IsNullOrWhiteSpace(installPath))
        {
            string resolved = ResolveWinePath(installPath, gameBiz);
            if (Directory.Exists(resolved))
            {
                yield return resolved;
            }
        }

        if (AppConfig.IsLinux)
        {
            string prefix = _compatLayerService.GetPrefixPath(gameBiz);
            foreach (string root in FindGameRoots(Path.Combine(prefix, "drive_c"), dataFolder, 3))
            {
                yield return root;
            }
        }

        foreach (string common in _steamLibraryService.GetCommonGameFolders())
        {
            foreach (string root in FindGameRoots(common, dataFolder, 2))
            {
                yield return root;
            }
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (string parent in new[]
                 {
                     Path.Combine(home, "Games"),
                     Path.Combine(home, "Game"),
                     Path.Combine(home, "Genshin Impact"),
                     Path.Combine(home, "Star Rail"),
                     Path.Combine(home, "ZenlessZoneZero"),
                     Path.Combine(AppConfig.CacheFolder, "games"),
                     "/mnt",
                 })
        {
            foreach (string root in FindGameRoots(parent, dataFolder, 2))
            {
                yield return root;
            }
        }
    }


    private static IEnumerable<string> FindGameRoots(string? parent, string dataFolder, int depth)
    {
        if (string.IsNullOrWhiteSpace(parent) || depth < 0 || !Directory.Exists(parent))
        {
            yield break;
        }
        if (Path.GetFileName(parent.TrimEnd(Path.DirectorySeparatorChar)).Equals(dataFolder, StringComparison.OrdinalIgnoreCase))
        {
            string? root = Path.GetDirectoryName(parent);
            if (root is not null)
            {
                yield return root;
            }
            yield break;
        }
        if (Directory.Exists(Path.Combine(parent, dataFolder)))
        {
            yield return parent;
        }
        if (depth == 0)
        {
            yield break;
        }
        IEnumerable<string> children;
        try
        {
            children = Directory.EnumerateDirectories(parent);
        }
        catch
        {
            yield break;
        }
        foreach (string child in children)
        {
            foreach (string root in FindGameRoots(child, dataFolder, depth - 1))
            {
                yield return root;
            }
        }
    }


    private string ResolveWinePath(string path, GameBiz gameBiz)
    {
        if (!AppConfig.IsLinux || path.Length < 3 || path[1] != ':')
        {
            return path.Replace('\\', '/');
        }
        char drive = char.ToLowerInvariant(path[0]);
        string rest = path[2..].TrimStart('\\', '/').Replace('\\', '/');
        if (drive == 'z')
        {
            return Path.Combine("/", rest);
        }
        string prefix = _compatLayerService.GetPrefixPath(gameBiz);
        string dosDevice = Path.Combine(prefix, "dosdevices", $"{drive}:");
        if (Directory.Exists(dosDevice))
        {
            return Path.GetFullPath(Path.Combine(dosDevice, rest));
        }
        return Path.Combine(prefix, $"drive_{drive}", rest);
    }


    private static string GetDataFolderName(GameBiz gameBiz)
    {
        return gameBiz.Value switch
        {
            GameBiz.hk4e_cn or GameBiz.hk4e_bilibili => "YuanShen_Data",
            GameBiz.hk4e_global => "GenshinImpact_Data",
            GameBiz.hkrpg_cn or GameBiz.hkrpg_global or GameBiz.hkrpg_bilibili => "StarRail_Data",
            GameBiz.nap_cn or GameBiz.nap_global or GameBiz.nap_bilibili => "ZenlessZoneZero_Data",
            _ => throw new ArgumentOutOfRangeException(nameof(gameBiz), gameBiz, "Unsupported game"),
        };
    }

}
