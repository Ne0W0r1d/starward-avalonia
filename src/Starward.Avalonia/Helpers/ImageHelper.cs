using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Starward.Helpers;

public static class ImageHelper
{

    private static readonly HttpClient HttpClient = new(new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All,
    });


    public static Bitmap? LoadAsset(string relativePath)
    {
        try
        {
            Uri uri = new($"avares://Starward/{relativePath.TrimStart('/')}");
            using Stream stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }


    public static Bitmap? LoadFile(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return new Bitmap(path);
            }
        }
        catch
        {
            // ignored
        }
        return null;
    }


    public static async Task<Bitmap?> LoadRemoteAsync(string? url, bool forceRefresh = false)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }
        try
        {
            string folder = Path.Combine(AppConfig.CacheFolder, "image");
            Directory.CreateDirectory(folder);
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..24];
            string extension = Path.GetExtension(new Uri(url).AbsolutePath);
            if (string.IsNullOrWhiteSpace(extension) || extension.Length > 6)
            {
                extension = ".img";
            }
            string file = Path.Combine(folder, hash + extension);
            if (forceRefresh || !File.Exists(file) || new FileInfo(file).Length == 0)
            {
                byte[] bytes = await HttpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(file, bytes);
            }
            return new Bitmap(file);
        }
        catch
        {
            return null;
        }
    }

}
