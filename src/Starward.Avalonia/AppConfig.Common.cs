using Starward.Features.Database;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Starward;

public static partial class AppConfig
{

    public static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
    };


    public static string StarwardExecutePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Starward");


    /// <summary>
    /// 创建启动当前 Starward 实例的命令。通过 dotnet 宿主运行时需要把入口 dll 一并带上。
    /// </summary>
    public static ProcessStartInfo CreateSelfStartInfo(params string[] arguments)
    {
        ProcessStartInfo info = new()
        {
            FileName = StarwardExecutePath,
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        string processName = Path.GetFileNameWithoutExtension(StarwardExecutePath);
        if (processName.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            string? assembly = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrWhiteSpace(assembly) || !File.Exists(assembly))
            {
                assembly = Environment.GetCommandLineArgs().FirstOrDefault();
            }
            if (!string.IsNullOrWhiteSpace(assembly) && File.Exists(assembly))
            {
                info.ArgumentList.Add(assembly);
            }
        }
        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }
        return info;
    }

    public static string AppVersion { get; private set; } = "0.0.0";

    public static string CacheFolder { get; private set; } = "";

    /// <summary>
    /// Starward 保存配置、数据库、日志的目录。
    /// </summary>
    public static string ConfigPath { get; private set; } = "";

    public static string? UserDataFolder { get; private set; }

    public static string LogFile { get; private set; } = "";

    public static Guid DeviceId { get; private set; }

    public static Guid SessionId { get; private set; } = Guid.CreateVersion7();

    public static bool IsPortable { get; private set; }

    public static bool IsLinux => OperatingSystem.IsLinux();

    public static bool IsWindows => OperatingSystem.IsWindows();

    public static bool IsMacOS => OperatingSystem.IsMacOS();


    private static bool _initialized;

    private static readonly object _initializeLock = new();


    public static void Initialize()
    {
        lock (_initializeLock)
        {
            if (_initialized)
            {
                return;
            }
            AppVersion = typeof(AppConfig).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
            IsPortable = File.Exists(Path.Combine(AppContext.BaseDirectory, "portable")) || Environment.GetEnvironmentVariable("STARWARD_PORTABLE") is "1";
            CacheFolder = IsPortable ? AppContext.BaseDirectory : GetDefaultDataFolder();
            ConfigPath = Path.Combine(CacheFolder, "config.json");
            Directory.CreateDirectory(CacheFolder);

            DeviceId = CreateDeviceId();
            LoadConfiguration();

            UserDataFolder ??= Path.Combine(CacheFolder, "data");
            Directory.CreateDirectory(UserDataFolder);

            string logFolder = Path.Combine(CacheFolder, "log");
            Directory.CreateDirectory(logFolder);
            LogFile = Path.Combine(logFolder, $"Starward_{DateTime.Now:yyMMdd}.log");

            DatabaseService.SetDatabase(UserDataFolder);
            _initialized = true;
        }
    }


    private static string GetDefaultDataFolder()
    {
        string? custom = Environment.GetEnvironmentVariable("STARWARD_DATA_FOLDER");
        if (!string.IsNullOrWhiteSpace(custom))
        {
            return Path.GetFullPath(custom);
        }
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Starward");
        }
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Starward");
        }
        string xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME")
                     ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        return Path.Combine(xdg, "Starward");
    }


    private static Guid CreateDeviceId()
    {
        try
        {
            string[] machineIds =
            [
                "/etc/machine-id",
                "/var/lib/dbus/machine-id",
            ];
            StringBuilder sb = new();
            foreach (string file in machineIds)
            {
                if (File.Exists(file))
                {
                    sb.Append(File.ReadAllText(file).Trim());
                    break;
                }
            }
            if (sb.Length == 0)
            {
                sb.Append(Environment.MachineName);
            }
            sb.Append(Environment.OSVersion);
            return new Guid(MD5.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
        }
        catch
        {
            return Guid.CreateVersion7();
        }
    }


    public static void WriteCrashLog(string source, Exception exception)
    {
        try
        {
            string file = LogFile;
            if (string.IsNullOrWhiteSpace(file))
            {
                string folder = Path.Combine(GetDefaultDataFolder(), "log");
                Directory.CreateDirectory(folder);
                file = Path.Combine(folder, $"Starward_{DateTime.Now:yyMMdd}.log");
            }
            StringBuilder sb = new();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source} Crash:");
            sb.AppendLine(exception.ToString());
            File.AppendAllText(file, sb.ToString());
        }
        catch
        {
            // ignored
        }
    }

}
