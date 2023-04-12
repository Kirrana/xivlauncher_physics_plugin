using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace HighFpsPhysicsPlugin;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    /// <summary>
    /// Updates based on currently selected option in the UI combo box. See SkipUpdateOptionsList: <inheritdoc cref="SkipUpdateOptionsList"/>.
    /// </summary>
    public int FramesPerPhysicsUpdate
    {
        get
        {
            return SkipUpdateOptionsList[SkipUpdatesOptionsListSelectedIndex];
        }
    }

    /// <summary>
    /// The list provided for allowable numbers of frames to skip physics updates.
    /// </summary>
    public int[] SkipUpdateOptionsList = new int[]
    {
        2,
        3,
        4,  
        5,
    };

    /// <summary>
    /// Automatically modifies UpdatesToSkip based on the selected interface combo box.
    /// </summary>
    public int SkipUpdatesOptionsListSelectedIndex = 0;

    public bool EnableOnStartup = false;

    private DalamudPluginInterface? pluginInterface;
    public void Initialize(DalamudPluginInterface pluginInterface) => this.pluginInterface = pluginInterface;
    public void Save() => this.pluginInterface!.SavePluginConfig(this);
}