using ImGuiNET;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace HighFpsPhysicsPlugin;

internal class ConfigurationWindow : Window
{
    private static readonly Vector4 Orange = new(1.0f, 165.0f / 255.0f, 0.0f, 1.0f);
    private static readonly Vector4 Green = new(0.0f, 1.0f, 0.0f, 1.0f);
    private static readonly Vector4 Red = new(1.0f, 0.0f, 0.0f, 1.0f);

    public ConfigurationWindow() : base("HighFPSPhysics - Configuration")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(275, 200),
            MaximumSize = new Vector2(275,200)
        };

        Flags |= ImGuiWindowFlags.NoResize;
    }

    public override void Draw()
    {
        ImGui.TextColored(Orange, "Warning! This plugin may cause crashing");
        ImGui.TextColored(Orange, "If you experience issues, remove the plugin");

        ImGuiHelpers.ScaledDummy(15.0f);

        if (ImGui.Checkbox("Enable On Startup", ref Service.Settings.EnableOnStartup))
        {
            Service.Settings.Save();
        }

        ImGuiHelpers.ScaledDummy(15.0f);

        ImGui.Text("High FPS Fix is");
        ImGui.SameLine();

        if (Service.PhysicsModification.GetStatus())
        {
            ImGui.TextColored(Green, "Enabled");
        }
        else
        {
            ImGui.TextColored(Red, "Disabled");
        }

        if (Service.PhysicsModification.GetStatus())
        {
            if (ImGui.Button("Disable", ImGuiHelpers.ScaledVector2(75.0f, 23.0f)))
            {
                Service.PhysicsModification.Disable();
            }
        }
        else
        {
            if (ImGui.Button("Enable", ImGuiHelpers.ScaledVector2(75.0f, 23.0f)))
            {
                Service.PhysicsModification.Enable();
            }
        }
    }
}