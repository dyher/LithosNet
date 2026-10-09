using System;
namespace LithosNet.V4.VM;
// 1:1 taedlar/neolith src/interpret.c + docs/internals/int64-design.md
// F_NUMBER opcode 8 = 4 bytes, F_LONG opcode 10 = 8 bytes

public sealed class InterpreterV4 {
    public const int STACK_SIZE = 4096;
    public SValueS[] Stack = new SValueS[STACK_SIZE];
    public int Sp = -1;
    public byte[] Program = new byte[0];
    public int Pc = 0;

    public InterpreterV4(){ for(int i=0;i<STACK_SIZE;i++) Stack[i]=SValueS.Invalid; }

    void PushNumber(long n){ Stack[++Sp]=SValueS.FromNumber(n); Console.WriteLine($"[push_number] {n} (int64_t)"); }

    static int LoadInt(byte[] prog, ref int pc){
        int v = prog[pc] | (prog[pc+1]<<8) | (prog[pc+2]<<16) | (prog[pc+3]<<24);
        pc+=4; return v;
    }
    static long LoadLong(byte[] prog, ref int pc){
        long lo = (uint)(prog[pc] | (prog[pc+1]<<8) | (prog[pc+2]<<16) | (prog[pc+3]<<24));
        long hi = (uint)(prog[pc+4] | (prog[pc+5]<<8) | (prog[pc+6]<<16) | (prog[pc+7]<<24));
        pc+=8; return lo | (hi<<32); // LOAD8 per lib/port/byte_code.h
    }

    public void EvalInstruction(byte[] prog){
        Program=prog; Pc=0;
        while(Pc < prog.Length){
            byte instr = prog[Pc++];
            switch(instr){
                case 8: // F_NUMBER opcode 8 per int64-design.md L49
                    { int i = LoadInt(prog, ref Pc); PushNumber(i); break; }
                case 10: // F_LONG opcode 10 per int64-design.md L55
                    { long lv = LoadLong(prog, ref Pc); PushNumber(lv); break; }
                case 0x12: // F_BYTE placeholder
                    { byte b = prog[Pc++]; PushNumber(b); break; }
                default:
                    Console.WriteLine($"[interp] unknown opcode {instr} at pc {Pc-1}"); return;
            }
        }
    }
}
