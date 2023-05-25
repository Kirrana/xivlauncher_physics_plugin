using System;
using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

namespace HighFpsPhysicsPlugin;

internal class Physics : IDisposable
{
    private const double FramesPerSecond = 60d;
    private readonly Framework _framework;

    //g_Client::System::Framework::Framework_InstancePointer2
    [Signature("48 8B 05 ?? ?? ?? ?? F3 0F 10 B0 ?? ?? ?? ?? F3 41 0F 5D F2")]
    private readonly IntPtr _frameworkPointer = IntPtr.Zero;

    //Client::Graphics::Physics::BoneSimulator_Update
    [Signature("48 8B C4 48 89 48 08 55 48 81 EC", DetourName = nameof(PhysicsSkip))]
    private readonly Hook<PhysicsSkipDelegate>? _physicsSkipHook = null!;

    private long _currentTick = 0;
    private bool _executePhysics = false;
    private long _expectedFrameTime;
    private long _lastExecutedTick = 0;

    public Physics(Framework framework)
    {
        SignatureHelper.Initialise(this);
        _framework = framework;

        if (Service.Settings.EnableOnStartup)
        {
            _physicsSkipHook?.Enable();
            _framework.Update += Framework_Update;
        }

        RecalculateExpectedFrametime();
    }

    private delegate IntPtr PhysicsSkipDelegate(IntPtr a1, IntPtr a2);

    public void Disable()
    {
        Service.Chat.Print("Disabling Physics Modification");
        _physicsSkipHook?.Disable();
        _framework.Update -= Framework_Update;
    }

    public void Dispose()
    {
        _framework.Update -= Framework_Update;
        _physicsSkipHook?.Dispose();
    }

    public void Enable()
    {
        Service.Chat.Print("Enabling Physics Modification");
        _physicsSkipHook?.Enable();
        _framework.Update += Framework_Update;
    }

    public bool GetStatus()
    {
        return _physicsSkipHook?.IsEnabled ?? false;
    }

    public void RecalculateExpectedFrametime()
    {
        var physicsFrameTime = (long)((1 / (FramesPerSecond)) * TimeSpan.TicksPerSecond);
        _expectedFrameTime = (long)(physicsFrameTime - ((physicsFrameTime / FramesPerSecond) * Service.Settings.PhysicsFrameTolerance));
    }

    private void Framework_Update(Framework framework)
    {
        _currentTick = DateTime.Now.Ticks;
        var frameTickDelta = _currentTick - _lastExecutedTick;

        if (frameTickDelta >= _expectedFrameTime)
        {
            _lastExecutedTick = _currentTick;
            _executePhysics = true;
        }
        else
        {
            _executePhysics = false;
        }
    }

    private IntPtr PhysicsSkip(IntPtr a1, IntPtr a2)
    {
        if (_executePhysics)
        {
            return _physicsSkipHook!.Original(a1, a2);
        }
        else
        {
            return _frameworkPointer;
        }
    }
}