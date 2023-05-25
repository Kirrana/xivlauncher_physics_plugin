using System;
using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

namespace HighFpsPhysicsPlugin;

internal class Physics : IDisposable
{
    private readonly Framework _framework;

    //g_Client::System::Framework::Framework_InstancePointer2
    [Signature("48 8B 05 ?? ?? ?? ?? F3 0F 10 B0 ?? ?? ?? ?? F3 41 0F 5D F2")]
    private readonly IntPtr _frameworkPointer = IntPtr.Zero;

    //Client::Graphics::Physics::BoneSimulator_Update
    [Signature("48 8B C4 48 89 48 08 55 48 81 EC", DetourName = nameof(PhysicsSkip))]
    private readonly Hook<PhysicsSkipDelegate>? _physicsSkipHook = null!;

    private TimeSlice _currentSlice;
    private bool _executePhysics = false;
    private long _expectedFrameTime;

    public Physics(Framework framework)
    {
        SignatureHelper.Initialise(this);
        _framework = framework;

        _currentSlice = new(DateTime.Now.Ticks, _expectedFrameTime);

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
        _expectedFrameTime = (long)((1 / (Service.Settings.TargetFPS)) * TimeSpan.TicksPerSecond);
    }

    private void Framework_Update(Framework framework)
    {
        var currentTick = DateTime.Now.Ticks;
        while (currentTick > _currentSlice!.EndTick)
        {
            _currentSlice.Update(_expectedFrameTime);
        }

        _executePhysics = _currentSlice.ShouldRunPhysics();
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

    private record TimeSlice
    {
        public TimeSlice(long startTick, long sliceLength)
        {
            _startTick = startTick;
            _sliceLength = sliceLength;
        }

        private long _sliceLength;
        private long _startTick;
        private bool _ranPhysics;
        public long EndTick => _startTick + _sliceLength;

        public void Update(long sliceLength)
        {
            _startTick = EndTick + 1;
            _sliceLength = sliceLength;
            _ranPhysics = false;
        }

        public bool ShouldRunPhysics()
        {
            if (_ranPhysics) return false;
            _ranPhysics = true;
            return true;
        }
    }
}