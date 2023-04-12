using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

namespace HighFpsPhysicsPlugin;

internal class Physics : IDisposable
{
    //g_Client::System::Framework::Framework_InstancePointer2
    [Signature("48 8B 05 ?? ?? ?? ?? F3 0F 10 B0 ?? ?? ?? ?? F3 41 0F 5D F2")]
    private readonly IntPtr frameworkPointer = IntPtr.Zero;

    //Client::Graphics::Physics::BoneSimulator_Update
    [Signature("48 8B C4 48 89 48 08 55 48 81 EC", DetourName = nameof(PhysicsSkip))]
    private readonly Hook<PhysicsSkipDelegate>? physicsSkipHook = null!;
    private delegate IntPtr PhysicsSkipDelegate(IntPtr a1, IntPtr a2);

    private int callsSinceLastSkip;

    public Physics()
    {
        SignatureHelper.Initialise(this);

        if (Service.Settings.EnableOnStartup)
        {
            physicsSkipHook?.Enable();
        }
    }

    public void Dispose()
    {
        physicsSkipHook?.Dispose();
    }

    private IntPtr PhysicsSkip(IntPtr a1, IntPtr a2)
    {
        callsSinceLastSkip += 1;

        if (callsSinceLastSkip > Service.Settings.FramesPerPhysicsUpdate)
        {
            callsSinceLastSkip = 0;
            return physicsSkipHook!.Original(a1, a2);
        }
        else
        {
            return frameworkPointer;
        }
    }

    public void Enable()
    {
        Service.Chat.Print($"Enabling Physics Modification.");
        physicsSkipHook?.Enable();
    }

    public void Disable()
    {
        Service.Chat.Print("Disabling Physics Modification");
        physicsSkipHook?.Disable();
    }

    public bool GetStatus()
    {
        return physicsSkipHook?.IsEnabled ?? false;
    }
}