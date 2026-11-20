using Starward.Core;
using Starward.Core.Gacha;
using Xunit;

namespace Starward.Avalonia.Tests;

public class GachaCacheTests
{

    [Fact]
    public void FindsGachaUrlInVersionedWebCacheFolder()
    {
        string root = Path.Combine(Path.GetTempPath(), $"StarwardGacha_{Guid.NewGuid():N}");
        string cache = Path.Combine(root, "StarRail_Data", "webCaches", "3.4.0.0", "Cache", "Cache_Data", "data_2");
        Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
        const string url = "https://webstatic-sea.hoyoverse.com/hkrpg/event/e20211215gacha-v2/index.html?authkey=abc&game_biz=hkrpg_global&auth_appid=webview_gacha";
        File.WriteAllText(cache, "cache header" + url + "\0binary trailer");

        try
        {
            Assert.Equal(url, GachaLogClient.GetGachaUrlFromWebCache(GameBiz.hkrpg_global, root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }


    [Fact]
    public void ChoosesTheNewestCacheDataBlock()
    {
        string root = Path.Combine(Path.GetTempPath(), $"StarwardGacha_{Guid.NewGuid():N}");
        string folder = Path.Combine(root, "ZenlessZoneZero_Data", "webCaches", "Cache", "Cache_Data");
        Directory.CreateDirectory(folder);
        string oldFile = Path.Combine(folder, "data_2");
        string newFile = Path.Combine(folder, "data_3");
        File.WriteAllText(oldFile, "https://webstatic.mihoyo.com/nap/event/e20230424gacha?authkey=old&game_biz=nap_cn");
        File.WriteAllText(newFile, "https://webstatic.mihoyo.com/nap/event/e20230424gacha?authkey=new&game_biz=nap_cn");
        File.SetLastWriteTime(oldFile, DateTime.Now.AddMinutes(-10));
        File.SetLastWriteTime(newFile, DateTime.Now);

        try
        {
            Assert.Contains("authkey=new", GachaLogClient.GetGachaUrlFromWebCache(GameBiz.nap_cn, root));
            Assert.Equal(newFile, GachaLogClient.GetGachaCacheFilePath(GameBiz.nap_cn, root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }


    [Fact]
    public void NormalizesUnescapedBase64Authkey()
    {
        string url = GachaLogClient.NormalizeGachaUrl(
            "https://gs.hoyoverse.com/genshin/event/e20190909gacha?authkey=a+b/c==&game_biz=hk4e_global#/log");

        Assert.Contains("%2B", url);
        Assert.Contains("%2F", url);
        Assert.Contains("%3D%3D", url);
        Assert.DoesNotContain("#/log", url);
    }


    [Fact]
    public void FindsGachaUrlInLevelDbAndServiceWorkerFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), $"StarwardGacha_{Guid.NewGuid():N}");
        string levelDb = Path.Combine(root, "YuanShen_Data", "webCaches", "5.1.0.0", "Local Storage", "leveldb");
        string worker = Path.Combine(root, "YuanShen_Data", "webCaches", "5.1.0.0", "Service Worker", "CacheStorage", "abc");
        Directory.CreateDirectory(levelDb);
        Directory.CreateDirectory(worker);
        const string url = "https://webstatic.mihoyo.com/hk4e/event/e20190909gacha-v3/index.html?authkey=abc&game_biz=hk4e_cn&auth_appid=webview_gacha";
        File.WriteAllText(Path.Combine(levelDb, "000003.log"), "KEY business_url VALUE " + url + '\0');
        File.WriteAllText(Path.Combine(worker, "9ad5c7c793f89be7_0"), "header" + url + '\0');

        try
        {
            Assert.Equal(url, GachaLogClient.GetGachaUrlFromWebCache(GameBiz.hk4e_cn, root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

}
