using System;
using System.Collections.Generic;


namespace LithosNet.V4.VM;
// 1:1 from taedlar/neolith lib/lpc/types.h + svalue.h
// https://github.com/taedlar/neolith main

// Forward decls from types.h L28-L38
// typedef struct object_s object_t; program_t etc.

public enum SValueType : short {
    T_INVALID = 0x0,
    T_LVALUE = 0x1,
    T_NUMBER = 0x2,  // runtime, not TYPE_NUMBER compile-time
    T_STRING = 0x4,
    T_REAL = 0x80,
    T_ARRAY = 0x8,
    T_OBJECT = 0x10,
    T_MAPPING = 0x20,
    T_FUNCTION = 0x40,
    T_BUFFER = 0x100,
    T_CLASS = 0x200,
    T_LVALUE_BYTE = 0x400,
    T_LVALUE_RANGE = 0x800,
    T_ERROR_HANDLER = 0x1000
}

public enum StringSubtype : short {
    STRING_CONSTANT = 0,               // from types.h L105
    STRING_MALLOC = 0x1,               // STRING_COUNTED
    STRING_SHARED = 0x1 | 0x2          // COUNTED|HASHED
}

// union svalue_u L44-L64
// int64_t number is Neolith extension per L50
public struct SValueU {
    public long Number; // int64_t
    public double Real;
    public string? ConstString;
    public string? SharedString;
    public string? MallocString;
    public ObjectS? Ob;
    public ArrayS? Arr;
    public MappingS? Map;
    public BufferS? Buf;
    public FunPtrS? Fp;
    // lvalue omitted for Phase1
}

// struct svalue_s L70-L74
public sealed class SValueS {
    public SValueType Type;
    public short Subtype;
    public SValueU U;

    public static SValueS Invalid => new SValueS{ Type=SValueType.T_INVALID };
    public bool IsNumber => Type == SValueType.T_NUMBER;
    public bool IsString => Type == SValueType.T_STRING;
    public bool IsObject => Type == SValueType.T_OBJECT;
    public bool IsZero => Type == SValueType.T_NUMBER && U.Number == 0;

    // SVALUE_STRPTR + SVALUE_STRLEN from types.h L117-L150
    public string? StrPtr() {
        if(Type != SValueType.T_STRING) return null;
        if(Subtype == (short)StringSubtype.STRING_MALLOC) return U.MallocString;
        if(Subtype == (short)StringSubtype.STRING_SHARED) return U.SharedString;
        return U.ConstString;
    }
    public int StrLen() {
        var s = StrPtr();
        return s?.Length ?? 0;
    }

    public static SValueS FromNumber(long n) => new SValueS{ Type=SValueType.T_NUMBER, U=new SValueU{ Number=n } };
    public static SValueS FromSharedString(string s) => new SValueS{ Type=SValueType.T_STRING, Subtype=(short)StringSubtype.STRING_SHARED, U=new SValueU{ SharedString=s } };
    public static SValueS FromMallocString(string s) => new SValueS{ Type=SValueType.T_STRING, Subtype=(short)StringSubtype.STRING_MALLOC, U=new SValueU{ MallocString=s } };
    public static SValueS FromObject(ObjectS ob) => new SValueS{ Type=SValueType.T_OBJECT, U=new SValueU{ Ob=ob } };
}

// lpc::svalue_view from svalue.h + types.h L209-L... - borrowing, pointer-sized
public readonly struct SValueView {
    readonly SValueS _sv;
    public SValueView(SValueS sv){ _sv=sv; }
    public static SValueView From(SValueS sv) => new SValueView(sv);
    public bool IsString() => _sv.IsString;
    public bool IsNumber() => _sv.IsNumber;
    public string CStr() => _sv.StrPtr() ?? "";
    public long Number() => _sv.U.Number;
    public int Length() => _sv.StrLen();
    public void SetNumber(long n){ _sv.Type=SValueType.T_NUMBER; _sv.Subtype=0; _sv.U=new SValueU{ Number=n }; }
    public void SetSharedString(string s){ _sv.Type=SValueType.T_STRING; _sv.Subtype=(short)StringSubtype.STRING_SHARED; _sv.U=new SValueU{ SharedString=s }; }
    public void SetMallocString(string s){ _sv.Type=SValueType.T_STRING; _sv.Subtype=(short)StringSubtype.STRING_MALLOC; _sv.U=new SValueU{ MallocString=s }; }
}

// lpc::svalue owning wrapper from svalue.h L550+
public sealed class SValueOwning : IDisposable {
    public SValueS Value = new SValueS();
    public SValueOwning(){}
    public SValueOwning(long n){ Value = SValueS.FromNumber(n); }
    public SValueOwning(string s){ Value = SValueS.FromMallocString(s); }
    public void Dispose(){ /* free_svalue(&Value_, caller) in C */ }
    public SValueView View() => SValueView.From(Value);
}

// Placeholders for refed types
public sealed class ArrayS { public List<SValueS> Items=new(); }
public sealed class MappingS { public Dictionary<SValueS,SValueS> Map=new(); }
public sealed class BufferS { public byte[] Data=new byte[0]; }
public sealed class FunPtrS { }