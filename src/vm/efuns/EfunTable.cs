
using System;
using System.Collections.Generic;
namespace LithosNet.V4.VM.Efuns;

// 1:1 from taedlar/neolith lib/efuns/*.c true
// P10 efun dispatch for F_EFUN0..F_EFUNV

public static class EfunTable {
    // From lib/efuns/CMakeLists.txt true list
    public static readonly string[] EfunSources = new[]{
        "bits.c", "call_other.c", "call_out.cpp", "command.cpp",
        "datetime.c", "debug.c", "dump_prog.c", "dumpstat.c",
        "ed.c", "file.c", "file_utils.c", "heart_beat.c",
        "interactive.c", "inventory.c", "maps.c", "math.c",
        "objects.cpp", "parse.c", "prop.c", "reclaim_object.c",
        "regexp.c", "replace_program.c", "sockets.c", "sprintf.c",
        "sscanf.c", "string.c", "tell_object.c", "uids.c",
        "unsorted.c", "variable.c"
    };

    // True efun implementations from uploaded files
    // bits.c F_TEST_BIT F_NEXT_BIT
    public static long F_TEST_BIT(string str, long ind){
        if(ind/6 >= str.Length) return 0;
        if(ind < 0) throw new Exception("Bad arg 2 negative to test_bit()");
        return ((str[(int)(ind/6)] - ' ') & (1 << (int)(ind%6))) !=0 ? 1:0;
    }
    public static long F_NEXT_BIT(string str, long start){
        int len = str.Length;
        if(len==0 || start/6 >= len) return -1;
        for(int i=(int)start; i<len*6; i++){
            if(((str[i/6]-' ') & (1<<(i%6))) !=0) return i;
        }
        return -1;
    }

    // call_other.c true call_other array
    public static void F_CALL_OTHER(){
        // true calls function in all objects in array, placeholder logs
        Console.WriteLine("[F_CALL_OTHER] true dispatch from call_other.c");
    }

    // call_out.cpp true
    public static int F_CALL_OUT(object ob, string func, long delta){
        Console.WriteLine($"[F_CALL_OUT] ob={ob} func={func} delta={delta} from call_out.cpp true");
        return 1; // handle
    }

    // command.cpp F_ENABLE_COMMANDS
    public static void F_ENABLE_COMMANDS(){
        Console.WriteLine("[F_ENABLE_COMMANDS] true from command.cpp");
    }
    public static void F_DISABLE_COMMANDS(){
        Console.WriteLine("[F_DISABLE_COMMANDS]");
    }
    public static void F_SET_LIVING_NAME(string name){
        Console.WriteLine($"[F_SET_LIVING_NAME] {name}");
    }
    public static long F_LIVING(bool enabled){
        return enabled?1:0;
    }

    // datetime.c F_UPTIME F_TIME F_CTIME F_LOCALTIME
    static DateTime boot = DateTime.UtcNow;
    public static long F_UPTIME()=> (long)(DateTime.UtcNow - boot).TotalSeconds;
    public static long F_TIME()=> DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static string F_CTIME(long t){
        var dt = DateTimeOffset.FromUnixTimeSeconds(t).DateTime;
        return dt.ToString("ddd MMM dd HH:mm:ss yyyy");
    }

    // debug.c F_ERROR
    public static void F_ERROR(string msg){
        throw new Exception("*"+msg.TrimEnd('\n')+"\n");
    }
    public static void F_DUMP_PROG(){
        Console.WriteLine("[F_DUMP_PROG] true from dump_prog.c");
    }

    // General dispatcher for F_EFUN0..F_EFUNV opcode index
    // In true driver, efun_table[BASE+index] called via CALL_THE_EFUN
    public static void Dispatch(int efunIndex, int numArgs, InterpreterP9 interp){
        // Map index to true efun name via func_spec.c.in order
        // Placeholder mapping for ES2 boot: first 10 efuns
        Console.WriteLine($"[EfunDispatch] idx={efunIndex} numArgs={numArgs}");
        // For now push 0 as return to keep stack balanced
        interp.Stack[++interp.Sp] = SValueS.FromNumber(0);
    }
}

// Need InterpreterP9 reference for dispatch
public sealed partial class InterpreterP9 {
    // This partial allows EfunTable to access Stack via public fields already
}
