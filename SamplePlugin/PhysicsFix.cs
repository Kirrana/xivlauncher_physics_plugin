using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Utility.Signatures;

namespace HighFpsPhysicsPlugin;

internal class PhysicsFix : IDisposable
{
    //Used to inject the "every 2nd frame variable"
    private AsmHook? oncePerFrameHook;

    //Used to inject the "if statement"
    private AsmHook? physFuncHook;

    //Opcode to check the "if"
    private byte[] testOpcode =
    {
        0xF7, 0x05, 0x1E, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF
    };

    //Opcode to jump depending on the check's result
    private byte[] jnzSkip15 =
    {
        0x75, 0x0E
    };

    //Opcode to negate values checked by testOpcode
    private byte[] decVar =
    {
            0xFF, 0x0D, 0xEC, 0xFF, 0xFF, 0xFF,
            0x79, 0x07,
            0x83, 0x05, 0xE3, 0xFF, 0xFF, 0xFF, 0x02,
            0x90, 0x90, 0x90, 0x90,
    };

    private int counterResetAddIndex = 14;

    nint skipHookMemAddr;
    nint countHookMemAddr;

    [Signature("7A 06 0F 84 ?? ?? ?? ?? 0F B6 ?? ?? ?? ?? ?? 48 89 ?? ?? ?? ?? ?? ?? 48", ScanType = ScanType.Text)]
    private IntPtr physicsUpdateIfAddr;

    [Signature("48 ?? ?? e8 ?? ?? ?? ?? e8 ?? ?? ?? ?? 84 ?? 74 ?? e8 ?? ?? ?? ?? 48 ?? ?? e8 ?? ??  ?? ?? e8 ?? ?? ?? ?? 84 ?? 74 ?? e8 ?? ?? ?? ?? 48 ?? ?? e8 ?? ?? ?? ?? e8 ?? ?? ?? ?? 48 ?? ?? 74 ?? e8", ScanType = ScanType.Text)]
    private IntPtr oncePerFrameAddr;

    public PhysicsFix()
    {
        SignatureHelper.Initialise(this);

        Setup();

        if (Service.Settings.EnableOnStartup)
        {
            oncePerFrameHook?.Enable();
            physFuncHook?.Enable();
        }
    }

    public void Dispose()
    {
        oncePerFrameHook?.Dispose();
        physFuncHook?.Dispose();
    }

    private void Setup()
    {
        //Memory used to store assembly code & flipping variable
        skipHookMemAddr = MemoryHelper.Allocate(100);

        //newMemAddr+0x36 is after the physics code & variable, the variable decrement code is here
        countHookMemAddr = new IntPtr(skipHookMemAddr.ToInt64() + 0x36); 

        //Return location after doing "if part" of injected code, if not skipping physics
        var returnNormalAddr = new IntPtr(physicsUpdateIfAddr.ToInt64() + 8);
        
        // Return location if skipping physics (it's the target of a jump instruction near returnNormalAddr)  TODO: This is just an offset, maybe scan/calculate from jump instruction instead :)
        var returnSkipAddr = new IntPtr(physicsUpdateIfAddr.ToInt64() + 0x96F);

        //Byte array that will hold opcodes to be put into new memory, stuff below is what it contains.
        var newMemOpcodes = new byte[100]; 

        //Opcode to return (to physics) if not jump
        var jmpReturnNormal = CreateOpcodesToFarJumpToAddress(returnNormalAddr);

        //Opcode to return (to physics) if jump, this is where jsSkip15 ends up
        var jmpReturnSkip = CreateOpcodesToFarJumpToAddress(returnSkipAddr);

        //Opcode to return back to the main update loop
        var jmpReturnOncePerFrame = CreateOpcodesToFarJumpToAddress(new IntPtr(oncePerFrameAddr.ToInt64() + 0x7));

        byte framesPerUpdate = (byte)(Service.Settings.FramesPerPhysicsUpdate);
        // add the correct amount to reset our counter
        decVar[counterResetAddIndex] = framesPerUpdate;
        // counter will count from (n-1) to 0
        byte[] counter = new byte[] { (byte)(framesPerUpdate - 0x1), 0x00, 0x00, 0x00 };

        //Begin filling newMemOpcodes, this is slightly janky looking
        var hookOpCodes = new List<byte>()

        .Concat(testOpcode)             // 0x00     TEST [rip+0x1E], 0xFFFFFFFF     # test (counter & 0xFFFFFFFF)
        .Concat(jnzSkip15)              // 0x0A     JNZ  0x0E                       # jump to skip if counter != 0
        .Concat(jmpReturnNormal)        // 0x0C     JMP returnNormalAddr            # return to original instruction+1
        .Concat(jmpReturnSkip)          // 0x1A     JMP returnSkipAddr              # return after physics instructions

        .Concat(counter)                // 0x28     01 00 00 00                     # counter

        // fill with blank up through 0x35
        .Concat(new byte[0x35 - 0x2B /* target address - end of counter dword */ ])

        .Concat(decVar)                 // 0x36     DEC [rip-0x13]                  # dec counter
                                        // 0x3C     JNS [rip+0x07]                  # jns to 0x45
                                        // 0x3E     ADD [rip-0x1C], 0x02            # counter += 2 (reset to 0x00000001)
                                        // 0x45     4x NOP                          # filler

        .Concat(jmpReturnOncePerFrame)  // 0x49     JMP jmpReturnOncePerFrame       # return after once-per-frame hook

        ;                               // 0x63                                     # end of memory block
        

        // Copy into memory
        byte[] hookOpCodesArray = hookOpCodes.ToArray();
        Buffer.BlockCopy(hookOpCodesArray, 0, newMemOpcodes, 0, hookOpCodesArray.Length);

        MemoryHelper.ChangePermission(skipHookMemAddr, 100, MemoryProtection.ExecuteReadWrite); //Write new memory opcodes to the new memory 
        MemoryHelper.WriteRaw(skipHookMemAddr, newMemOpcodes);

        var physicsJumpOpcodes = CreateOpcodesToFarJumpToAddress(skipHookMemAddr); //Create opcodes that just jump to newMemAddr where new stuff ("if" part) is
        physFuncHook = new AsmHook(physicsUpdateIfAddr, physicsJumpOpcodes, "asdf", AsmHookBehaviour.ExecuteAfter);


        var oncePerFrameJumpOpcodes = CreateOpcodesToFarJumpToAddress(countHookMemAddr); //Create opcodes that just jump to newMemAddr+0x36, where new stuff ("flip" part) is
        oncePerFrameHook = new AsmHook(oncePerFrameAddr, oncePerFrameJumpOpcodes, "zxcv", AsmHookBehaviour.ExecuteAfter);
    }

    private byte[] CreateOpcodesToFarJumpToAddress(IntPtr targetAddr)
    {
        var addrBytes = BitConverter.GetBytes(targetAddr.ToInt64());

        var jumpOpcodes = new byte[]
        {
            0xFF, 0x25, 0x00, 0x00, 0x00, 0x00, // 0xff25 for long jump, 0x00000000 for relative location of jump address (i.e. the next QWord)
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 //these 00s are for target address 
        }; 

        for (var i = 6; i < 14; i++)
        {
            jumpOpcodes[i] = addrBytes[i - 6];
        }

        return jumpOpcodes;
    }

    public void UpdateFramesToSkip()
    {
        oncePerFrameHook?.Disable();
        physFuncHook?.Disable();

        // change our add index to add the new amount next time counter hits 0
        decVar[counterResetAddIndex] = (byte)(Service.Settings.FramesPerPhysicsUpdate);
        MemoryHelper.WriteRaw(countHookMemAddr, decVar);

        oncePerFrameHook?.Enable();
        physFuncHook?.Enable();
    }

    public void DebugMessage()
    {
        var hookCode = MemoryHelper.ReadRaw(skipHookMemAddr, 100);
        Chat.Print($"[DEBUG]\n{BitConverter.ToString(hookCode).Replace("-", " ")}");
    }

    public void CounterDebugMessage()
    {
        var counter = MemoryHelper.ReadRaw(new IntPtr(skipHookMemAddr.ToInt64() + 0x28), 4);
        Chat.Print($"[DEBUG] Counter: {BitConverter.ToString(counter).Replace("-", " ")}");
    }

    public void Enable()
    {
        Chat.Print("Enabling Physics Modification");
        oncePerFrameHook?.Enable();
        physFuncHook?.Enable();
    }

    public void Disable()
    {
        Chat.Print("Disabling Physics Modification");
        oncePerFrameHook?.Disable();
        physFuncHook?.Disable();
    }

    public bool GetStatus()
    {
        return (oncePerFrameHook?.IsEnabled ?? false) &&
               (physFuncHook?.IsEnabled ?? false);
    }

}