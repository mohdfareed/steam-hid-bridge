using System;

namespace SteamHidBridge.App.Input;

public sealed record MouseInputLoopStatistics(
    TimeSpan PollInterval,
    long PollCount,
    long FrameCount,
    double PollsPerSecond,
    double FramesPerSecond);
