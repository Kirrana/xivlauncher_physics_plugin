using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace HighFpsPhysicsPlugin;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public bool EnableOnStartup = false;
    private IDalamudPluginInterface? pluginInterface;
    public float TargetFPS { get; set; } = 60f;
    public int Version { get; set; } = 2;

    public void Initialize(IDalamudPluginInterface pluginInterface) => this.pluginInterface = pluginInterface;

    public void Save() => this.pluginInterface!.SavePluginConfig(this);
}