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

namespace SteamHidBridge.App.Updates;

public sealed class AppUpdater
{
    private const string Owner = "mohdfareed";
    private const string Repository = "steam-hid-bridge";
    private const string PackageAssetName = "SteamHidBridge-win-x64.zip";

    private static readonly Uri LatestReleaseUri = new($"https://api.github.com/repos/{Owner}/{Repository}/releases/latest");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Version CurrentVersion { get; } = ReadCurrentVersion();

    public string CurrentVersionText => $"v{CurrentVersion.Major}.{CurrentVersion.Minor}.{Math.Max(CurrentVersion.Build, 0)}";

    public async Task<AppUpdateCheckResult> CheckLatestAsync(CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SteamHidBridge");

        using var response = await http.GetAsync(LatestReleaseUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("GitHub returned an empty release response.");

        if (!TryParseVersion(release.TagName, out var latestVersion))
        {
            throw new InvalidOperationException($"Latest release tag '{release.TagName}' is not a vX.Y.Z version.");
        }

        var asset = release.Assets.FirstOrDefault(candidate => candidate.Name == PackageAssetName)
            ?? throw new InvalidOperationException($"Latest release does not include {PackageAssetName}.");

        return new AppUpdateCheckResult(CurrentVersion, latestVersion, release.TagName, asset.DownloadUrl);
    }

    public static void StartUpdate(AppUpdateCheckResult update)
    {
        var sourceScriptPath = Path.Combine(AppContext.BaseDirectory, "update.ps1");
        if (!File.Exists(sourceScriptPath))
        {
            throw new FileNotFoundException("The updater script is missing from the app folder.", sourceScriptPath);
        }

        var tempScriptPath = Path.Combine(
            Path.GetTempPath(),
            $"SteamHidBridgeUpdate-{Guid.NewGuid():N}.ps1");
        var tempCommandPath = Path.ChangeExtension(tempScriptPath, ".cmd");

        File.Copy(sourceScriptPath, tempScriptPath, overwrite: true);

        var installDir = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        var logsDir = Path.Combine(installDir, "logs");
        Directory.CreateDirectory(logsDir);
        var logPath = Path.Combine(logsDir, "update.log");

        var powerShellArguments = string.Join(
            ' ',
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            CommandLineQuote(tempScriptPath),
            "-PackageUrl",
            CommandLineQuote(update.PackageUrl),
            "-InstallDir",
            CommandLineQuote(installDir),
            "-CurrentProcessId",
            Environment.ProcessId.ToString());

        File.WriteAllText(
            tempCommandPath,
            $"""
            @echo off
            echo Steam HID Bridge updater
            echo Log: {logPath}
            echo.
            powershell.exe {powerShellArguments} > {CommandLineQuote(logPath)} 2>&1
            set UPDATE_EXIT_CODE=%ERRORLEVEL%
            type {CommandLineQuote(logPath)}
            echo.
            echo Updater exit code: %UPDATE_EXIT_CODE%
            echo.
            pause
            exit /b %UPDATE_EXIT_CODE%
            """);

        Process.Start(new ProcessStartInfo
        {
            FileName = tempCommandPath,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal
        });
    }

    private static Version ReadCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(AppUpdater).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (TryParseVersion(informationalVersion, out var parsedVersion))
        {
            return parsedVersion;
        }

        return NormalizeVersion(assembly.GetName().Version ?? new Version(0, 0, 0));
    }

    private static bool TryParseVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0);

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        if (candidate.StartsWith('v') || candidate.StartsWith('V'))
        {
            candidate = candidate[1..];
        }

        var suffixIndex = candidate.IndexOfAny(['+', '-']);
        if (suffixIndex >= 0)
        {
            candidate = candidate[..suffixIndex];
        }

        if (!Version.TryParse(candidate, out var parsed))
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
