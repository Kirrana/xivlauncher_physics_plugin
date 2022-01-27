using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Hooking;
using Dalamud.Game;
using Dalamud.Logging;
using Dalamud.Memory;
using System.Reflection;
using System;

namespace HighFpsPhysicsPlugin
{
    /**
     * This plugin cuts the physics' refresh rate in half by effectively adding a "if(every second frame)" 
     * to the game's physics update. This is achieved through two injections, one which does the if
     * check during the physics update and one which toggles the variable used for the if check.
     */
    public sealed class HighFpsPhysicsPlugin : IDalamudPlugin
    {
        public string Name => "High FPS Physics Fix";

        private const string commandName = "/physics";


        private DalamudPluginInterface PluginInterface { get; init; }
        private CommandManager CommandManager { get; init; }
        private Configuration Configuration { get; init; }
        private PluginUI PluginUi { get; init; }
        [PluginService] private SigScanner sigScanner { get; set; }

        AsmHook oncePerFrameHook; //Used to inject the "every 2nd frame variable"
        AsmHook physFuncHook; //Used to inject the "if statement"

        public HighFpsPhysicsPlugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager)
        {
            this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;


            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);

            // you might normally want to embed resources and load them from the manifest stream
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;

            this.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Toggles the physics change on/off. \nIt's off on boot for crash safety (if it were to crash you wouldn't want it on by default).\n If you disable (not toggle) the plugin you need to reboot the game to make it work again."
            });

            //this.PluginInterface.UiBuilder.Draw += DrawUI;
            //this.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;




            //------------------------------------------------------------------------------------------------------------------------------------------------------------------------
            //--------------------------------------------------------Actual injection stuff below here-------------------------------------------------------------------------------
            //------------------------------------------------------------------------------------------------------------------------------------------------------------------------

            var newMemAddr = MemoryHelper.Allocate(100);                      //Memory used to store assembly code & flipping variable
            IntPtr newMem2ndAddr = new IntPtr(newMemAddr.ToInt64() + 0x35);   //newMemAddr+0x35 is after the physics code & variable, the variable negation(flip on/off) code is here


            /// <summary>
            /// Creates the opcodes for a long jump to a given address. Used to jump to/from new memory.
            /// </summary>
            byte[] createOpcodesToFarJumpToAdress(IntPtr targetAddr) 
            {
                var addrBytes = BitConverter.GetBytes(targetAddr.ToInt64());
                var jumpOpcodes = new byte[] { 0xFF, 0x25, 0x00, 0x00, 0x00, 0x00, //instruction stuff, 0xff25 for long jump, dunno what all these 00s are for :>
                                                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00 }; //these 00s are for target address 
                for (int i = 6; i < 14; i++)
                {
                    jumpOpcodes[i] = addrBytes[i - 6];
                }
                return jumpOpcodes;
            }


            var physicsUpdateIfAddr =
                this.sigScanner.ScanText("7A 06 0F 84 37 09 00 00 0F B6 81 F8 00 00 00 48 89 9C 24 D8 01 00 00");  //Address to appropriate if statement to hijack inside the physics function
            var oncePerFrameAddr =
                this.sigScanner.ScanText("48 8B 05 2B 9B 9C 01 F3 0F 10 88 B8 16 00 00 F3 0F 58 8B 90 58 00 00");  //Address to appropriate place in main update function (which runs once per frame) to hijack
            //PluginLog.Log($"physics if addr: {physicsUpdateIfAddr.ToInt64():X}");
            var returnNormalAddr = new IntPtr(physicsUpdateIfAddr.ToInt64() + 8);   //Return location after doing "if part" of injected code, if not skipping physics
            var returnSkipAddr = new IntPtr(physicsUpdateIfAddr.ToInt64() + 0x93F); // Return location if skipping physics (it's the target of a jump instruction near returnNormalAddr)  TODO: This is just an offset, maybe scan instead :)

            //PluginLog.Log($"once per frame addr: {oncePerFrameAddr.ToInt64():X}");
            //PluginLog.Log($"newMemAddr: {newMemAddr.ToInt64():X}");


            byte[] newMemOpcodes = new byte[100]; //Byte array that will hold opcodes to be put into new memory, stuff below is what it contains.

            byte[] testOpcode = new byte[] { 0xF7, 0x05, 0x1E, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF };  //Opcode to check the "if"
            byte[] jsSkip15 = new byte[] { 0x78, 0x0E };                                                    //Opcode to jump depending on the check's result
            byte[] jmpReturnNormal = createOpcodesToFarJumpToAdress(returnNormalAddr);                      //Opcode to return (to physics) if not jump
            byte[] jmpReturnSkip = createOpcodesToFarJumpToAdress(returnSkipAddr);                          //Opcode to return (to physics) if jump, this is where jsSkip15 ends up

            byte[] negVar = new byte[] { 0xF7, 0x1D, 0xED, 0xFF, 0xFF, 0xFF, 0x90, 0x90, 0x90, 0x90 };      //Opcode to negate values checked by testOpcode
            byte[] jmpReturnOncePerFrame= createOpcodesToFarJumpToAdress(new IntPtr(oncePerFrameAddr.ToInt64()+0x7)); //Opcode to return back to the main update loop

            //Begin filling newMemOpcodes, this is slightly janky looking
            int i = 0;
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
            //End filling newMemOpcodes


            //PluginLog.Log($"CODE BYTES: {string.Join(" ", newMemOpcodes):X}");


            _ = MemoryHelper.ChangePermission(newMemAddr, 100, newPermissions: MemoryProtection.ExecuteReadWrite);      //Write new memory opcodes to the new memory 
            MemoryHelper.WriteRaw(newMemAddr, newMemOpcodes);

            var physicsJumpOpcodes = createOpcodesToFarJumpToAdress(newMemAddr);                                            //Create opcodes that just jump to newMemAddr where new stuff ("if" part) is
            this.physFuncHook = new AsmHook(physicsUpdateIfAddr, physicsJumpOpcodes, "asdf", AsmHookBehaviour.ExecuteAfter);
            //PluginLog.Log($"BIGJUMP ops: {string.Join(" ", bigJumpOpcodes):X

            var oncePerFrameJumpOpcodes = createOpcodesToFarJumpToAdress(newMem2ndAddr);                                    //Create opcodes that just jump to newMemAddr+0x35, where new stuff ("flip" part) is
            this.oncePerFrameHook = new AsmHook(oncePerFrameAddr, oncePerFrameJumpOpcodes, "zxcv", AsmHookBehaviour.ExecuteAfter);
            //PluginLog.Log($"onceperframe ops: {string.Join(" ", oncePerFrameOpcodes):X}");
            //var addrBytes = BitConverter.GetBytes(newMem2ndAddr.ToInt64());
            //PluginLog.Log($"onceperframe ops: {string.Join(" ", oncePerFrameOpcodes):X}");
            //PluginLog.Log($"onceperframe_call_addr: {newMem2ndAddr:X}");

        }

        public void Dispose()
        {
            //this.PluginUi.Dispose();
            this.CommandManager.RemoveHandler(commandName);


            this.oncePerFrameHook.Disable();
            this.physFuncHook.Disable();

            this.oncePerFrameHook.Dispose();
            this.physFuncHook.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            //Toggles hooks on/off when command is ran.
            if(this.oncePerFrameHook.IsEnabled || this.physFuncHook.IsEnabled)
            {
                this.oncePerFrameHook.Disable();
                this.physFuncHook.Disable();
            }
            else
            {
                this.physFuncHook.Enable();
                this.oncePerFrameHook.Enable();
            }
        }

        private void DrawUI()
        {
           // this.PluginUi.Draw();
        }

        private void DrawConfigUI()
        {
           // this.PluginUi.SettingsVisible = true;
        }
    }
}
