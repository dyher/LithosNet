
using System;
namespace LithosNet.V4.VM.Efuns;
public static class EfunTable {
    public static readonly string[] EfunSources = new[]{
        "bits.c","call_other.c","call_out.cpp","command.cpp","datetime.c","debug.c","dump_prog.c",
        "file.c","file_utils.c","heart_beat.c","interactive.c","variable.c","tell_object.c","uids.c","string.c","sprintf.c","sscanf.c"
    };
    public static long F_TEST_BIT(string str, long ind){
        if(ind/6 >= str.Length) return 0;
        if(ind < 0) throw new Exception("Bad arg 2 negative to test_bit()");
        return ((str[(int)(ind/6)] - ' ') & (1 << (int)(ind%6))) !=0 ? 1:0;
    }
    public static long F_NEXT_BIT(string str, long start){
        for(int i=(int)start; i<str.Length*6; i++){
            if(((str[i/6]-' ') & (1<<(i%6))) !=0) return i;
        }
        return -1;
    }
    public static long F_UPTIME()=> (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
    public static long F_TIME()=> DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static string F_CTIME(long t)=> DateTimeOffset.FromUnixTimeSeconds(t).ToString("ddd MMM dd HH:mm:ss yyyy");
    public static void F_ERROR(string msg){ throw new Exception("*"+msg.TrimEnd('\n')+"\n"); }

    public static void Dispatch(int efunIndex, int numArgs, InterpreterP9 interp){
        Console.WriteLine($"[EfunDispatch] idx={efunIndex} numArgs={numArgs} true from bits.c/datetime.c/file.c etc.");
        // push 0 as return for now, real impl from uploaded file*.c
        if(interp.Sp+1 < InterpreterP9.STACK_SIZE) interp.Stack[++interp.Sp]=SValueS.FromNumber(0);
    }
}
