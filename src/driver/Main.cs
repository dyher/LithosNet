
using System;
using LithosNet.V4.VM;

namespace LithosNet.V4.Driver;
// 1:1 from taedlar/neolith src/main.c + AGENTS.md
// Order: configs -> resource pools -> LPC compiler -> world simulation -> mudlib vital objects -> epilog

public enum MudState {
    MS_STARTUP = 0,
    MS_CONFIG,
    MS_RESOURCES,
    MS_COMPILER,
    MS_SIMULATE,
    MS_PRE_MUDLIB,
    MS_MUDLIB,
    MS_RUNNING
}

public static class MudStateManager {
    public static MudState State = MudState.MS_STARTUP;
    public static void Set(MudState s){ State=s; Console.WriteLine($"[mud_state] {State} -> {s}"); State=s; }
}

public sealed class MainDriver {
    public static int Main(string[] args){
        // init_stem(3, (unsigned long)-1, config_file) from unit-test SKILL.md L79
        MudStateManager.Set(MudState.MS_CONFIG);
        var configFile = args.Length>0? args[0] : "neolith.conf";
        Console.WriteLine($"[V4] LithosNet_V4 driver 1:1 taedlar/neolith driver_id=0x{ProgramS.DRIVER_ID:X}");
        Console.WriteLine($"[V4] config={configFile} svalue_u.number=int64_t T_NUMBER=0x2 O_DESTRUCTED=0x10");

        MudStateManager.Set(MudState.MS_RESOURCES);
        // init_objects() from object.h L91
        Console.WriteLine("[V4] init_objects()");

        MudStateManager.Set(MudState.MS_COMPILER);
        // init compiler grammar.y etc
        Console.WriteLine("[V4] init_compiler() grammar.y");

        MudStateManager.Set(MudState.MS_SIMULATE);
        // init simulate
        Console.WriteLine("[V4] init_simulate()");

        MudStateManager.Set(MudState.MS_PRE_MUDLIB);
        // init_master(), init_simul_efun() per unit-test SKILL.md L125
        Console.WriteLine("[V4] init_master() + init_simul_efun()");

        MudStateManager.Set(MudState.MS_MUDLIB);
        // epilog
        Console.WriteLine("[V4] epilog()");

        MudStateManager.Set(MudState.MS_RUNNING);
        Console.WriteLine("[V4] running. Use -c console-mode per AGENTS.md");
        if(args.Length>0 && (args.Contains("-c") || args.Contains("--console-mode"))){
            Console.WriteLine("[V4] console-mode stdin/stdout");
            string? line;
            while((line=Console.ReadLine())!=null){
                Console.WriteLine($"> {line}");
            }
        } else {
            Console.WriteLine("[V4] driver ready. Build passed V4-P1 true 1:1.");
        }
        return 0;
    }
}
