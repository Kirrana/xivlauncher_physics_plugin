using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace HighFpsPhysicsPlugin;


public sealed class HighFpsPhysicsPlugin : IDalamudPlugin
{
    private const string CommandName = "/physics";

    public HighFpsPhysicsPlugin(DalamudPluginInterface pluginInterface, IFramework framework)
    {
        pluginInterface.Create<Service>();

        Service.Settings = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Service.Settings.Initialize(pluginInterface);

        Service.PhysicsModification = new Physics(framework);

        Service.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Configuration Window\n" +
                          "/physics on - Enable Physics Change\n" +
                          "/physics off - Disable Physics Change\n" +
                          "/physics t - Toggle Physics Change"
        });

        Service.WindowSystem.AddWindow(new ConfigurationWindow());

        Service.PluginInterface.UiBuilder.Draw += Service.WindowSystem.Draw;
        Service.PluginInterface.UiBuilder.OpenConfigUi += () => Service.WindowSystem.Windows[0].IsOpen = true;
    }

    public string Name => "High FPS Physics Fix";

    public void Dispose()
    {
        Service.Commands.RemoveHandler(CommandName);
        Service.WindowSystem.RemoveAllWindows();

        Service.PhysicsModification.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        var configWindow = Service.WindowSystem.Windows[0];

        switch (args)
        {
            case "":
                configWindow.IsOpen = !configWindow.IsOpen;
                break;

            case "on":
                Service.PhysicsModification.Enable();
                break;

            case "off":
                Service.PhysicsModification.Disable();
                break;

            case "t":
                if (Service.PhysicsModification.GetStatus())
                {
                    Service.PhysicsModification.Disable();
                }
                else
                {
                    Service.PhysicsModification.Enable();
                }
                break;
        }
    }
}