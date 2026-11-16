using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Starward;

public static partial class AppConfig
{

    private sealed class AppConfiguration
    {
        public string? Language { get; set; }

        public string? UserDataFolder { get; set; }

        public bool? EnableLoginAuthTicket { get; set; }

        public string? stoken { get; set; }

        public string? mid { get; set; }
    }


    public static string? Language { get; private set; }

    public static bool? EnableLoginAuthTicket { get; set; }

    public static string? stoken { get; set; }

    public static string? mid { get; set; }


    public static void SetLanguage(string? lang)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(lang))
            {
                var info = new CultureInfo(lang);
                CultureInfo.CurrentUICulture = info;
                CultureInfo.DefaultThreadCurrentUICulture = info;
                Language = lang;
            }
            else
            {
                CultureInfo.CurrentUICulture = CultureInfo.InstalledUICulture;
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InstalledUICulture;
                Language = null;
            }
        }
        catch
        {
            // ignored
        }
    }


    private static void LoadConfiguration()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return;
            }
            string text = File.ReadAllText(ConfigPath);
            AppConfiguration? config = JsonSerializer.Deserialize<AppConfiguration>(text, JsonSerializerOptions);
            if (config is null)
            {
                return;
            }
            SetLanguage(config.Language);
            if (!string.IsNullOrWhiteSpace(config.UserDataFolder))
            {
                string folder = config.UserDataFolder;
                if (!Path.IsPathFullyQualified(folder))
                {
                    folder = Path.GetFullPath(folder, CacheFolder);
                }
                UserDataFolder = folder;
            }
            EnableLoginAuthTicket = config.EnableLoginAuthTicket;
            stoken = config.stoken;
            mid = config.mid;
        }
        catch (Exception ex)
        {
            WriteCrashLog("LoadConfiguration", ex);
        }
    }


    public static void SaveConfiguration()
    {
        try
        {
            string? userDataFolder = UserDataFolder;
            if (!string.IsNullOrWhiteSpace(userDataFolder) && userDataFolder.StartsWith(CacheFolder, StringComparison.Ordinal))
            {
                userDataFolder = Path.GetRelativePath(CacheFolder, userDataFolder);
            }
            AppConfiguration config = new()
            {
                Language = Language,
                UserDataFolder = userDataFolder,
                EnableLoginAuthTicket = EnableLoginAuthTicket,
                stoken = stoken,
                mid = mid,
            };
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonSerializerOptions));
        }
        catch (Exception ex)
        {
            WriteCrashLog("SaveConfiguration", ex);
        }
    }


    public static void ChangeUserDataFolder(string folder)
    {
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
        UserDataFolder = Path.GetFullPath(folder);
        Features.Database.DatabaseService.SetDatabase(UserDataFolder);
        ClearSettingCache();
        SaveConfiguration();
    }

}
