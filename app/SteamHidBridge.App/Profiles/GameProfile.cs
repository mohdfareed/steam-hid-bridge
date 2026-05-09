using System.Collections.Generic;

namespace SteamHidBridge.App.Profiles;

public sealed class GameProfile
{
    public string Title { get; set; } = string.Empty;
    public string Executable { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public List<string> ReceiverProcesses { get; set; } = [];
}
