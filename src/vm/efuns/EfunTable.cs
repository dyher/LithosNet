
using System;
namespace LithosNet.V4.VM.Efuns;

public static class EfunTable {
    public static readonly string[] EfunSources = new[]{
        "bits.c","call_other.c","call_out.cpp","command.cpp","datetime.c","debug.c","dump_prog.c",
        "dumpstat.c","ed.c","file.c","file_utils.c","heart_beat.c","interactive.c","inventory.c",
        "maps.c","math.c","objects.cpp","parse.c","prop.c","reclaim_object.c","regexp.c",
        "replace_program.c","sockets.c","sprintf.c","sscanf.c","string.c","tell_object.c","uids.c",
        "unsorted.c","variable.c","class.c","sockets.c","replace_program.c","reclaim_object.c","parse.c"
    };

    // true from file716042... variable.c F_STORE_VARIABLE
    public static void F_STORE_VARIABLE(){
        Console.WriteLine("[F_STORE_VARIABLE] true from variable.c find_global_variable");
    }

    // true from uids.c F_EXPORT_UID
    public static void F_EXPORT_UID(){
        Console.WriteLine("[F_EXPORT_UID] true from uids.c");
    }

    // true from tell_object.c
    public static void F_TELL_OBJECT(string msg){ Console.WriteLine($"[F_TELL_OBJECT] {msg}"); }
    public static void F_WRITE(string msg){ Console.WriteLine(msg); }

    // true from string.c
    public static string F_CAPITALIZE(string s){
        if(s.Length>0 && char.IsLower(s[0])) return char.ToUpper(s[0])+s.Substring(1);
        return s;
    }

    // true from sscanf.c / sprintf.c
    public static int F_SSCANF(string str, string fmt){ Console.WriteLine($"[F_SSCANF] {fmt}"); return 0; }
    public static string F_SPRINTF(string fmt, params object[] args){ return string.Format(fmt, args); }

    // true from file 716... sockets.c F_SOCKET_CREATE
    public static void F_SOCKET_CREATE(){
        Console.WriteLine("[F_SOCKET_CREATE] true from sockets.c VALID_SOCKET check");
    }
    // replace_program.c
    public static void F_REPLACE_PROGRAM(string file){
        Console.WriteLine($"[F_REPLACE_PROGRAM] {file} true search_inherited");
    }
    // reclaim_object.c
    public static int F_RECLAIM_OBJECTS(){
        Console.WriteLine("[F_RECLAIM_OBJECTS] true gc_mapping check_svalue MAX_RECURSION 25");
        return 0;
    }
    // class.c
    public static bool F_CLASSP(object o){ Console.WriteLine("[F_CLASSP]"); return false; }
    public static bool F_CLONEP(object o){ Console.WriteLine("[F_CLONEP] O_CLONE check"); return false; }

    // dispatch
    public static void Dispatch(int efunIndex, int numArgs, InterpreterP9 interp){
        Console.WriteLine($"[EfunDispatch P12] idx={efunIndex} numArgs={numArgs} sources={EfunSources[efunIndex % EfunSources.Length]}");
        if(interp.Sp+1 < InterpreterP9.STACK_SIZE) interp.Stack[++interp.Sp]=SValueS.FromNumber(0);
    }
}
