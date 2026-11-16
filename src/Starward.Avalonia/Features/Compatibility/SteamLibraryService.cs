using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Starward.Features.Compatibility;

public sealed partial class SteamLibraryService
{

    public IReadOnlyList<string> GetSteamRoots()
    {
        List<string> roots = new();
        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }
            try
            {
                string full = Path.GetFullPath(path);
                if (Directory.Exists(full) && !roots.Contains(full, StringComparer.Ordinal))
                {
                    roots.Add(full);
                }
            }
            catch
            {
                // ignored
            }
        }

        Add(Environment.GetEnvironmentVariable("STEAM_ROOT"));
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Add(Path.Combine(home, ".steam", "steam"));
        Add(Path.Combine(home, ".steam", "root"));
        Add(Path.Combine(home, ".local", "share", "Steam"));
        Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"));
        Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam"));
        Add(Path.Combine(home, "snap", "steam", "common", ".local", "share", "Steam"));
        Add(Path.Combine(home, ".steam", "debian-installation"));
        return roots;
    }


    public IReadOnlyList<string> GetLibraryFolders()
    {
        List<string> folders = new();
        foreach (string root in GetSteamRoots())
        {
            if (!folders.Contains(root, StringComparer.Ordinal))
            {
                folders.Add(root);
            }
            string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf))
            {
                continue;
            }
            try
            {
                string text = File.ReadAllText(vdf);
                foreach (Match match in LibraryPathRegex().Matches(text))
                {
                    string path = match.Groups[1].Value.Replace(@"\\", @"\");
                    if (Directory.Exists(path) && !folders.Contains(path, StringComparer.Ordinal))
                    {
                        folders.Add(path);
                    }
                }
            }
            catch
            {
                // ignored
            }
        }
        return folders;
    }


    public IReadOnlyList<string> GetCommonGameFolders()
    {
        return GetLibraryFolders()
              .Select(x => Path.Combine(x, "steamapps", "common"))
              .Where(Directory.Exists)
              .ToList();
    }


    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPathRegex();

}
