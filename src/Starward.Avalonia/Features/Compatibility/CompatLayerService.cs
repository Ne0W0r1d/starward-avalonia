using Microsoft.Extensions.Logging;
using Starward.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Starward.Features.Compatibility;

/// <summary>
/// 检测 Linux 上可用的 Wine / Proton 兼容层，并负责为游戏进程准备环境变量。
/// </summary>
public sealed class CompatLayerService
{

    private readonly ILogger<CompatLayerService> _logger;

    private readonly SteamLibraryService _steamLibraryService;

    private readonly object _lock = new();

    private List<CompatLayer>? _cachedLayers;

    private DateTime _cacheTime;


    public CompatLayerService(ILogger<CompatLayerService> logger, SteamLibraryService steamLibraryService)
    {
        _logger = logger;
        _steamLibraryService = steamLibraryService;
    }


    public bool IsSupported => OperatingSystem.IsLinux();


    public async Task<IReadOnlyList<CompatLayer>> DetectAsync(bool forceRefresh = false)
    {
        if (!IsSupported)
        {
            return [];
        }
        lock (_lock)
        {
            if (!forceRefresh && _cachedLayers is not null && DateTime.UtcNow - _cacheTime < TimeSpan.FromMinutes(2))
            {
                return _cachedLayers;
            }
        }
        List<CompatLayer> layers = await Task.Run(DetectInternal);
        layers = layers.DistinctBy(x => x.Id)
                       .OrderBy(x => x.IsProton ? 0 : 1)
                       .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                       .ToList();
        lock (_lock)
        {
            _cachedLayers = layers;
            _cacheTime = DateTime.UtcNow;
        }
        return layers;
    }


    public async Task<CompatLayer?> GetSelectedLayerAsync(GameBiz biz, bool forceRefresh = false)
    {
        IReadOnlyList<CompatLayer> layers = await DetectAsync(forceRefresh);
        string? id = AppConfig.GetCompatLayerId(biz) ?? AppConfig.CompatLayerId;
        CompatLayer? layer = layers.FirstOrDefault(x => x.Id == id);
        return layer ?? layers.FirstOrDefault(x => x.IsProton) ?? layers.FirstOrDefault();
    }


    public string GetPrefixPath(GameBiz biz)
    {
        string? configured = AppConfig.GetWinePrefix(biz);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }
        string root = AppConfig.WinePrefixRoot ?? Path.Combine(AppConfig.CacheFolder, "prefixes");
        return Path.Combine(root, biz.Value);
    }


    public ProcessStartInfo CreateStartInfo(CompatLayer layer, string executable, string? arguments, string workingDirectory, string prefix)
    {
        Directory.CreateDirectory(prefix);
        string steamClientPath = layer.SteamRoot
                                 ?? _steamLibraryService.GetSteamRoots().FirstOrDefault()
                                 ?? prefix;
        return CreateStartInfo(layer,
                               executable,
                               arguments,
                               workingDirectory,
                               prefix,
                               AppConfig.CompatExtraArguments,
                               AppConfig.CompatDisableMenuBuilder,
                               steamClientPath);
    }


    public static ProcessStartInfo CreateStartInfo(
        CompatLayer layer,
        string executable,
        string? arguments,
        string workingDirectory,
        string prefix,
        string? extraArguments,
        bool disableMenuBuilder,
        string? steamClientPath = null)
    {
        ProcessStartInfo psi = new()
        {
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };
        List<string> args = SplitArguments(arguments);
        if (layer.IsProton)
        {
            psi.FileName = layer.ExecutablePath;
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add(executable);
            foreach (string arg in args)
            {
                psi.ArgumentList.Add(arg);
            }
            psi.Environment["STEAM_COMPAT_DATA_PATH"] = prefix;
            psi.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = steamClientPath ?? layer.SteamRoot ?? prefix;
            psi.Environment["SteamDeck"] = "0";
        }
        else
        {
            psi.FileName = layer.ExecutablePath;
            psi.ArgumentList.Add(executable);
            foreach (string arg in args)
            {
                psi.ArgumentList.Add(arg);
            }
            psi.Environment["WINEPREFIX"] = prefix;
        }
        psi.Environment["WINEDEBUG"] = "-all";
        psi.Environment["WINEESYNC"] = "1";
        if (disableMenuBuilder)
        {
            string overrides = psi.Environment.TryGetValue("WINEDLLOVERRIDES", out string? value) ? value + ";" : "";
            psi.Environment["WINEDLLOVERRIDES"] = overrides + "winemenubuilder.exe=d";
        }
        if (!string.IsNullOrWhiteSpace(extraArguments))
        {
            foreach (string arg in SplitArguments(extraArguments))
            {
                psi.ArgumentList.Add(arg);
            }
        }
        return psi;
    }


    /// <summary>
    /// 简单的命令行参数拆分，支持双引号和单引号。
    /// </summary>
    public static List<string> SplitArguments(string? text)
    {
        List<string> result = new();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }
        System.Text.StringBuilder current = new();
        char quote = '\0';
        bool tokenStarted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quote == '\'')
            {
                tokenStarted = true;
                if (c == '\'')
                {
                    quote = '\0';
                }
                else
                {
                    current.Append(c);
                }
                continue;
            }
            if (quote == '"')
            {
                tokenStarted = true;
                if (c == '"')
                {
                    quote = '\0';
                }
                else if (c == '\\' && i + 1 < text.Length && text[i + 1] is '"' or '\\')
                {
                    current.Append(text[++i]);
                }
                else
                {
                    current.Append(c);
                }
                continue;
            }
            if (c is '"' or '\'')
            {
                quote = c;
                tokenStarted = true;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (tokenStarted)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    tokenStarted = false;
                }
            }
            else
            {
                tokenStarted = true;
                current.Append(c);
            }
        }
        if (tokenStarted)
        {
            result.Add(current.ToString());
        }
        return result;
    }


    private List<CompatLayer> DetectInternal()
    {
        List<CompatLayer> layers = new();
        try
        {
            List<string> steamRoots = _steamLibraryService.GetSteamRoots().ToList();
            List<string> libraryFolders = _steamLibraryService.GetLibraryFolders().ToList();

            // Steam 内置 Proton
            foreach (string common in _steamLibraryService.GetCommonGameFolders())
            {
                foreach (string dir in SafeEnumerateDirectories(common))
                {
                    string name = Path.GetFileName(dir);
                    if (!name.StartsWith("Proton", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    string proton = Path.Combine(dir, "proton");
                    if (IsExecutableFile(proton))
                    {
                        string? steamRoot = steamRoots.FirstOrDefault(x => common.StartsWith(Path.Combine(x, "steamapps"), StringComparison.Ordinal));
                        AddLayer(layers, new CompatLayer
                        {
                            Id = CompatLayer.CreateId(proton),
                            Name = name,
                            Kind = CompatLayerKind.Proton,
                            ExecutablePath = proton,
                            RootPath = dir,
                            SteamRoot = steamRoot,
                            Version = ReadVersionFile(dir),
                            Source = "Steam",
                        });
                    }
                }
            }

            // compatibilitytools.d（GE-Proton / Proton-GE / 自定义 Proton）
            foreach (string root in steamRoots.SelectMany(x => new[]
                     {
                         Path.Combine(x, "compatibilitytools.d"),
                         Path.Combine(x, "steamapps", "common"),
                     }).Distinct())
            {
                DetectProtonFolders(root, "compatibilitytools.d", layers, steamRoots.FirstOrDefault());
            }
            foreach (string root in libraryFolders.Select(x => Path.Combine(x, "compatibilitytools.d")))
            {
                DetectProtonFolders(root, "compatibilitytools.d", layers, steamRoots.FirstOrDefault());
            }
            foreach (string root in new[]
                     {
                         "/usr/share/steam/compatibilitytools.d",
                         "/usr/local/share/steam/compatibilitytools.d",
                         "/usr/share/steam/steamapps/common",
                     })
            {
                DetectProtonFolders(root, "system", layers, steamRoots.FirstOrDefault());
            }

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] customRoots = (AppConfig.CompatScanPaths ?? "")
                                   .Split(['\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string root in customRoots)
            {
                DetectProtonFolders(root, "custom", layers, steamRoots.FirstOrDefault());
                DetectWineRoot(root, "custom", layers);
            }

            // Lutris / Heroic / Bottles 的 Wine 构建
            DetectWineRunners(Path.Combine(home, ".local", "share", "lutris", "runners", "wine"), "Lutris", layers);
            DetectWineRunners(Path.Combine(home, ".var", "app", "net.lutris.Lutris", "data", "lutris", "runners", "wine"), "Lutris Flatpak", layers);
            DetectWineRunners(Path.Combine(home, ".config", "heroic", "tools", "wine"), "Heroic", layers);
            DetectWineRunners(Path.Combine(home, ".local", "share", "heroic", "tools", "wine"), "Heroic", layers);
            DetectWineRunners(Path.Combine(home, ".var", "app", "com.heroicgameslauncher.hgl", "config", "heroic", "tools", "wine"), "Heroic Flatpak", layers);
            DetectWineRunners(Path.Combine(home, ".var", "app", "com.heroicgameslauncher.hgl", ".config", "heroic", "tools", "wine"), "Heroic Flatpak", layers);
            DetectWineRunners(Path.Combine(home, ".local", "share", "bottles", "runners"), "Bottles", layers);
            DetectWineRunners(Path.Combine(home, ".var", "app", "com.usebottles.bottles", "data", "bottles", "runners"), "Bottles Flatpak", layers);
            DetectWineRunners(Path.Combine(home, ".local", "share", "wine", "runners"), "Wine", layers);
            DetectWineRunners(Path.Combine(home, ".local", "share", "umu", "compatibilitytools"), "umu", layers);

            // 系统 Wine
            foreach (string wine in FindSystemWine())
            {
                AddLayer(layers, new CompatLayer
                {
                    Id = CompatLayer.CreateId(wine),
                    Name = "System Wine",
                    Kind = CompatLayerKind.Wine,
                    ExecutablePath = wine,
                    RootPath = Path.GetDirectoryName(Path.GetDirectoryName(wine)),
                    Version = ReadWineVersion(wine),
                    Source = "System",
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Detect compatibility layers");
        }
        return layers;
    }


    private void DetectProtonFolders(string root, string source, List<CompatLayer> layers, string? steamRoot)
    {
        if (!Directory.Exists(root))
        {
            return;
        }
        foreach (string dir in SafeEnumerateDirectories(root))
        {
            string proton = Path.Combine(dir, "proton");
            if (!IsExecutableFile(proton))
            {
                continue;
            }
            string name = Path.GetFileName(dir);
            AddLayer(layers, new CompatLayer
            {
                Id = CompatLayer.CreateId(proton),
                Name = name,
                Kind = CompatLayerKind.Proton,
                ExecutablePath = proton,
                RootPath = dir,
                SteamRoot = steamRoot,
                Version = ReadVersionFile(dir),
                Source = source,
            });
        }
    }


    private void DetectWineRunners(string root, string source, List<CompatLayer> layers)
    {
        if (!Directory.Exists(root))
        {
            return;
        }
        foreach (string dir in SafeEnumerateDirectories(root))
        {
            DetectWineRoot(dir, source, layers);
        }
    }


    private void DetectWineRoot(string root, string source, List<CompatLayer> layers)
    {
        string[] candidates =
        [
            Path.Combine(root, "bin", "wine"),
            Path.Combine(root, "bin", "wine64"),
            Path.Combine(root, "wine"),
        ];
        string? wine = candidates.FirstOrDefault(IsExecutableFile);
        if (wine is null)
        {
            return;
        }
        string? name = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(wine)));
        if (string.IsNullOrWhiteSpace(name) || name is "bin")
        {
            name = Path.GetFileName(root);
        }
        AddLayer(layers, new CompatLayer
        {
            Id = CompatLayer.CreateId(wine),
            Name = name,
            Kind = CompatLayerKind.Wine,
            ExecutablePath = wine,
            RootPath = root,
            Version = ReadWineVersion(wine),
            Source = source,
        });
    }


    private static void AddLayer(List<CompatLayer> layers, CompatLayer layer)
    {
        if (!layers.Any(x => x.Id == layer.Id))
        {
            layers.Add(layer);
        }
    }


    private static IEnumerable<string> FindSystemWine()
    {
        List<string> result = new();
        string? path = Environment.GetEnvironmentVariable("PATH");
        foreach (string dir in (path ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string name in new[] { "wine", "wine64" })
            {
                string file = Path.Combine(dir, name);
                if (IsExecutableFile(file))
                {
                    result.Add(file);
                }
            }
        }
        foreach (string file in new[]
                 {
                     "/usr/bin/wine",
                     "/usr/local/bin/wine",
                     "/opt/wine-stable/bin/wine",
                     "/opt/wine-staging/bin/wine",
                     "/opt/wine-devel/bin/wine",
                 })
        {
            if (IsExecutableFile(file))
            {
                result.Add(file);
            }
        }
        return result.Distinct();
    }


    private static string? ReadVersionFile(string dir)
    {
        foreach (string name in new[] { "version", "proton_version" })
        {
            string file = Path.Combine(dir, name);
            try
            {
                if (File.Exists(file))
                {
                    string value = File.ReadAllText(file).Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Split('\n')[0].Trim();
                    }
                }
            }
            catch
            {
                // ignored
            }
        }
        return null;
    }


    private static string? ReadWineVersion(string wine)
    {
        try
        {
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = wine,
                    ArgumentList = { "--version" },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Environment = { ["WINEDEBUG"] = "-all" },
                },
            };
            process.Start();
            if (!process.WaitForExit(2500))
            {
                try { process.Kill(true); } catch { }
                return null;
            }
            string text = process.StandardOutput.ReadToEnd().Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }


    private static IEnumerable<string> SafeEnumerateDirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path);
        }
        catch
        {
            return [];
        }
    }


    private static bool IsExecutableFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }
            if (OperatingSystem.IsWindows())
            {
                return true;
            }
            UnixFileMode mode = File.GetUnixFileMode(path);
            return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch
        {
            return false;
        }
    }

}
