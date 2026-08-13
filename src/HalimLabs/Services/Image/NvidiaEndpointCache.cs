using System.IO;
using HalimLabs.Models;

namespace HalimLabs.Services.Image;

/// <summary>
/// Remembers NVIDIA endpoints that 404 for this API key so the UI does not keep selecting them.
/// </summary>
public static class NvidiaEndpointCache
{
    private static readonly object Gate = new();
    private static readonly HashSet<string> Unavailable = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HalimLabs3",
        "unavailable-endpoints.txt");

    static NvidiaEndpointCache() => Load();

    public static string KeyOf(ImageModelProfile profile) =>
        (profile.ApiBaseUrl ?? string.Empty).TrimEnd('/').ToLowerInvariant();

    public static bool IsUnavailable(ImageModelProfile profile)
    {
        var key = KeyOf(profile);
        if (string.IsNullOrEmpty(key))
            return false;
        lock (Gate)
            return Unavailable.Contains(key);
    }

    public static void MarkUnavailable(ImageModelProfile profile)
    {
        var key = KeyOf(profile);
        if (string.IsNullOrEmpty(key))
            return;
        lock (Gate)
        {
            if (Unavailable.Add(key))
                Save();
        }
    }

    public static void MarkAvailable(ImageModelProfile profile)
    {
        var key = KeyOf(profile);
        if (string.IsNullOrEmpty(key))
            return;
        lock (Gate)
        {
            if (Unavailable.Remove(key))
                Save();
        }
    }

    private static void Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return;
            foreach (var line in File.ReadAllLines(FilePath))
            {
                var key = line.Trim();
                if (key.Length > 0)
                    Unavailable.Add(key);
            }
        }
        catch
        {
            // cache is optional
        }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllLines(FilePath, Unavailable.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        }
        catch
        {
            // cache is optional
        }
    }
}
