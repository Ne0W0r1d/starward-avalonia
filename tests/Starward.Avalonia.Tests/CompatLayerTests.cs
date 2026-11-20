using Starward.Features.Compatibility;
using System.Diagnostics;
using Xunit;

namespace Starward.Avalonia.Tests;

public class CompatLayerTests
{

    [Fact]
    public void ArgumentsPreserveWindowsPathsAndQuotedSpaces()
    {
        List<string> args = CompatLayerService.SplitArguments(
            @"-windowed Z:\Games\config.ini ""C:\Program Files\Starward\profile.ini"" 'single quoted' """" -flag");

        Assert.Equal(
        [
            "-windowed",
            @"Z:\Games\config.ini",
            @"C:\Program Files\Starward\profile.ini",
            "single quoted",
            "",
            "-flag",
        ], args);
    }


    [Fact]
    public void ProtonStartInfoSetsCompatDataAndClientPath()
    {
        CompatLayer layer = new()
        {
            Id = "/games/GE-Proton/proton",
            Name = "GE-Proton",
            Kind = CompatLayerKind.Proton,
            ExecutablePath = "/games/GE-Proton/proton",
            SteamRoot = "/home/user/.local/share/Steam",
            Source = "test",
        };

        ProcessStartInfo info = CompatLayerService.CreateStartInfo(
            layer,
            "/games/Genshin/GenshinImpact.exe",
            "-dx11",
            "/games/Genshin",
            "/prefixes/hk4e_global",
            "",
            true);

        Assert.Equal("/games/GE-Proton/proton", info.FileName);
        Assert.Equal(new[] { "run", "/games/Genshin/GenshinImpact.exe", "-dx11" }, info.ArgumentList);
        Assert.Equal("/prefixes/hk4e_global", info.Environment["STEAM_COMPAT_DATA_PATH"]);
        Assert.Equal("/home/user/.local/share/Steam", info.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"]);
        Assert.Equal("winemenubuilder.exe=d", info.Environment["WINEDLLOVERRIDES"]);
    }


    [Fact]
    public void WineStartInfoSetsWinePrefix()
    {
        CompatLayer layer = new()
        {
            Id = "/usr/bin/wine",
            Name = "System Wine",
            Kind = CompatLayerKind.Wine,
            ExecutablePath = "/usr/bin/wine",
            Source = "test",
        };

        ProcessStartInfo info = CompatLayerService.CreateStartInfo(
            layer,
            "/games/StarRail/StarRail.exe",
            "",
            "/games/StarRail",
            "/prefixes/hkrpg_global",
            "-force-d3d11",
            false);

        Assert.Equal("/usr/bin/wine", info.FileName);
        Assert.Equal(new[] { "/games/StarRail/StarRail.exe", "-force-d3d11" }, info.ArgumentList);
        Assert.Equal("/prefixes/hkrpg_global", info.Environment["WINEPREFIX"]);
        Assert.False(info.Environment.ContainsKey("STEAM_COMPAT_DATA_PATH"));
    }


    [Fact]
    public void LinuxProcessLookupMatchesWindowsStyleExecutablePath()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        Assert.True(File.Exists("/bin/sleep"));
        string folder = Path.Combine(Path.GetTempPath(), $"starward-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        string executable = Path.Combine(folder, "GenshinImpact.exe");
        File.CreateSymbolicLink(executable, "/bin/sleep");
        using Process process = Process.Start(new ProcessStartInfo(executable, "30"))!;

        try
        {
            Thread.Sleep(150);
            Assert.NotNull(Starward.Features.GameLauncher.GameLauncherService.FindGameProcess("GenshinImpact.exe"));
        }
        finally
        {
            process.Kill(true);
            Directory.Delete(folder, true);
        }
    }


    [Fact]
    public void LayerIdResolvesSymlinksToAvoidDuplicateWineEntries()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"starward-layer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        string target = Path.Combine(folder, "wine64");
        File.WriteAllText(target, "");
        string link = Path.Combine(folder, "wine");
        File.CreateSymbolicLink(link, target);

        try
        {
            Assert.Equal(CompatLayer.CreateId(target), CompatLayer.CreateId(link));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }


    [Fact]
    public void DuplicateBuildsAreMergedAndOldIdRemainsSelectable()
    {
        CompatLayer custom = new()
        {
            Id = "/custom/GE-Proton9-20/proton",
            Name = "GE-Proton9-20",
            Kind = CompatLayerKind.Proton,
            ExecutablePath = "/custom/GE-Proton9-20/proton",
            Version = "GE-Proton9-20",
            Source = "custom",
        };
        CompatLayer steam = new()
        {
            Id = "/steam/compatibilitytools.d/GE-Proton9-20/proton",
            Name = "GE-Proton9-20",
            Kind = CompatLayerKind.Proton,
            ExecutablePath = "/steam/compatibilitytools.d/GE-Proton9-20/proton",
            SteamRoot = "/steam",
            Version = "GE-Proton9-20",
            Source = "compatibilitytools.d",
        };

        IReadOnlyList<CompatLayer> layers = CompatLayerService.DeduplicateLayers([custom, steam]);

        CompatLayer layer = Assert.Single(layers);
        Assert.Equal(steam.Id, layer.Id);
        Assert.True(layer.MatchesId(custom.Id));
    }


    [Fact]
    public void MangoHudAndSdlInputSetProtonEnvironment()
    {
        CompatLayer layer = new()
        {
            Id = "/games/GE-Proton/proton",
            Name = "GE-Proton",
            Kind = CompatLayerKind.Proton,
            ExecutablePath = "/games/GE-Proton/proton",
            Source = "test",
        };

        ProcessStartInfo info = CompatLayerService.CreateStartInfo(
            layer,
            "/games/Genshin/GenshinImpact.exe",
            "",
            "/games/Genshin",
            "/prefixes/hk4e_global",
            "",
            false,
            null,
            enableMangoHud: true,
            enableSdlInput: true);

        Assert.Equal("1", info.Environment["MANGOHUD"]);
        Assert.Equal("1", info.Environment["PROTON_PREFER_SDL"]);
        Assert.Equal("1", info.Environment["PROTON_USE_SDL"]);
        Assert.False(info.Environment.ContainsKey("SDL_JOYSTICK_HIDAPI"));
    }


    [Fact]
    public void SdlInputUsesHidapiForWine()
    {
        CompatLayer layer = new()
        {
            Id = "/usr/bin/wine",
            Name = "System Wine",
            Kind = CompatLayerKind.Wine,
            ExecutablePath = "/usr/bin/wine",
            Source = "test",
        };

        ProcessStartInfo info = CompatLayerService.CreateStartInfo(
            layer,
            "/games/StarRail/StarRail.exe",
            "",
            "/games/StarRail",
            "/prefixes/hkrpg_global",
            "",
            false,
            enableSdlInput: true);

        Assert.Equal("1", info.Environment["SDL_JOYSTICK_HIDAPI"]);
        Assert.False(info.Environment.ContainsKey("PROTON_PREFER_SDL"));
    }

}
