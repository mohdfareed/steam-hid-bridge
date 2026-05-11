using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Platform.Windows;

namespace SteamHidBridge.App.Core.Runtime;

internal sealed class GameProcessHost : IDisposable
{
    private ChildProcessJob? childProcessJob;
    private Process? process;

    public bool HasLaunchedProcess => process is not null;
    public bool HasExited { get; private set; } = true;

    public void Launch(GameProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Executable))
        {
            throw new InvalidOperationException("No executable configured.");
        }

        if (!File.Exists(profile.Executable))
        {
            throw new FileNotFoundException($"Executable not found: {profile.Executable}", profile.Executable);
        }

        if (process is { HasExited: false })
        {
            throw new InvalidOperationException("A launched process is already running.");
        }

        string workingDirectory = string.IsNullOrWhiteSpace(profile.WorkingDirectory)
            ? Path.GetDirectoryName(profile.Executable) ?? AppContext.BaseDirectory
            : profile.WorkingDirectory;

        try
        {
            Process? launchedProcess = Process.Start(new ProcessStartInfo
            {
                FileName = profile.Executable,
                Arguments = profile.Arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false
            }) ?? throw new InvalidOperationException("Launch failed.");
            Track(launchedProcess);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            throw new InvalidOperationException($"Launch failed: {ex.Message}", ex);
        }
    }

    public void Stop()
    {
        try
        {
            if (process is { HasExited: false } runningProcess)
            {
                runningProcess.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            Trace.TraceError($"stop-launched-process-failed{Environment.NewLine}{ex}");
        }
        finally
        {
            process?.Dispose();
            process = null;
            childProcessJob?.Dispose();
            childProcessJob = null;
            HasExited = true;
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private void Track(Process launchedProcess)
    {
        process?.Dispose();
        process = launchedProcess;
        HasExited = false;

        launchedProcess.EnableRaisingEvents = true;
        launchedProcess.Exited += (_, _) =>
        {
            HasExited = true;
        };

        _ = TryTrackProcessTree(launchedProcess);
    }

    private bool TryTrackProcessTree(Process launchedProcess)
    {
        try
        {
            childProcessJob ??= new ChildProcessJob();
            return childProcessJob.TryAdd(launchedProcess);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            Trace.TraceError($"child-process-job-unavailable{Environment.NewLine}{ex}");
            return false;
        }
    }
}
