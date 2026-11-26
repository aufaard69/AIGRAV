using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIGRAV.RevitBridge;

public class VersionInfo
{
    public string version { get; set; } = "0.0.0";
    public string release_name { get; set; } = "";
    public string release_date { get; set; } = "";
    public List<string> highlights { get; set; } = new();
    public List<string> fixes { get; set; } = new();
}

public static class AIGRAVVersionChecker
{
    private const string RemoteVersionUrl = "https://raw.githubusercontent.com/aufaard69/AIGRAV/main/version.json";
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    private static IEnumerable<string> GetCandidateRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string? dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrWhiteSpace(dllDir))
        {
            DirectoryInfo? dir = new(dllDir);
            for (int i = 0; i < 8 && dir != null; i++)
            {
                if (seen.Add(dir.FullName))
                {
                    yield return dir.FullName;
                }
                dir = dir.Parent;
            }
        }

        foreach (string root in new[] { @"D:\AIGRAV", @"C:\AIGRAV" })
        {
            if (seen.Add(root))
            {
                yield return root;
            }
        }
    }

    public static string? ResolveRootFile(string fileName)
    {
        foreach (string root in GetCandidateRoots())
        {
            string candidate = Path.Combine(root, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    public static VersionInfo GetLocalVersion()
    {
        try
        {
            string? path = ResolveRootFile("version.json");
            if (path != null)
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<VersionInfo>(json) ?? new VersionInfo();
            }
        }
        catch { }
        return new VersionInfo { version = "3.2.0", release_name = "AIGRAV Core" };
    }

    public static async Task<(bool hasUpdate, VersionInfo? remoteInfo)> CheckForUpdatesAsync()
    {
        try
        {
            var local = GetLocalVersion();
            using var req = new HttpRequestMessage(HttpMethod.Get, RemoteVersionUrl);
            req.Headers.Add("User-Agent", "AIGRAV-RevitBridge");

            var resp = await _httpClient.SendAsync(req).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
            {
                string json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                var remote = JsonSerializer.Deserialize<VersionInfo>(json);
                if (remote != null && IsNewer(remote.version, local.version))
                {
                    return (true, remote);
                }
            }
        }
        catch
        {
            // Network failure or offline should never interrupt Revit.
        }
        return (false, null);
    }

    private static bool IsNewer(string remote, string local)
    {
        try
        {
            Version r = Version.Parse(remote.TrimStart('v', 'V'));
            Version l = Version.Parse(local.TrimStart('v', 'V'));
            return r > l;
        }
        catch
        {
            return false;
        }
    }

    public static bool TriggerUpdateScript()
    {
        string? scriptPath = ResolveRootFile("sync-aigrav.ps1");
        if (scriptPath == null) return false;

        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" -Force",
                UseShellExecute = true,
                CreateNoWindow = false
            };
            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
