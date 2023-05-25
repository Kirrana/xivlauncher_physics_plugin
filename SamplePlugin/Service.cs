using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;

namespace HighFpsPhysicsPlugin;

internal class Service
{
    public static Physics PhysicsModification = null!;
    public static Configuration Settings = null!;
    public static WindowSystem WindowSystem = new("HighFPSPhysics");
    [PluginService] public static ChatGui Chat { get; private set; } = null!;
    [PluginService] public static CommandManager Commands { get; private set; } = null!;
    [PluginService] public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
}