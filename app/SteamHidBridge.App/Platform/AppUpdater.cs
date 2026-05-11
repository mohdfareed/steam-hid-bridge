using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SteamHidBridge.App.Platform.App;

namespace SteamHidBridge.App.Platform;

internal sealed record AppUpdateCheckResult(
    Version CurrentVersion,
    Version LatestVersion,
    string LatestTag,
    string PackageUrl,
    string UpdaterUrl)
{
    public bool IsUpdateAvailable => LatestVersion > CurrentVersion;
    public string CurrentVersionText => $"v{CurrentVersion.Major}.{CurrentVersion.Minor}.{Math.Max(CurrentVersion.Build, 0)}";
    public string LatestVersionText => $"v{LatestVersion.Major}.{LatestVersion.Minor}.{Math.Max(LatestVersion.Build, 0)}";
}

internal sealed class AppUpdater
{
    private const string Owner = "mohdfareed";
    private const string Repository = "steam-hid-bridge";
    private const string PackageAssetName = "SteamHidBridge-win-x64.zip";
    private const string UpdaterAssetName = "SteamHidBridge-updater.ps1";
    private static readonly Uri LatestReleaseUri = new($"https://api.github.com/repos/{Owner}/{Repository}/releases/latest");

    public Version CurrentVersion { get; } = ReadCurrentVersion();
    public string CurrentVersionText => $"v{CurrentVersion.Major}.{CurrentVersion.Minor}.{Math.Max(CurrentVersion.Build, 0)}";

    public async Task<AppUpdateCheckResult> CheckLatestAsync(CancellationToken cancellationToken = default)
    {
        using HttpClient http = CreateHttpClient();
        await using Stream stream = await http.GetStreamAsync(LatestReleaseUri, cancellationToken).ConfigureAwait(false);
        using JsonDocument json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        string tagName = json.RootElement.GetProperty("tag_name").GetString()
            ?? throw new InvalidOperationException("GitHub returned an empty release tag.");

        if (!TryParseVersion(tagName, out Version latestVersion))
        {
            throw new InvalidOperationException($"Latest release tag '{tagName}' is not a vX.Y.Z version.");
        }

        string packageUrl = string.Empty;
        string updaterUrl = string.Empty;
        foreach (JsonElement asset in json.RootElement.GetProperty("assets").EnumerateArray())
        {
            string? name = asset.GetProperty("name").GetString();
            string? downloadUrl = asset.GetProperty("browser_download_url").GetString();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(downloadUrl))
            {
                continue;
            }

            if (name == PackageAssetName)
            {
                packageUrl = downloadUrl;
            }
            else if (name == UpdaterAssetName)
            {
                updaterUrl = downloadUrl;
            }
        }

        return string.IsNullOrWhiteSpace(packageUrl)
            ? throw new InvalidOperationException($"Latest release does not include {PackageAssetName}.")
            : string.IsNullOrWhiteSpace(updaterUrl)
            ? throw new InvalidOperationException($"Latest release does not include {UpdaterAssetName}.")
            : new AppUpdateCheckResult(CurrentVersion, latestVersion, tagName, packageUrl, updaterUrl);
    }

    public static async Task StartUpdateAsync(AppUpdateCheckResult update, CancellationToken cancellationToken = default)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"SteamHidBridgeUpdate-{Guid.NewGuid():N}");
        string tempScriptPath = Path.Combine(tempRoot, UpdaterAssetName);
        _ = Directory.CreateDirectory(tempRoot);
        _ = Directory.CreateDirectory(AppDataPaths.LogDirectory);

        using HttpClient http = CreateHttpClient();
        await using (Stream source = await http.GetStreamAsync(update.UpdaterUrl, cancellationToken).ConfigureAwait(false))
        await using (FileStream destination = File.Create(tempScriptPath))
        {
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }

        ProcessStartInfo start = new()
        {
            FileName = "powershell.exe",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(tempScriptPath);
        start.ArgumentList.Add("-PackageUrl");
        start.ArgumentList.Add(update.PackageUrl);
        start.ArgumentList.Add("-InstallDir");
        start.ArgumentList.Add(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
        start.ArgumentList.Add("-CurrentProcessId");
        start.ArgumentList.Add(Environment.ProcessId.ToString());
        start.ArgumentList.Add("-LogPath");
        start.ArgumentList.Add(AppDataPaths.UpdateLogPath);
        _ = Process.Start(start);
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClient http = new();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SteamHidBridge");
        return http;
    }

    private static Version ReadCurrentVersion()
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(AppUpdater).Assembly;
        string? informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return TryParseVersion(informationalVersion, out Version parsedVersion)
            ? parsedVersion
            : NormalizeVersion(assembly.GetName().Version ?? new Version(0, 0, 0));
    }

    private static bool TryParseVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string candidate = value.Trim().TrimStart('v', 'V');
        int suffixIndex = candidate.IndexOfAny(['+', '-']);
        if (suffixIndex >= 0)
        {
            candidate = candidate[..suffixIndex];
        }

        if (!Version.TryParse(candidate, out Version? parsed))
        {
            return false;
        }

        version = NormalizeVersion(parsed);
        return true;
    }

    private static Version NormalizeVersion(Version version)
    {
        return new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
    }
}
