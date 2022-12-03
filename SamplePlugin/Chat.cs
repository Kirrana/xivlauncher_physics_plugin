using Dalamud.Game.Text.SeStringHandling;

namespace HighFpsPhysicsPlugin;

internal static class Chat
{
    public static void Print(string message)
    {
        var stringBuilder = new SeStringBuilder();
        stringBuilder.AddUiForeground(45);
        stringBuilder.AddText($"[HighFPSPhysics] ");
        stringBuilder.AddUiForegroundOff();
        stringBuilder.AddText(message);

        Service.Chat.Print(stringBuilder.BuiltString);
    }
}