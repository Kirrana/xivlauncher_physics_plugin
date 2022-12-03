using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace HighFpsPhysicsPlugin;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public int UpdatesToSkip = 2;
    public bool EnableOnStartup = false;

    private DalamudPluginInterface? pluginInterface;
    public void Initialize(DalamudPluginInterface pluginInterface) => this.pluginInterface = pluginInterface;
    public void Save() => this.pluginInterface!.SavePluginConfig(this);
}