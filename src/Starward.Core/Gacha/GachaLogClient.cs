using Microsoft.Win32;
using Starward.Core.Gacha.Genshin;
using Starward.Core.Gacha.StarRail;
using Starward.Core.Gacha.ZZZ;
using System.Net.Http.Json;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;

namespace Starward.Core.Gacha;

public abstract class GachaLogClient
{


    protected const string WEB_CACHE_PATH_YS_CN = @"YuanShen_Data\webCaches\Cache\Cache_Data\data_2";
    protected const string WEB_CACHE_PATH_YS_OS = @"GenshinImpact_Data\webCaches\Cache\Cache_Data\data_2";

    protected const string API_PREFIX_YS_CN = "https://public-operation-hk4e.mihoyo.com/gacha_info/api/getGachaLog";
    protected const string API_PREFIX_YS_OS = "https://public-operation-hk4e-sg.hoyoverse.com/gacha_info/api/getGachaLog";

    protected static ReadOnlySpan<byte> SPAN_WEB_PREFIX_YS_CN => "https://webstatic.mihoyo.com/hk4e/event/e20190909gacha"u8;
    protected static ReadOnlySpan<byte> SPAN_WEB_PREFIX_YS_OS => "https://gs.hoyoverse.com/genshin/event/e20190909gacha"u8;



    protected const string WEB_CACHE_SR_PATH = @"StarRail_Data\webCaches\Cache\Cache_Data\data_2";

    protected const string API_PREFIX_SR_CN = "https://public-operation-hkrpg.mihoyo.com/common/hkrpg_gacha_record/api/getGachaLog";
    protected const string API_PREFIX_SR_OS = "https://public-operation-hkrpg-sg.hoyoverse.com/common/hkrpg_gacha_record/api/getGachaLog";

    protected static ReadOnlySpan<byte> SPAN_WEB_PREFIX_SR_CN => "https://webstatic.mihoyo.com/hkrpg/event/e20211215gacha"u8;
    protected static ReadOnlySpan<byte> SPAN_WEB_PREFIX_SR_OS => "https://gs.hoyoverse.com/hkrpg/event/e20211215gacha"u8;


    protected const string WEB_CACHE_ZZZ_PATH = @"ZenlessZoneZero_Data\webCaches\Cache\Cache_Data\data_2";

    protected const string API_PREFIX_ZZZ_CN = "https://public-operation-nap.mihoyo.com/common/gacha_record/api/getGachaLog";
    protected const string API_PREFIX_ZZZ_OS = "https://public-operation-nap-sg.hoyoverse.com/common/gacha_record/api/getGachaLog";

    protected static ReadOnlySpan<byte> SPAN_WEB_PREFIX_ZZZ_CN => "https://webstatic.mihoyo.com/nap/event/e20230424gacha"u8;
    protected static ReadOnlySpan<byte> SPAN_WEB_PREFIX_ZZZ_OS => "https://gs.hoyoverse.com/nap/event/e20230424gacha"u8;





    protected const string REG_KEY_BH3_CN = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\崩坏3";
    protected const string REG_KEY_BH3_OS = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Honkai Impact 3";
    protected const string REG_KEY_BH3_GL = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Honkai Impact 3rd";
    protected const string REG_KEY_BH3_TW = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\崩壊3rd";
    protected const string REG_KEY_BH3_KR = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\붕괴3rd";
    protected const string REG_KEY_BH3_JP = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\崩壊3rd";




    protected readonly HttpClient _httpClient;


    public abstract IReadOnlyCollection<IGachaType> QueryGachaTypes { get; init; }



    public GachaLogClient(HttpClient? httpClient = null)
    {
        if (httpClient is null)
        {
            _httpClient = new HttpClient(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All })
            {
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
            };
        }
        else
        {
            _httpClient = httpClient;
        }
    }



    #region public method



    public async Task<long> GetUidByGachaUrlAsync(string gachaUrl)
    {
        var prefix = GetGachaUrlPrefix(gachaUrl);
        foreach (var gachaType in QueryGachaTypes)
        {
            var param = new GachaLogQuery(gachaType, 1, 1, 0);
            var list = await GetGachaLogByQueryAsync<GachaLogItem>(prefix, param);
            if (list.Count != 0)
            {
                return list.First().Uid;
            }
        }
        return 0;
    }



    public abstract Task<IEnumerable<GachaLogItem>> GetGachaLogAsync(string gachaUrl, long endId = 0, string? lang = null, IProgress<(IGachaType GachaType, int Page)>? progress = null, CancellationToken cancellationToken = default);


    public abstract Task<IEnumerable<GachaLogItem>> GetGachaLogAsync(string gachaUrl, IGachaType gachaType, long endId = 0, string? lang = null, IProgress<(IGachaType GachaType, int Page)>? progress = null, CancellationToken cancellationToken = default);


    public abstract Task<IEnumerable<GachaLogItem>> GetGachaLogAsync(string gachaUrl, GachaLogQuery query, CancellationToken cancellationToken = default);




    public static string? GetGachaUrlFromWebCache(GameBiz gameBiz, string? installPath = null)
    {
        foreach (string file in GetGachaCacheFiles(gameBiz, installPath))
        {
            string? url = FindGachaUrlInCacheFile(file);
            if (!string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
        }
        return null;
    }


    /// <summary>
    /// Returns the web cache data files of the game, newest first.
    /// </summary>
    public static IReadOnlyList<string> GetGachaCacheFiles(GameBiz gameBiz, string? installPath = null)
    {
        Dictionary<string, DateTime> files = new(StringComparer.Ordinal);
        void AddFile(string? file)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                return;
            }
            try
            {
                if (!File.Exists(file))
                {
                    return;
                }
                DateTime time = File.GetLastWriteTime(file);
                if (!files.TryGetValue(file, out DateTime old) || time > old)
                {
                    files[file] = time;
                }
            }
            catch
            {
                // A running game may remove or replace a cache file at any time.
            }
        }

        string webCaches = GetWebCachesFolderPath(gameBiz, installPath);
        AddFile(CombineWindowsPath(webCaches, @"Cache\Cache_Data\data_2"));
        AddFile(CombineWindowsPath(webCaches, @"Cache_Data\data_2"));
        if (Directory.Exists(webCaches))
        {
            EnumerateCacheFiles(webCaches, 0, 6, AddFile);
        }
        return files.OrderByDescending(x => x.Value)
                    .ThenBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => x.Key)
                    .ToList();
    }



    public static string GetWebCachesFolderPath(GameBiz gameBiz, string? installPath)
    {
        string prefix = gameBiz.Value switch
        {
            GameBiz.hk4e_cn or GameBiz.hk4e_bilibili => CombineWindowsPath(installPath, @"YuanShen_Data\webCaches"),
            GameBiz.hk4e_global => CombineWindowsPath(installPath, @"GenshinImpact_Data\webCaches"),
            GameBiz.hkrpg_cn or GameBiz.hkrpg_global or GameBiz.hkrpg_bilibili => CombineWindowsPath(installPath, @"StarRail_Data\webCaches"),
            GameBiz.nap_cn or GameBiz.nap_global or GameBiz.nap_bilibili => CombineWindowsPath(installPath, @"ZenlessZoneZero_Data\webCaches"),
            _ => throw new ArgumentOutOfRangeException($"Unknown region {gameBiz}"),
        };
        return prefix;
    }



    public static string GetGachaCacheFilePath(GameBiz gameBiz, string? installPath)
    {
        return GetGachaCacheFiles(gameBiz, installPath).FirstOrDefault()
               ?? gameBiz.Value switch
               {
                   GameBiz.hk4e_cn or GameBiz.hk4e_bilibili => CombineWindowsPath(installPath, WEB_CACHE_PATH_YS_CN),
                   GameBiz.hk4e_global => CombineWindowsPath(installPath, WEB_CACHE_PATH_YS_OS),
                   GameBiz.hkrpg_cn or GameBiz.hkrpg_global or GameBiz.hkrpg_bilibili => CombineWindowsPath(installPath, WEB_CACHE_SR_PATH),
                   GameBiz.nap_cn or GameBiz.nap_global or GameBiz.nap_bilibili => CombineWindowsPath(installPath, WEB_CACHE_ZZZ_PATH),
                   _ => throw new ArgumentOutOfRangeException($"Unknown region {gameBiz}"),
               };
    }


    private static void EnumerateCacheFiles(string directory, int depth, int maxDepth, Action<string> addFile)
    {
        if (depth > maxDepth)
        {
            return;
        }
        try
        {
            foreach (string file in Directory.EnumerateFiles(directory))
            {
                string name = Path.GetFileName(file);
                if (name.StartsWith("data_", StringComparison.Ordinal)
                    || name.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".ldb", StringComparison.OrdinalIgnoreCase)
                    || Regex.IsMatch(name, @"_\d$"))
                {
                    addFile(file);
                }
            }
        }
        catch
        {
            return;
        }
        try
        {
            foreach (string child in Directory.EnumerateDirectories(directory))
            {
                string name = Path.GetFileName(child);
                if (name.Equals("GPUCache", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("ShaderCache", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Code Cache", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                EnumerateCacheFiles(child, depth + 1, maxDepth, addFile);
            }
        }
        catch
        {
            // Ignore unreadable subfolders.
        }
    }


    /// <summary>
    /// Fixes URLs copied from the game cache or from the clipboard.
    /// Some builds store base64 authkeys with unescaped '+', '/' or '=' characters.
    /// </summary>
    public static string NormalizeGachaUrl(string? gachaUrl)
    {
        if (string.IsNullOrWhiteSpace(gachaUrl))
        {
            return "";
        }
        string url = gachaUrl.Trim().Trim('\0', '"', '\'');
        url = url.Replace("\\u0026", "&", StringComparison.OrdinalIgnoreCase);
        int nul = url.IndexOf('\0');
        if (nul >= 0)
        {
            url = url[..nul];
        }
        int hash = url.IndexOf('#');
        if (hash > 0)
        {
            url = url[..hash];
        }
        Match authkey = Regex.Match(url, @"authkey=([^&]+)");
        if (authkey.Success)
        {
            string value = authkey.Groups[1].Value;
            if (!value.Contains('%') && (value.Contains('+') || value.Contains('/') || value.Contains('=')))
            {
                url = string.Concat(url.AsSpan(0, authkey.Groups[1].Index),
                                    Uri.EscapeDataString(value),
                                    url.AsSpan(authkey.Groups[1].Index + value.Length));
            }
        }
        return url;
    }


    /// <summary>
    /// Finds the most recent gacha URL in a Chromium web cache data file.
    /// </summary>
    public static string? FindGachaUrlInCacheFile(string path)
    {
        const long MaxCacheFileSize = 64 * 1024 * 1024;
        try
        {
            using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (fs.Length is <= 0 or > MaxCacheFileSize)
            {
                return null;
            }
            byte[] data = new byte[fs.Length];
            fs.ReadExactly(data);
            ReadOnlySpan<byte> span = data;
            ReadOnlySpan<byte> authkey = "authkey="u8;
            int searchEnd = span.Length;
            while (searchEnd > 0)
            {
                int authkeyIndex = span[..searchEnd].LastIndexOf(authkey);
                if (authkeyIndex < 0)
                {
                    break;
                }
                int start = span[..authkeyIndex].LastIndexOf("https://"u8);
                if (start >= 0)
                {
                    int end = start;
                    while (end < span.Length)
                    {
                        byte b = span[end];
                        if (b is 0 or (byte)'"' or (byte)'\'' or (byte)'<' or (byte)'>' or (byte)'#'
                            or (byte)'\r' or (byte)'\n' or (byte)'\t' or (byte)' ' || b < 0x20 || b == 0x7F)
                        {
                            break;
                        }
                        end++;
                    }
                    if (end > start)
                    {
                        string candidate = Encoding.UTF8.GetString(span[start..end]);
                        if (IsGachaUrl(candidate))
                        {
                            return NormalizeGachaUrl(candidate);
                        }
                    }
                }
                searchEnd = authkeyIndex;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }


    private static bool IsGachaUrl(string url)
    {
        if (!url.Contains("authkey=", StringComparison.Ordinal))
        {
            return false;
        }
        return url.Contains("getGachaLog", StringComparison.OrdinalIgnoreCase)
               || url.Contains("getLdGachaLog", StringComparison.OrdinalIgnoreCase)
               || url.Contains("auth_appid=webview_gacha", StringComparison.OrdinalIgnoreCase)
               || (url.Contains("gacha", StringComparison.OrdinalIgnoreCase)
                   && url.Contains("/event/", StringComparison.OrdinalIgnoreCase));
    }


    protected static string? GetGenericGachaUrlPrefix(string gachaUrl, string cnApiPrefix, string osApiPrefix, string? lang)
    {
        string url = NormalizeGachaUrl(gachaUrl);
        int queryIndex = url.IndexOf('?');
        if (queryIndex < 0)
        {
            return null;
        }
        string query = url[queryIndex..];
        if (!query.Contains("authkey=", StringComparison.Ordinal))
        {
            return null;
        }
        string gameBiz = Regex.Match(query, @"[?&]game_biz=([^&]+)").Groups[1].Value;
        bool isGlobal = gameBiz.EndsWith("_global", StringComparison.OrdinalIgnoreCase)
                        || gameBiz.EndsWith("_os", StringComparison.OrdinalIgnoreCase)
                        || gameBiz.EndsWith("_asia", StringComparison.OrdinalIgnoreCase)
                        || url.Contains("hoyoverse.com", StringComparison.OrdinalIgnoreCase)
                        || url.Contains("webstatic-sea", StringComparison.OrdinalIgnoreCase)
                        || url.Contains("-sg.", StringComparison.OrdinalIgnoreCase);
        return BuildGachaUrlPrefix(isGlobal ? osApiPrefix : cnApiPrefix, query, lang);
    }


    protected static string BuildGachaUrlPrefix(string apiPrefix, string query, string? lang)
    {
        int hash = query.IndexOf('#');
        if (hash >= 0)
        {
            query = query[..hash];
        }
        query = Regex.Replace(query, @"[?&](?:real_gacha_type|gacha_type|page|size|end_id)=\d+", "");
        query = query.TrimStart('?', '&');
        query = "?" + query;
        if (!string.IsNullOrWhiteSpace(lang))
        {
            Match langMatch = Regex.Match(query, @"[?&]lang=[^&]+");
            if (langMatch.Success)
            {
                query = string.Concat(query.AsSpan(0, langMatch.Index),
                                      langMatch.Value[0] == '?' ? "?lang=" : "&lang=",
                                      LanguageUtil.FilterLanguage(lang),
                                      query.AsSpan(langMatch.Index + langMatch.Length));
            }
            else
            {
                query += $"&lang={LanguageUtil.FilterLanguage(lang)}";
            }
        }
        return apiPrefix + query;
    }


    /// <summary>
    /// 游戏文件中的相对路径由 Windows 程序写入，Path.Join 在 Linux 上不会拆分反斜杠。
    /// </summary>
    private static string CombineWindowsPath(string? root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return relativePath;
        }
        string[] parts = relativePath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        return Path.Combine([root, .. parts]);
    }



    private static ReadOnlySpan<byte> GetGachaUrlPattern(GameBiz gameBiz)
    {
        return gameBiz.Value switch
        {
            GameBiz.hk4e_cn or GameBiz.hk4e_bilibili => SPAN_WEB_PREFIX_YS_CN,
            GameBiz.hk4e_global => SPAN_WEB_PREFIX_YS_OS,
            GameBiz.hkrpg_cn or GameBiz.hkrpg_bilibili => SPAN_WEB_PREFIX_SR_CN,
            GameBiz.hkrpg_global => SPAN_WEB_PREFIX_SR_OS,
            GameBiz.nap_cn or GameBiz.nap_bilibili => SPAN_WEB_PREFIX_ZZZ_CN,
            GameBiz.nap_global => SPAN_WEB_PREFIX_ZZZ_OS,
            _ => throw new ArgumentOutOfRangeException($"Unknown region {gameBiz}"),
        };
    }



    #endregion




    #region protected method



    protected async Task<T> CommonGetAsync<T>(string url, CancellationToken cancellationToken = default)
    {
        var wrapper = await _httpClient.GetFromJsonAsync(url, typeof(miHoYoApiWrapper<T>), GachaLogJsonContext.Default, cancellationToken) as miHoYoApiWrapper<T>;
        if (wrapper is null)
        {
            throw new miHoYoApiException(-1, "Response body is null");
        }
        else if (wrapper.Retcode != 0)
        {
            throw new miHoYoApiException(wrapper.Retcode, wrapper.Message);
        }
        else
        {
            return wrapper.Data;
        }
    }



    protected abstract string GetGachaUrlPrefix(string gachaUrl, string? lang = null);



    protected async Task<List<T>> GetGachaLogAsync<T>(string gachaUrl, long endId = 0, string? lang = null, IProgress<(IGachaType GachaType, int Page)>? progress = null, CancellationToken cancellationToken = default) where T : GachaLogItem
    {
        endId = Math.Clamp(endId, 0, long.MaxValue);
        var prefix = GetGachaUrlPrefix(gachaUrl, lang);
        var result = new List<T>();
        foreach (var gachaType in QueryGachaTypes)
        {
            result.AddRange(await GetGachaLogByTypeAsync<T>(prefix, gachaType, endId, progress, cancellationToken));
        }
        return result;
    }




    protected async Task<List<T>> GetGachaLogAsync<T>(string gachaUrl, IGachaType gachaType, long endId = 0, string? lang = null, IProgress<(IGachaType GachaType, int Page)>? progress = null, CancellationToken cancellationToken = default) where T : GachaLogItem
    {
        endId = Math.Clamp(endId, 0, long.MaxValue);
        var prefix = GetGachaUrlPrefix(gachaUrl, lang);
        return await GetGachaLogByTypeAsync<T>(prefix, gachaType, endId, progress, cancellationToken);
    }




    protected virtual async Task<List<T>> GetGachaLogByQueryAsync<T>(string gachaUrlPrefix, GachaLogQuery param, CancellationToken cancellationToken = default) where T : GachaLogItem
    {
        await Task.Delay(Random.Shared.Next(200, 300), cancellationToken);
        var url = $"{gachaUrlPrefix}&{param}";
        var wrapper = await _httpClient.GetFromJsonAsync(url, typeof(miHoYoApiWrapper<GachaLogResult<T>>), GachaLogJsonContext.Default, cancellationToken) as miHoYoApiWrapper<GachaLogResult<T>>;
        if (wrapper is null)
        {
            return new List<T>();
        }
        else if (wrapper.Retcode != 0)
        {
            throw new miHoYoApiException(wrapper.Retcode, wrapper.Message);
        }
        else
        {
            return wrapper.Data.List;
        }
    }




    protected async Task<List<T>> GetGachaLogByTypeAsync<T>(string prefix, IGachaType gachaType, long endId = 0, IProgress<(IGachaType GachaType, int Page)>? progress = null, CancellationToken cancellationToken = default) where T : GachaLogItem
    {
        var param = new GachaLogQuery(gachaType, 1, 20, 0);
        var result = new List<T>();
        while (true)
        {
            progress?.Report((gachaType, param.Page));
            var list = await GetGachaLogByQueryAsync<T>(prefix, param, cancellationToken);
            result.AddRange(list);
            if (list.Count == 20 && list.Last().Id > endId)
            {
                param.Page++;
                param.EndId = list.Last().Id;
            }
            else
            {
                break;
            }
        }
        return result;
    }


    [SupportedOSPlatform("windows")]
    protected static string? GetGameInstallPathFromRegistry(string regKey)
    {
        var launcherPath = Registry.GetValue(regKey, "InstallPath", null) as string;
        var configPath = Path.Join(launcherPath, "config.ini");
        if (File.Exists(configPath))
        {
            var str = File.ReadAllText(configPath);
            var installPath = Regex.Match(str, @"game_install_path=(.+)").Groups[1].Value.Trim();
            if (Directory.Exists(installPath))
            {
                return Path.GetFullPath(installPath);
            }
        }
        return null;
    }


    protected static string? FindMatchStringFromFile(string path, ReadOnlySpan<byte> prefix)
    {
        using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var ms = new MemoryStream();
        fs.CopyTo(ms);
        var span = ms.ToArray().AsSpan();
        var index = span.LastIndexOf(prefix);
        if (index >= 0)
        {
            var length = span[index..].IndexOfAny("\0\""u8);
            return Encoding.UTF8.GetString(span.Slice(index, length));
        }

        return null;
    }


    #endregion




    #region Gacha Info


    public async Task<GenshinGachaWiki> GetGenshinGachaInfoAsync(GameBiz gameBiz, string lang, CancellationToken cancellationToken = default)
    {
        lang = LanguageUtil.FilterLanguage(lang);
        GenshinGachaWiki wiki;
        if (gameBiz.IsChinaServer() && lang is "zh-cn")
        {
            const string url = "https://api-takumi.mihoyo.com/event/platsimulator/config?gids=2&game=hk4e";
            wiki = await CommonGetAsync<GenshinGachaWiki>(url, cancellationToken);
        }
        else
        {
            string url = $"https://sg-public-api.hoyolab.com/event/simulatoros/config?lang={lang}";
            wiki = await CommonGetAsync<GenshinGachaWiki>(url, cancellationToken);
        }
        wiki.Language = lang;
        return wiki;
    }


    public async Task<StarRailGachaWiki> GetStarRailGachaInfoAsync(GameBiz gameBiz, string lang, CancellationToken cancellationToken = default)
    {
        lang = LanguageUtil.FilterLanguage(lang);
        StarRailGachaWiki wiki;
        if (gameBiz.IsChinaServer() && lang is "zh-cn")
        {
            const string url = "https://api-takumi.mihoyo.com/event/rpgsimulator/config?game=hkrpg";
            wiki = await CommonGetAsync<StarRailGachaWiki>(url, cancellationToken);
        }
        else
        {
            wiki = new StarRailGachaWiki { Game = "hkrpg", Avatar = new List<StarRailGachaInfo>(), Equipment = new List<StarRailGachaInfo>() };
            for (int i = 1; i <= 10; i++)
            {
                string url = $"https://sg-public-api.hoyolab.com/event/rpgcalc/avatar/list?game=hkrpg&lang={lang}&tab_from=TabAll&page={i}&size=100";
                var wrapper = await CommonGetAsync<StarRailGachaInfoWrapper>(url, cancellationToken);
                wiki.Avatar.AddRange(wrapper.List);
                if (wrapper.List.Count != 100)
                {
                    break;
                }
            }
            for (int i = 1; i <= 10; i++)
            {
                string url = $"https://sg-public-api.hoyolab.com/event/rpgcalc/equipment/list?game=hkrpg&lang={lang}&tab_from=TabAll&page={i}&size=100";
                var wrapper = await CommonGetAsync<StarRailGachaInfoWrapper>(url, cancellationToken);
                wiki.Equipment.AddRange(wrapper.List);
                if (wrapper.List.Count != 100)
                {
                    break;
                }
            }
        }
        wiki.Language = lang;
        return wiki;
    }


    public async Task<ZZZGachaWiki> GetZZZGachaInfoAsync(GameBiz gameBiz, string lang, CancellationToken cancellationToken = default)
    {
        lang = LanguageUtil.FilterLanguage(lang);
        ZZZGachaWiki wiki;
        if (gameBiz.IsChinaServer() && lang is "zh-cn")
        {
            const string url = $"https://starward-static.scighost.com/metadata/v1/zzz/ZZZGachaInfo.nap_cn.zh-cn.json";
            wiki = await CommonGetAsync<ZZZGachaWiki>(url, cancellationToken);
        }
        else
        {
            string url = $"https://starward-static.scighost.com/metadata/v1/zzz/ZZZGachaInfo.nap_global.{lang}.json";
            wiki = await CommonGetAsync<ZZZGachaWiki>(url, cancellationToken);
        }
        if (string.IsNullOrWhiteSpace(wiki.Language))
        {
            wiki.Language = lang;
        }
        return wiki;
    }



    #endregion



}
