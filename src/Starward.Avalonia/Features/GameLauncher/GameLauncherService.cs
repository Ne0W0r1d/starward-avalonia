using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.Compatibility;
using Starward.Features.HoYoPlay;
using Starward.Features.PlayTime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Starward.Features.GameLauncher;

public sealed partial class GameLauncherService
{

    private readonly ILogger<GameLauncherService> _logger;

    private readonly HoYoPlayService _hoYoPlayService;

    private readonly CompatLayerService _compatLayerService;

    private readonly PlayTimeRecordService _playTimeRecordService;


    public GameLauncherService(
        ILogger<GameLauncherService> logger,
        HoYoPlayService hoYoPlayService,
        CompatLayerService compatLayerService,
        PlayTimeRecordService playTimeRecordService)
    {
        _logger = logger;
        _hoYoPlayService = hoYoPlayService;
        _compatLayerService = compatLayerService;
        _playTimeRecordService = playTimeRecordService;
    }


    public static string? GetGameInstallPath(GameId gameId) => GetGameInstallPath(gameId.GameBiz);


    public static string? GetGameInstallPath(GameBiz gameBiz)
    {
        string? path = AppConfig.GetGameInstallPath(gameBiz);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        path = GetFullPathIfRelativePath(path);
        if (Directory.Exists(path))
        {
            return Path.GetFullPath(path);
        }
        if (AppConfig.GetGameInstallPathRemovable(gameBiz))
        {
            return path;
        }
        ChangeGameInstallPath(gameBiz, null);
        return null;
    }


    public async Task<Version?> GetLocalGameVersionAsync(GameId gameId, string? installPath = null)
    {
        installPath ??= GetGameInstallPath(gameId);
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return null;
        }
        string config = Path.Combine(installPath, "config.ini");
        if (!File.Exists(config))
        {
            _logger.LogWarning("config.ini not found: {path}", config);
            return null;
        }
        string text = await File.ReadAllTextAsync(config);
        MatchCollection matches = GameVersionRegex().Matches(text);
        Version? version = null;
        if (matches.Count > 0)
        {
            _ = Version.TryParse(matches[^1].Groups[1].Value, out version);
        }
        return version;
    }


    public async Task<(Version? Latest, Version? Predownload)> GetLatestGameVersionAsync(GameId gameId)
    {
        try
        {
            GameConfig? config = await _hoYoPlayService.GetGameConfigAsync(gameId);
            if (config is null)
            {
                return (null, null);
            }
            if (config.DefaultDownloadMode is DownloadMode.DOWNLOAD_MODE_CHUNK or DownloadMode.DOWNLOAD_MODE_LDIFF)
            {
                GameBranch? branch = await _hoYoPlayService.GetGameBranchAsync(gameId);
                _ = Version.TryParse(branch?.Main?.Tag, out Version? latest);
                _ = Version.TryParse(branch?.PreDownload?.Tag, out Version? predownload);
                return (latest, predownload);
            }
            GamePackage package = await _hoYoPlayService.GetGamePackageAsync(gameId);
            _ = Version.TryParse(package.Main.Major?.Version, out Version? main);
            _ = Version.TryParse(package.PreDownload.Major?.Version, out Version? pre);
            return (main, pre);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get latest game version {gameId}", gameId.Id);
            return (null, null);
        }
    }


    public async Task<string> GetGameExeNameAsync(GameId gameId)
    {
        string? name = GetGameExeName(gameId.GameBiz);
        if (string.IsNullOrWhiteSpace(name))
        {
            GameConfig? config = await _hoYoPlayService.GetGameConfigAsync(gameId);
            name = config?.ExeFileName;
        }
        return name ?? throw new ArgumentOutOfRangeException($"Unknown game ({gameId.Id}, {gameId.GameBiz}).");
    }


    public static string? GetGameExeName(GameBiz gameBiz)
    {
        return gameBiz.Value switch
        {
            GameBiz.hk4e_cn or GameBiz.hk4e_bilibili => "YuanShen.exe",
            GameBiz.hk4e_global => "GenshinImpact.exe",
            _ => gameBiz.Game switch
            {
                GameBiz.hkrpg => "StarRail.exe",
                GameBiz.bh3 => "BH3.exe",
                GameBiz.nap => "ZenlessZoneZero.exe",
                _ => null,
            },
        };
    }


    public async Task<bool> IsGameExeExistsAsync(GameId gameId, string? installPath = null)
    {
        installPath ??= GetGameInstallPath(gameId);
        if (!string.IsNullOrWhiteSpace(installPath))
        {
            string exe = Path.Combine(installPath, await GetGameExeNameAsync(gameId));
            return File.Exists(exe);
        }
        return false;
    }


    public async Task<Process?> GetGameProcessAsync(GameId gameId)
    {
        string name = await GetGameExeNameAsync(gameId);
        return FindGameProcess(name);
    }


    public static Process? FindGameProcess(string exeName)
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                foreach (string dir in Directory.EnumerateDirectories("/proc"))
                {
                    if (!int.TryParse(Path.GetFileName(dir), out int pid))
                    {
                        continue;
                    }
                    try
                    {
                        string cmdline = File.ReadAllText(Path.Combine(dir, "cmdline"));
                        string? argv0 = cmdline.Split('\0').FirstOrDefault(x => x.Length > 0);
                        string? processFile = argv0 is null ? null : Path.GetFileName(argv0.Replace('\\', '/').Trim('"'));
                        if (processFile?.Equals(exeName, StringComparison.OrdinalIgnoreCase) is true)
                        {
                            return Process.GetProcessById(pid);
                        }
                    }
                    catch
                    {
                        // The process may have exited while /proc was being inspected.
                    }
                }
                return null;
            }
            return Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exeName))
                          .FirstOrDefault(x =>
                          {
                              try { return !x.HasExited; }
                              catch { return false; }
                          });
        }
        catch
        {
            return null;
        }
    }


    public async Task<Process?> StartGameAsync(GameId gameId, string? installPath = null)
    {
        string? exe = null;
        bool thirdPartyTool = false;
        if (!string.IsNullOrWhiteSpace(installPath))
        {
            string candidate = Path.Combine(installPath, await GetGameExeNameAsync(gameId));
            if (File.Exists(candidate))
            {
                exe = candidate;
            }
        }
        if (string.IsNullOrWhiteSpace(exe) && AppConfig.GetEnableThirdPartyTool(gameId.GameBiz))
        {
            string? tool = GetThirdPartyToolPath(gameId);
            if (File.Exists(tool))
            {
                exe = tool;
                thirdPartyTool = true;
            }
        }
        if (string.IsNullOrWhiteSpace(exe))
        {
            string? folder = GetGameInstallPath(gameId);
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new DirectoryNotFoundException(Localization.Get("Avalonia_Error_GamePathNotConfigured"));
            }
            exe = Path.Combine(folder, await GetGameExeNameAsync(gameId));
            if (!File.Exists(exe))
            {
                throw new FileNotFoundException(Localization.Get("Avalonia_Error_GameExeMissing"), exe);
            }
        }

        string? argument = AppConfig.GetStartArgument(gameId.GameBiz)?.Trim();
        if (AppConfig.GetUsePopupWindow(gameId.GameBiz))
        {
            argument += " -popupwindow";
        }
        if (AppConfig.GetEnableDX12(gameId.GameBiz))
        {
            argument += " -use-d3d12";
        }

        ProcessStartInfo info;
        if (OperatingSystem.IsLinux())
        {
            CompatLayer layer = await _compatLayerService.GetSelectedLayerAsync(gameId.GameBiz)
                                ?? throw new InvalidOperationException(Localization.Get("Avalonia_Error_NoCompatLayer"));
            string prefix = _compatLayerService.GetPrefixPath(gameId.GameBiz);
            info = _compatLayerService.CreateStartInfo(layer, exe, argument, Path.GetDirectoryName(exe)!, prefix, gameId.GameBiz);
            _logger.LogInformation("Start game with {layer} ({kind})\r\npath: {exe}\r\narg: {arg}\r\nprefix: {prefix}",
                                   layer.DisplayName, layer.KindText, exe, argument, prefix);
        }
        else
        {
            info = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = argument,
                WorkingDirectory = Path.GetDirectoryName(exe),
                UseShellExecute = true,
                Verb = "runas",
            };
            _logger.LogInformation("Start game\r\npath: {exe}\r\narg: {arg}", exe, argument);
        }

        Process? process = Process.Start(info);
        if (process is not null)
        {
            if (!thirdPartyTool)
            {
                _ = _playTimeRecordService.TrackLaunchedGameAsync(gameId.GameBiz, process);
            }
        }
        return process;
    }


    public static string? ChangeGameInstallPath(GameId gameId, string? path) => ChangeGameInstallPath(gameId.GameBiz, path);


    public static string? ChangeGameInstallPath(GameBiz gameBiz, string? path)
    {
        if (Directory.Exists(path))
        {
            path = Path.GetFullPath(path);
            AppConfig.SetGameInstallPath(gameBiz, path);
            AppConfig.SetGameInstallPathRemovable(gameBiz, false);
        }
        else
        {
            path = null;
            AppConfig.SetGameInstallPath(gameBiz, null);
            AppConfig.SetGameInstallPathRemovable(gameBiz, false);
        }
        return path;
    }


    public static string GetFullPathIfRelativePath(string path)
    {
        if (Path.IsPathFullyQualified(path))
        {
            return Path.GetFullPath(path);
        }
        return Path.GetFullPath(path, AppConfig.CacheFolder);
    }


    public static string? GetThirdPartyToolPath(GameId gameId)
    {
        string? path = AppConfig.GetThirdPartyToolPath(gameId.GameBiz);
        if (!string.IsNullOrWhiteSpace(path))
        {
            path = GetFullPathIfRelativePath(path);
        }
        if (File.Exists(path))
        {
            return path;
        }
        AppConfig.SetThirdPartyToolPath(gameId.GameBiz, null);
        return null;
    }


    public static string? SetThirdPartyToolPath(GameId gameId, string? path)
    {
        if (File.Exists(path))
        {
            path = Path.GetFullPath(path);
            AppConfig.SetThirdPartyToolPath(gameId.GameBiz, path);
        }
        else
        {
            path = null;
            AppConfig.SetThirdPartyToolPath(gameId.GameBiz, null);
        }
        return path;
    }


    /// <summary>
    /// 在 Steam 库、常见游戏目录中查找已安装的游戏。
    /// </summary>
    public async Task<IReadOnlyList<(GameBiz GameBiz, string Path)>> AutoDetectInstalledGamesAsync()
    {
        List<(GameBiz, string)> result = new();
        HashSet<string> visited = new(StringComparer.Ordinal);
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        List<string> roots = new()
        {
            Path.Combine(home, "Games"),
            Path.Combine(home, "Game"),
            Path.Combine(home, "Genshin Impact"),
            Path.Combine(home, "Star Rail"),
            Path.Combine(home, "ZenlessZoneZero"),
            Path.Combine(AppConfig.CacheFolder, "games"),
            "/mnt",
        };
        if (OperatingSystem.IsLinux())
        {
            try
            {
                roots.AddRange(SteamLibraryServiceAccessor.GetCommonGameFolders());
            }
            catch
            {
                // ignored
            }
        }
        Dictionary<string, GameBiz> exeMap = new(StringComparer.OrdinalIgnoreCase);
        foreach (GameBiz biz in GameBiz.AllGameBizs)
        {
            string? exe = GetGameExeName(biz);
            if (exe is not null)
            {
                exeMap[exe] = biz;
            }
        }
        await Task.Run(() =>
        {
            foreach (string root in roots.Where(Directory.Exists))
            {
                foreach (string file in EnumerateFilesSafe(root, 4))
                {
                    string name = Path.GetFileName(file);
                    if (exeMap.TryGetValue(name, out GameBiz biz))
                    {
                        string? dir = Path.GetDirectoryName(file);
                        if (dir is not null && visited.Add(dir))
                        {
                            result.Add((biz, dir));
                        }
                    }
                }
            }
        });
        foreach ((GameBiz biz, string path) in result)
        {
            if (string.IsNullOrWhiteSpace(AppConfig.GetGameInstallPath(biz)))
            {
                ChangeGameInstallPath(biz, path);
            }
        }
        return result;
    }


    private static IEnumerable<string> EnumerateFilesSafe(string root, int depth)
    {
        if (depth < 0)
        {
            yield break;
        }
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(root);
        }
        catch
        {
            yield break;
        }
        foreach (string file in files)
        {
            yield return file;
        }
        IEnumerable<string> dirs;
        try
        {
            dirs = Directory.EnumerateDirectories(root);
        }
        catch
        {
            yield break;
        }
        foreach (string dir in dirs)
        {
            string name = Path.GetFileName(dir);
            if (name is "drive_c" or "windows" or "Program Files" or "Program Files (x86)")
            {
                continue;
            }
            foreach (string file in EnumerateFilesSafe(dir, depth - 1))
            {
                yield return file;
            }
        }
    }


    [GeneratedRegex(@"game_version=(.+)")]
    private static partial Regex GameVersionRegex();

}


internal static class SteamLibraryServiceAccessor
{

    public static IReadOnlyList<string> GetCommonGameFolders()
    {
        return AppConfig.GetService<Starward.Features.Compatibility.SteamLibraryService>().GetCommonGameFolders();
    }

}
