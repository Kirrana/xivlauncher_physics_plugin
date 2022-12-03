using System;
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
    private byte[] testOpcode = {0xF7, 0x05, 0x1E, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF};

    //Opcode to jump depending on the check's result
    private byte[] jsSkip15 = {0x78, 0x0E}; 

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
        var newMemAddr = MemoryHelper.Allocate(100);

        //newMemAddr+0x35 is after the physics code & variable, the variable negation(flip on/off) code is her
        var newMem2ndAddr = new IntPtr(newMemAddr.ToInt64() + 0x35); 

        //Return location after doing "if part" of injected code, if not skipping physics
        var returnNormalAddr = new IntPtr(physicsUpdateIfAddr.ToInt64() + 8);
        
        // Return location if skipping physics (it's the target of a jump instruction near returnNormalAddr)  TODO: This is just an offset, maybe scan instead :)
        var returnSkipAddr = new IntPtr(physicsUpdateIfAddr.ToInt64() + 0x93F); 


        //Byte array that will hold opcodes to be put into new memory, stuff below is what it contains.
        var newMemOpcodes = new byte[100]; 

        //Opcode to return (to physics) if not jump
        var jmpReturnNormal = CreateOpcodesToFarJumpToAdress(returnNormalAddr); 

        //Opcode to return (to physics) if jump, this is where jsSkip15 ends up
        var jmpReturnSkip = CreateOpcodesToFarJumpToAdress(returnSkipAddr); 

        //Opcode to negate values checked by testOpcode
        byte[] negVar = {0xF7, 0x1D, 0xED, 0xFF, 0xFF, 0xFF, 0x90, 0x90, 0x90, 0x90 }; 

        var jmpReturnOncePerFrame = CreateOpcodesToFarJumpToAdress(new IntPtr(oncePerFrameAddr.ToInt64() + 0x7)); //Opcode to return back to the main update loop

        //Begin filling newMemOpcodes, this is slightly janky looking
        var i = 0;
        Buffer.BlockCopy(testOpcode, 0, newMemOpcodes, i, testOpcode.Length);
        i += testOpcode.Length;
        Buffer.BlockCopy(jsSkip15, 0, newMemOpcodes, i, jsSkip15.Length);
        i += jsSkip15.Length;
        Buffer.BlockCopy(jmpReturnNormal, 0, newMemOpcodes, i, jmpReturnNormal.Length);
        i += jmpReturnNormal.Length;
        Buffer.BlockCopy(jmpReturnSkip, 0, newMemOpcodes, i, jmpReturnSkip.Length);
        i += jmpReturnSkip.Length;
        newMemOpcodes[i++] = 0xFF; //These first 4 bytes are used by the test/neg opcodes
        newMemOpcodes[i++] = 0xFF;
        newMemOpcodes[i++] = 0xFF;
        newMemOpcodes[i++] = 0xFF; // the remaining ones are just excess that doesn't do anything
        newMemOpcodes[i++] = 0xFF;
        newMemOpcodes[i++] = 0xFF;
        newMemOpcodes[i++] = 0xFF;
        newMemOpcodes[i++] = 0xFF;

        i = 0x35;
        Buffer.BlockCopy(negVar, 0, newMemOpcodes, i, negVar.Length);
        i += negVar.Length;
        Buffer.BlockCopy(jmpReturnOncePerFrame, 0, newMemOpcodes, i, jmpReturnOncePerFrame.Length);

        MemoryHelper.ChangePermission(newMemAddr, 100, MemoryProtection.ExecuteReadWrite); //Write new memory opcodes to the new memory 
        MemoryHelper.WriteRaw(newMemAddr, newMemOpcodes);

        var physicsJumpOpcodes = CreateOpcodesToFarJumpToAdress(newMemAddr); //Create opcodes that just jump to newMemAddr where new stuff ("if" part) is
        physFuncHook = new AsmHook(physicsUpdateIfAddr, physicsJumpOpcodes, "asdf", AsmHookBehaviour.ExecuteAfter);


        var oncePerFrameJumpOpcodes = CreateOpcodesToFarJumpToAdress(newMem2ndAddr); //Create opcodes that just jump to newMemAddr+0x35, where new stuff ("flip" part) is
        oncePerFrameHook = new AsmHook(oncePerFrameAddr, oncePerFrameJumpOpcodes, "zxcv", AsmHookBehaviour.ExecuteAfter);
    }

    private byte[] CreateOpcodesToFarJumpToAdress(IntPtr targetAddr)
    {
        var addrBytes = BitConverter.GetBytes(targetAddr.ToInt64());

        var jumpOpcodes = new byte[]
        {
            0xFF, 0x25, 0x00, 0x00, 0x00, 0x00, //instruction stuff, 0xff25 for long jump, dunno what all these 00s are for :>
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 //these 00s are for target address 
        }; 

        for (var i = 6; i < 14; i++)
        {
            jumpOpcodes[i] = addrBytes[i - 6];
        }

        return jumpOpcodes;
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