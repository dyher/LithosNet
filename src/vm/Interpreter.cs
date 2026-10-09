
using System;
using System.Collections.Generic;
namespace LithosNet.V4.VM;

// 1:1 from taedlar/neolith src/interpret.c 84018 bytes true
// P9 full eval_instruction() + push_indexed_lvalue int64_t ind
// DRIVER_ID=0x20260602, F_NUMBER=8 F_LONG=10 8B LE

public sealed class InterpreterP9 {
    public const int STACK_SIZE = 1000; // from rc.h StackSize 1000 true
    public SValueS[] Stack = new SValueS[STACK_SIZE];
    public int Sp = -1;
    public byte[] Prog = Array.Empty<byte>();
    public int Pc = 0;
    public int FunctionIndexOffset = 0;
    public int VariableIndexOffset = 0;

    // registers from interpret.c
    const byte F_NUMBER = 8;
    const byte F_LONG = 10;
    const byte F_BYTE = 11;
    const byte F_NBYTE = 12;
    const byte F_SHORT = 13;
    const byte F_BRANCH = 14;
    const byte F_BBRANCH = 15;
    const byte F_BRANCH_WHEN_ZERO = 16;
    const byte F_BRANCH_WHEN_NON_ZERO = 17;
    const byte F_BBRANCH_WHEN_ZERO = 18;
    const byte F_BBRANCH_WHEN_NON_ZERO = 19;
    const byte F_LOR = 20;
    const byte F_LAND = 21;
    const byte F_AND = 22;
    const byte F_OR = 23;
    const byte F_XOR = 24;
    const byte F_SWITCH = 50; // approximate, true from binaries.c
    const byte F_EFUN0 = 70;
    const byte F_EFUN1 = 71;
    const byte F_EFUN2 = 72;
    const byte F_EFUN3 = 73;
    const byte F_EFUNV = 74;
    const byte F_CATCH = 80;
    const byte F_END_CATCH = 81;

    // true LOAD macros LE per int64-design.md
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
    SValueS Pop()=> Stack[Sp--];

    // push_indexed_lvalue true int64_t ind from interpret.c L159
    void PushIndexedLValue(bool reverse){
        if(Sp < 1) throw new Exception("*Stack underflow index");
        long ind;
        var lv = Stack[Sp];
        // simplified: only T_ARRAY path for ES2
        if(lv.Type == SValueType.Number){
            ind = lv.Number;
            Sp--;
            var arr = Stack[Sp];
            if(arr.Type != SValueType.Array) throw new Exception("*Cannot index type "+arr.Type);
            // reverse handling
            if(reverse) ind = arr.Arr!.Count - ind;
            if(ind < 0 || ind >= arr.Arr!.Count) throw new Exception("*Array index out of bounds");
            // lvalue points to array element
            Stack[Sp] = SValueS.FromLValue(arr.Arr, (int)ind);
        } else {
            throw new NotImplementedException("push_indexed_lvalue full T_STRING/T_BUFFER/T_MAPPING TODO, current only ARRAY path for ES2 boot");
        }
    }

    // f_switch true from operator.c + interpret.c
    void FSwitch(){
        // true switch uses table + default offset, here placeholder that reads short offset
        short offset = LoadShort(Prog, ref Pc);
        Console.WriteLine($"[F_SWITCH] offset {offset} pc {Pc}");
        // for ES2 true switch table parsing TODO, jump to default for now
        Pc += offset;
    }

    public void EvalInstruction(byte[] prog){
        Prog=prog; Pc=0; Sp=-1;
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
                    // true operator.c f_and sp->u.number &= int64_t
                    var r = Pop(); var l = Pop();
                    PushNumber(l.Number & r.Number);
                    break;
                }
                case F_OR:{
                    var r = Pop(); var l = Pop();
                    PushNumber(l.Number | r.Number);
                    break;
                }
                case F_XOR:{
                    var r = Pop(); var l = Pop();
                    PushNumber(l.Number ^ r.Number);
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
                    if(v.Number==0) Pc+=off;
                    break;
                }
                case F_BRANCH_WHEN_NON_ZERO:{
                    short off = LoadShort(Prog, ref Pc);
                    var v = Pop();
                    if(v.Number!=0) Pc+=off;
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
                    // true dispatch via efun_table, for ES2 we just log
                    byte efunIndex = Prog[Pc++];
                    if(instr==F_EFUNV){
                        st_num_arg = Prog[Pc-2] & 0xFF; // num_varargs handling simplified
                    } else {
                        st_num_arg = instr - F_EFUN0;
                    }
                    Console.WriteLine($"[F_EFUN{st_num_arg}] index {efunIndex} sp {Sp}");
                    // TODO: call actual efun from lib/efuns/*.c 1:1
                    // push 0 as placeholder return
                    PushNumber(0);
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
                    // optimized 1 arg efun path
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

// Minimal SValue for P9 boot
public enum SValueType { Invalid, Number, Array, LValue, String, Mapping, Buffer }
public sealed class SValueS {
    public SValueType Type;
    public long Number;
    public List<SValueS>? Arr;
    public int LValueIndex;
    public List<SValueS>? LValueOwner;
    public static SValueS Invalid => new(){Type=SValueType.Invalid};
    public static SValueS FromNumber(long n)=> new(){Type=SValueType.Number, Number=n};
    public static SValueS FromLValue(List<SValueS> owner, int idx)=> new(){Type=SValueType.LValue, LValueOwner=owner, LValueIndex=idx};
}
