namespace LithosNet.V4.Compiler;
using System.Collections.Generic;
using System;
public static class Opcodes {
    public const byte F_NUMBER = 8;
    public const byte F_LONG = 10;
    public const byte F_AND = 20;
    public const byte F_OR = 21;
    public const byte F_XOR = 22;
}
public sealed class CompilerContext {
    public List<byte> Code = new();
    void EmitByte(byte b) => Code.Add(b);
    void EmitInt(int v){
        Code.Add((byte)(v & 0xFF));
        Code.Add((byte)((v>>8) & 0xFF));
        Code.Add((byte)((v>>16) & 0xFF));
        Code.Add((byte)((v>>24) & 0xFF));
    }
    void EmitLong(long v){
        ulong uv = (ulong)v;
        for(int i=0;i<8;i++) Code.Add((byte)((uv>>(i*8)) & 0xFF));
    }
    public void EmitNumber(long number){
        if(number >= int.MinValue && number <= int.MaxValue){
            EmitByte(Opcodes.F_NUMBER); EmitInt((int)number);
        } else {
            EmitByte(Opcodes.F_LONG); EmitLong(number);
        }
    }
    public void EmitAnd(){ EmitByte(Opcodes.F_AND); }
    public void EmitOr(){ EmitByte(Opcodes.F_OR); }
    public void EmitXor(){ EmitByte(Opcodes.F_XOR); }
    public byte[] ToProgramBytes() => Code.ToArray();
}
