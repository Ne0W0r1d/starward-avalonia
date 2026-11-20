using System;
using System.Collections.Generic;
using System.IO;

namespace Starward.Features.Compatibility;

public enum CompatLayerKind
{
    Wine,
    Proton,
}


public sealed class CompatLayer
{

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required CompatLayerKind Kind { get; init; }

    public required string ExecutablePath { get; init; }

    public string? RootPath { get; init; }

    public string? SteamRoot { get; init; }

    public string? Version { get; init; }

    public required string Source { get; init; }

    public bool IsProton => Kind is CompatLayerKind.Proton;

    public string KindText => Kind is CompatLayerKind.Proton ? "Proton" : "Wine";

    public string DisplayName => string.IsNullOrWhiteSpace(Version) ? Name : $"{Name}  ({Version})";

    public string SourceText => Source;

    /// <summary>
    /// 去重时被合并到当前条目的其它可执行文件 ID，用于兼容旧配置。
    /// </summary>
    public HashSet<string> AliasIds { get; } = new(StringComparer.Ordinal);

    public bool MatchesId(string? id)
    {
        return !string.IsNullOrWhiteSpace(id) && (id == Id || AliasIds.Contains(id));
    }


    public override string ToString() => DisplayName;


    public static string CreateId(string executablePath)
    {
        try
        {
            string fullPath = Path.GetFullPath(executablePath);
            FileSystemInfo? target = File.ResolveLinkTarget(fullPath, returnFinalTarget: true);
            return target?.FullName ?? fullPath;
        }
        catch
        {
            return executablePath;
        }
    }

}
