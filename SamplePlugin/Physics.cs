using System;
using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using Dalamud.Plugin.Services;

namespace HighFpsPhysicsPlugin;

internal class Physics : IDisposable
{
    private readonly IFramework _framework;
    //Client::Graphics::Physics::BoneSimulator_Update
    [Signature("40 55 53 56 41 57 48 8D AC 24 ?? ?? ?? ?? 48 81 EC ?? ?? ?? ?? 44 0F 29 94 24", DetourName = nameof(PhysicsSkip))]
    private readonly Hook<PhysicsSkipDelegate>? _physicsSkipHook = null!;

    private TimeSlice _currentSlice;
    private bool _executePhysics = false;
    private long _expectedFrameTime;

    public Physics(IFramework framework)
    {
        Service.GameInteropProvider.InitializeFromAttributes(this);
        _framework = framework;

        _currentSlice = new(DateTime.Now.Ticks, _expectedFrameTime);

        if (Service.Settings.EnableOnStartup)
        {
            _physicsSkipHook?.Enable();
            _framework.Update += Framework_Update;
        }

        RecalculateExpectedFrametime();
    }

    private delegate void PhysicsSkipDelegate(nint a1, nint a2);
    
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

    private void Framework_Update(IFramework framework)
    {
        var currentTick = DateTime.Now.Ticks;
        while (currentTick > _currentSlice!.EndTick)
        {
            _currentSlice.Update(_expectedFrameTime);
        }

        _executePhysics = _currentSlice.ShouldRunPhysics();
    }

    private void PhysicsSkip(nint a1, nint a2)
    {
        if (_executePhysics)
        {
            _physicsSkipHook!.Original(a1, a2);
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