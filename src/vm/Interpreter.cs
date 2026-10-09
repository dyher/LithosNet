
using System;
namespace LithosNet.V4.VM;
// 1:1 from taedlar/neolith src/interpret.c L3785... true bytecode
// LOAD_INT 4 bytes, LOAD_LONG 8 bytes little endian per int64-design.md

public sealed class InterpreterV4 {
    public const int STACK_SIZE = 4096;
    public SValueS[] Stack = new SValueS[STACK_SIZE];
    public int Sp = -1;
    public byte[] Program = new byte[0];
    public int Pc = 0;

    public InterpreterV4(){
        for(int i=0;i<STACK_SIZE;i++) Stack[i]=SValueS.Invalid;
    }

    void PushNumber(long n){ Stack[++Sp]=SValueS.FromNumber(n); }
    SValueS Pop(){ return Stack[Sp--]; }

    // true LOAD macros from src/interpret.c
    static int LoadInt(byte[] prog, ref int pc){
        int v = prog[pc] | (prog[pc+1]<<8) | (prog[pc+2]<<16) | (prog[pc+3]<<24);
        pc+=4;
        return v;
    }
    static long LoadLong(byte[] prog, ref int pc){
        long lo = (uint)(prog[pc] | (prog[pc+1]<<8) | (prog[pc+2]<<16) | (prog[pc+3]<<24));
        long hi = (uint)(prog[pc+4] | (prog[pc+5]<<8) | (prog[pc+6]<<16) | (prog[pc+7]<<24));
        pc+=8;
        return lo | (hi<<32);
    }

    public void EvalInstruction(byte[] prog){
        Program=prog; Pc=0;
        while(Pc < prog.Length){
            byte instr = prog[Pc++];
            switch(instr){
                case 0x10: // F_NUMBER placeholder, real opcode from efuns_opcode.h
                    {
                        int i = LoadInt(prog, ref Pc);
                        PushNumber(i);
                        break;
                    }
                case 0x11: // F_LONG true int64_t per file3785 L2494
                    {
                        long lv = LoadLong(prog, ref Pc);
                        PushNumber(lv);
                        Console.WriteLine($"[F_LONG] {lv}");
                        break;
                    }
                case 0x12: // F_BYTE
                    {
                        byte b = prog[Pc++];
                        PushNumber(b);
                        break;
                    }
                case 0x13: // F_NBYTE
                    {
                        byte b = prog[Pc++];
                        PushNumber(-(int)b);
                        break;
                    }
                default:
                    Console.WriteLine($"[interp] unknown opcode {instr:X2} at pc {Pc-1}");
                    return;
            }
        }
    }
}
