using Starward.Core;
using Starward.Core.Gacha;
using Xunit;

namespace Starward.Avalonia.Tests;

public class LinuxPathTests
{

    [Fact]
    public void GachaCachePathUsesNativeSeparators()
    {
        string root = Path.Combine(Path.GetTempPath(), "StarwardPathTest");

        string webCaches = GachaLogClient.GetWebCachesFolderPath(GameBiz.hk4e_global, root);
        string cache = GachaLogClient.GetGachaCacheFilePath(GameBiz.hk4e_global, root);

        Assert.Equal(Path.Combine(root, "GenshinImpact_Data", "webCaches"), webCaches);
        Assert.Equal(Path.Combine(root, "GenshinImpact_Data", "webCaches", "Cache", "Cache_Data", "data_2"), cache);
        Assert.DoesNotContain('\\', webCaches);
        Assert.DoesNotContain('\\', cache);
    }


    [Fact]
    public void GachaUrlIsFoundInCachedDataFile()
    {
        string root = Path.Combine(Path.GetTempPath(), $"StarwardGacha_{Guid.NewGuid():N}");
        string cache = GachaLogClient.GetGachaCacheFilePath(GameBiz.hk4e_global, root);
        Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
        const string url = "https://gs.hoyoverse.com/genshin/event/e20190909gacha-v3/index.html?authkey=test";
        File.WriteAllText(cache, url + '\0');

        try
        {
            Assert.Equal(url, GachaLogClient.GetGachaUrlFromWebCache(GameBiz.hk4e_global, root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

}
