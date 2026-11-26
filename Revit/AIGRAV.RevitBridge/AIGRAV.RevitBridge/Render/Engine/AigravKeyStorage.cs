using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIGRAV.RevitBridge.Render.Engine;

public static class AigravKeyStorage
{
    private static readonly byte[] Entropy = "AIGRAV_RENDER_ENCRYPTION_SALT_2026"u8.ToArray();

    private static string StorageFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AIGRAV",
        "keys.dat");

    public static void SetKey(string provider, string key)
    {
        var dict = GetAllKeys();
        dict[provider.Trim().ToLowerInvariant()] = key.Trim();
        SaveAll(dict);
    }

    public static string? GetKey(string provider)
    {
        var dict = GetAllKeys();
        string normalized = provider.Trim().ToLowerInvariant();
        return dict.TryGetValue(normalized, out var val) ? val : null;
    }

    public static Dictionary<string, string> GetAllKeys()
    {
        string path = StorageFile;
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            byte[] encrypted = File.ReadAllBytes(path);
            byte[] decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            string json = Encoding.UTF8.GetString(decrypted);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void SaveAll(Dictionary<string, string> dict)
    {
        string path = StorageFile;
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = false });
        byte[] plaintext = Encoding.UTF8.GetBytes(json);
        byte[] encrypted = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path, encrypted);
    }
}
