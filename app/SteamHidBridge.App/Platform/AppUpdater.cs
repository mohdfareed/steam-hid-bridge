using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    public string CurrentVersionText => $"v{Format(CurrentVersion)}";

    public string LatestVersionText => $"v{Format(LatestVersion)}";

    private static string Format(Version version)
    {
        return $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
    }
}

internal sealed class AppUpdater
{
    private const string Owner = "mohdfareed";
    private const string Repository = "steam-hid-bridge";
    private const string PackageAssetName = "SteamHidBridge-win-x64.zip";
    private const string UpdaterAssetName = "SteamHidBridge-updater.ps1";

    private static readonly Uri LatestReleaseUri = new($"https://api.github.com/repos/{Owner}/{Repository}/releases/latest");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Version CurrentVersion { get; } = ReadCurrentVersion();

    public string CurrentVersionText => $"v{CurrentVersion.Major}.{CurrentVersion.Minor}.{Math.Max(CurrentVersion.Build, 0)}";

    public async Task<AppUpdateCheckResult> CheckLatestAsync(CancellationToken cancellationToken = default)
    {
        using HttpClient http = new();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SteamHidBridge");

        using HttpResponseMessage response = await http.GetAsync(LatestReleaseUri, cancellationToken);
        _ = response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        GitHubRelease release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("GitHub returned an empty release response.");

        if (!TryParseVersion(release.TagName, out Version latestVersion))
        {
            throw new InvalidOperationException($"Latest release tag '{release.TagName}' is not a vX.Y.Z version.");
        }

        GitHubAsset asset = release.Assets.FirstOrDefault(candidate => candidate.Name == PackageAssetName)
            ?? throw new InvalidOperationException($"Latest release does not include {PackageAssetName}.");
        GitHubAsset updater = release.Assets.FirstOrDefault(candidate => candidate.Name == UpdaterAssetName)
            ?? throw new InvalidOperationException($"Latest release does not include {UpdaterAssetName}.");

        return new AppUpdateCheckResult(CurrentVersion, latestVersion, release.TagName, asset.DownloadUrl, updater.DownloadUrl);
    }

    public static void StartUpdate(AppUpdateCheckResult update)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"SteamHidBridgeUpdate-{Guid.NewGuid():N}");
        string tempScriptPath = Path.Combine(tempRoot, UpdaterAssetName);
        string tempCommandPath = Path.Combine(tempRoot, "start-update.cmd");

        string installDir = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        _ = Directory.CreateDirectory(tempRoot);
        _ = Directory.CreateDirectory(AppDataPaths.LogDirectory);
        string logPath = AppDataPaths.UpdateLogPath;
        string powerShellCommand =
            $"$ErrorActionPreference='Stop'; " +
            $"Invoke-WebRequest -Uri {PowerShellQuote(update.UpdaterUrl)} -OutFile {PowerShellQuote(tempScriptPath)}; " +
            $"& {PowerShellQuote(tempScriptPath)} " +
            $"-PackageUrl {PowerShellQuote(update.PackageUrl)} " +
            $"-InstallDir {PowerShellQuote(installDir)} " +
            $"-CurrentProcessId {Environment.ProcessId}";

        File.WriteAllText(
            tempCommandPath,
            $"""
                @echo off
                echo Steam HID Bridge updater
                echo Log: {logPath}
                echo.
                powershell.exe -NoProfile -ExecutionPolicy Bypass -Command {CommandLineQuote(powerShellCommand)} > {CommandLineQuote(logPath)} 2>&1
                set UPDATE_EXIT_CODE=%ERRORLEVEL%
                type {CommandLineQuote(logPath)}
                echo.
                echo Updater exit code: %UPDATE_EXIT_CODE%
                echo.
                pause
                exit /b %UPDATE_EXIT_CODE%
            """);

        _ = Process.Start(new ProcessStartInfo
        {
            FileName = tempCommandPath,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal
        });
    }

    private static Version ReadCurrentVersion()
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(AppUpdater).Assembly;
        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

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

        string candidate = value.Trim();
        if (candidate.StartsWith('v') || candidate.StartsWith('V'))
        {
            candidate = candidate[1..];
        }

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

    private static string CommandLineQuote(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string PowerShellQuote(string value)
    {
        return $"'{value.Replace("'", "''")}'";
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = "";

        [JsonPropertyName("assets")]
        public GitHubAsset[] Assets { get; init; } = [];
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("browser_download_url")]
        public string DownloadUrl { get; init; } = "";
    }
}
