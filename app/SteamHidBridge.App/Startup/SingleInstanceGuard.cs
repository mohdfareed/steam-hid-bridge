using System;
using System.Threading;

namespace SteamHidBridge.App.Startup;

public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\SteamHidBridge.SingleInstance";
    private readonly Mutex mutex;

    private SingleInstanceGuard(Mutex mutex)
    {
        this.mutex = mutex;
    }

    public static SingleInstanceGuard? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (createdNew)
        {
            return new SingleInstanceGuard(mutex);
        }

        mutex.Dispose();
        return null;
    }

    public void Dispose()
    {
        mutex.ReleaseMutex();
        mutex.Dispose();
    }
}
