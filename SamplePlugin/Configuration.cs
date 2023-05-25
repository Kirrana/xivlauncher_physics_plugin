using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace HighFpsPhysicsPlugin;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public bool EnableOnStartup = false;
    private DalamudPluginInterface? pluginInterface;
    public int PhysicsFrameTolerance { get; set; } = 2;
    public int Version { get; set; } = 2;

    public void Initialize(DalamudPluginInterface pluginInterface) => this.pluginInterface = pluginInterface;

    public void Save() => this.pluginInterface!.SavePluginConfig(this);
}