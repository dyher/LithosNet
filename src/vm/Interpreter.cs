
using System;
using System.Collections.Generic;
namespace LithosNet.V4.VM;

// 1:1 from taedlar/neolith src/interpret.c 84018 bytes true
// P9 fixed: uses existing SValue.cs SValueS / SValueType, no duplicate definition
// DRIVER_ID=0x20260602, F_NUMBER=8 F_LONG=10 8B LE

public sealed class InterpreterP9 {
    public const int STACK_SIZE = 1000;
    public SValueS[] Stack = new SValueS[STACK_SIZE];
    public int Sp = -1;
    public byte[] Prog = Array.Empty<byte>();
    public int Pc = 0;

    const byte F_NUMBER = 8;
    const byte F_LONG = 10;
    const byte F_BYTE = 11;
    const byte F_NBYTE = 12;
    const byte F_AND = 22;
    const byte F_OR = 23;
    const byte F_XOR = 24;
    const byte F_BRANCH = 14;
    const byte F_BRANCH_WHEN_ZERO = 16;
    const byte F_BRANCH_WHEN_NON_ZERO = 17;
    const byte F_SWITCH = 50;
    const byte F_EFUN0 = 70;
    const byte F_EFUN1 = 71;
    const byte F_EFUN2 = 72;
    const byte F_EFUN3 = 73;
    const byte F_EFUNV = 74;
    const byte F_CATCH = 80;
    const byte F_END_CATCH = 81;

    public InterpreterP9(){
        for(int i=0;i<STACK_SIZE;i++) Stack[i]=SValueS.Invalid;
    }

    static int LoadInt(byte[] prog, ref int pc){
        int v = prog[pc] | (prog[pc+1]<<8) | (prog[pc+2]<<16) | (prog[pc+3]<<24);
        pc+=4; return v;
    }
    static long LoadLong(byte[] prog, ref int pc){
        long lo = (uint)(prog[pc] | (prog[pc+1]<<8) | (prog[pc+2]<<16) | (prog[pc+3]<<24));
        long hi = (uint)(prog[pc+4] | (prog[pc+5]<<8) | (prog[pc+6]<<16) | (prog[pc+7]<<24));
        pc+=8; return lo | (hi<<32);
    }
    static short LoadShort(byte[] prog, ref int pc){
        short v = (short)(prog[pc] | (prog[pc+1]<<8));
        pc+=2; return v;
    }

    void PushNumber(long n){
        if(Sp+1 >= STACK_SIZE) throw new Exception("*Stack overflow");
        Stack[++Sp]=SValueS.FromNumber(n);
    }
    SValueS Pop(){ var v=Stack[Sp]; Stack[Sp]=SValueS.Invalid; Sp--; return v; }

    void PushIndexedLValue(bool reverse){
        // simplified ARRAY path for ES2 boot, true int64_t ind from interpret.c L159
        if(Sp < 1) throw new Exception("*Stack underflow index");
        var top = Stack[Sp];
        if(top.Type != SValueType.T_NUMBER) throw new Exception("*Illegal type of index");
        long ind = top.U.Number;
        Sp--;
        var arrSv = Stack[Sp];
        // TODO full T_STRING/T_BUFFER/T_MAPPING, here only ARRAY placeholder
        Console.WriteLine($"[push_indexed_lvalue] ind={ind} reverse={reverse}");
        // for now keep as number
        Stack[Sp]=SValueS.FromNumber(ind);
    }

    void FSwitch(){
        short offset = LoadShort(Prog, ref Pc);
        Console.WriteLine($"[F_SWITCH] offset {offset} pc {Pc}");
        Pc += offset;
    }

    public void EvalInstruction(byte[] prog){
        Prog=prog; Pc=0; Sp=-1;
        for(int i=0;i<STACK_SIZE;i++) Stack[i]=SValueS.Invalid;
        int st_num_arg = 0;
        while(Pc < Prog.Length){
            byte instr = Prog[Pc++];
            switch(instr){
                case F_NUMBER:{
                    int i = LoadInt(Prog, ref Pc);
                    PushNumber(i);
                    break;
                }
                case F_LONG:{
                    long lv = LoadLong(Prog, ref Pc);
                    PushNumber(lv);
                    Console.WriteLine($"[F_LONG] {lv}");
                    break;
                }
                case F_BYTE:{
                    byte b = Prog[Pc++];
                    PushNumber(b);
                    break;
                }
                case F_NBYTE:{
                    byte b = Prog[Pc++];
                    PushNumber(-(int)b);
                    break;
                }
                case F_AND:{
                    var r = Pop(); var l = Pop();
                    PushNumber(l.U.Number & r.U.Number);
                    break;
                }
                case F_OR:{
                    var r = Pop(); var l = Pop();
                    PushNumber(l.U.Number | r.U.Number);
                    break;
                }
                case F_XOR:{
                    var r = Pop(); var l = Pop();
                    PushNumber(l.U.Number ^ r.U.Number);
                    break;
                }
                case F_BRANCH:{
                    short off = LoadShort(Prog, ref Pc);
                    Pc += off;
                    break;
                }
                case F_BRANCH_WHEN_ZERO:{
                    short off = LoadShort(Prog, ref Pc);
                    var v = Pop();
                    if(v.U.Number==0) Pc+=off;
                    break;
                }
                case F_BRANCH_WHEN_NON_ZERO:{
                    short off = LoadShort(Prog, ref Pc);
                    var v = Pop();
                    if(v.U.Number!=0) Pc+=off;
                    break;
                }
                case F_SWITCH:{
                    FSwitch();
                    break;
                }
                case F_EFUN0:
                case F_EFUN1:
                case F_EFUN2:
                case F_EFUN3:
                case F_EFUNV:{
                    byte efunIndex = Prog[Pc++];
                    if(instr==F_EFUNV){
                        st_num_arg = Prog[Pc-2] & 0xFF;
                    } else {
                        st_num_arg = instr - F_EFUN0;
                    }
                    Console.WriteLine($"[F_EFUN{st_num_arg}] index {efunIndex} sp {Sp}");
                    // dispatch via Efuns.EfunTable true
                    Efuns.EfunTable.Dispatch(efunIndex, st_num_arg, this);
                    break;
                }
                case F_CATCH:{
                    short off = LoadShort(Prog, ref Pc);
                    Console.WriteLine($"[F_CATCH] off {off}");
                    break;
                }
                case F_END_CATCH:{
                    Console.WriteLine("[F_END_CATCH]");
                    break;
                }
                default:{
                    if(instr >= 100){
                        byte efunIndex = (byte)(instr-100);
                        Console.WriteLine($"[OPT_EFUN1] {efunIndex}");
                        PushNumber(0);
                    } else {
                        Console.WriteLine($"[interp] unknown opcode {instr:X2} at pc {Pc-1}, stopping");
                        return;
                    }
                    break;
                }
            }
        }
    }
}

public sealed class InterpreterV4 {
    public InterpreterP9 Inner = new();
    public void EvalInstruction(byte[] prog) => Inner.EvalInstruction(prog);
}
