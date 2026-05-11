using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SteamHidBridge.App.Platform.Teensy;

internal sealed class TeensyFirmwareUpdater
{
    private const string FirmwareFileName = "SteamHidBridge.Board";
    private readonly string firmwareDirectory = Path.Combine(AppContext.BaseDirectory, "Firmware");

    public bool HasBundledFirmware => File.Exists(Path.Combine(firmwareDirectory, FirmwareFileName + ".hex"));

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

        _ = process.Start();
        string standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        string standardError = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Firmware update failed with exit code {process.ExitCode}.{Environment.NewLine}{standardError}{standardOutput}".Trim());
        }
    }
}
