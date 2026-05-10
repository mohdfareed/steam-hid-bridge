using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using SteamHidBridge.App.Infrastructure;
using SteamHidBridge.App.Profiles;
using SteamHidBridge.App.Windows;

namespace SteamHidBridge.App.Runtime;

public sealed class GameProcessHost(Action<string> setActivity) : IDisposable
{
    private ChildProcessJob? childProcessJob;
    private Process? process;

    public bool HasLaunchedProcess => process is not null;
    public bool HasExited { get; private set; } = true;

    public void Launch(string id, GameProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Executable))
        {
            setActivity("No executable configured.");
            return;
        }

        if (!File.Exists(profile.Executable))
        {
            setActivity($"Executable not found: {profile.Executable}");
            return;
        }

        if (process is { HasExited: false })
        {
            setActivity("A launched process is already running.");
            return;
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
            });

            if (launchedProcess is null)
            {
                setActivity("Launch failed: process was not created.");
                return;
            }

            Track(launchedProcess);
            setActivity($"Launched {id}.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            setActivity($"Launch failed: {ex.Message}");
        }
    }

    public void Stop()
    {
        try
        {
            if (process is { HasExited: false } runningProcess)
            {
                setActivity($"Stopping launched process {runningProcess.Id}.");
                runningProcess.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            setActivity($"Could not stop launched process: {ex.Message}");
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
            setActivity($"Launched process exited: {launchedProcess.Id}");
        };

        if (TryTrackProcessTree(launchedProcess))
        {
            AppLog.Write($"tracking launched process tree={launchedProcess.Id}");
        }
        else
        {
            AppLog.Write($"tracking launched process directly={launchedProcess.Id}");
        }
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
            AppLog.WriteException("child-process-job-unavailable", ex);
            return false;
        }
    }
}
