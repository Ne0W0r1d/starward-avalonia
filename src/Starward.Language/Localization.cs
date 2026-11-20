using System.Globalization;

namespace Starward.Language;

public static class Localization
{



    public static readonly IReadOnlyCollection<(string Title, string LangCode)> LanguageList = new List<(string, string)>
    {
        ("Deutsch (de-DE)", "de-DE"),
        ("English (en-US)", "en-US"),
        ("Italiano (it-IT)", "it-IT"),
        ("日本語 (ja-JP)", "ja-JP"),
        ("한국어 (ko-KR)", "ko-KR"),
        ("Русский (ru-RU)", "ru-RU"),
        ("ภาษาไทย (th-TH)", "th-TH"),
        ("Tiếng Việt (vi-VN)", "vi-VN"),
        ("简体中文 (zh-CN)", "zh-CN"),
        ("繁體中文 - 香港地區 (zh-HK)", "zh-HK"),
        ("繁體中文 - 台灣地區 (zh-TW)", "zh-TW"),
    }.AsReadOnly();


    public static string Get(string key)
    {
        string? value = Lang.ResourceManager.GetString(key, CultureInfo.CurrentUICulture);
        return string.IsNullOrEmpty(value) ? key : value;
    }


    public static string Format(string key, params object?[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), args);
    }


}
