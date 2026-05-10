using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SteamHidBridge.App.Platform.App;

namespace SteamHidBridge.App.Platform.Board;

public sealed class BoardFirmwareUpdater
{
    private const string FirmwareFileName = "SteamHidBridge.Board";
    private readonly string firmwareDirectory = Path.Combine(AppContext.BaseDirectory, "Firmware");

    public string StatusText
    {
        get
        {
            string hexPath = Path.Combine(firmwareDirectory, FirmwareFileName + ".hex");
            return File.Exists(hexPath)
                ? "Flash the packaged firmware to update the board."
                : "Firmware package is missing.";
        }
    }

    public async Task UpdateAsync(CancellationToken cancellationToken = default)
    {
        string hexPath = Path.Combine(firmwareDirectory, FirmwareFileName + ".hex");
        string uploaderPath = Path.Combine(firmwareDirectory, "teensy_post_compile.exe");
        if (!File.Exists(hexPath))
        {
            throw new FileNotFoundException("Bundled board firmware was not found.", hexPath);
        }

        if (!File.Exists(uploaderPath))
        {
            throw new FileNotFoundException("Bundled board firmware uploader was not found.", uploaderPath);
        }

        string arguments = string.Join(
            ' ',
            $"-file={FirmwareFileName}",
            $"-path=\"{firmwareDirectory}\"",
            $"-tools=\"{firmwareDirectory}\"",
            "-board=TEENSY40",
            "-reboot");

        await RunAsync(uploaderPath, arguments, cancellationToken).ConfigureAwait(false);
    }

    private static async Task RunAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            }
        };

        AppLog.Write($"firmware update start file={fileName} args={arguments}");
        _ = process.Start();
        string standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        string standardError = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(standardOutput))
        {
            AppLog.Write($"firmware update stdout={standardOutput.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(standardError))
        {
            AppLog.Write($"firmware update stderr={standardError.Trim()}");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Firmware update failed with exit code {process.ExitCode}.");
        }
    }
}
