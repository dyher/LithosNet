using System;
using System.Collections.Generic;

namespace LithosNet.VM;

public enum SValueType : byte
{
    T_INVALID = 0x0,
    T_LVALUE = 0x1,
    T_NUMBER = 0x2,
    T_STRING = 0x4,
    T_ARRAY = 0x8,
    T_OBJECT = 0x10,
    T_MAPPING = 0x20,
    T_FUNCTION = 0x40,
    T_BUFFER = 0x80,
    T_CLASS = 0x81,
}

[Flags]
public enum SValueSubType : ushort
{
    STRING_MALLOC = 0x1,
    STRING_SHARED = 0x2,
    STRING_CONSTANT = 0x4,
}

public sealed class SValueArray
{
    public List<SValue> Items { get; } = new();
    public int RefCount { get; set; } = 1;
}

public sealed class SValueMapping
{
    public Dictionary<SValue, SValue> Map { get; } = new(SValueComparer.Instance);
    public int RefCount { get; set; } = 1;
}

public sealed class SValueComparer : IEqualityComparer<SValue>
{
    public static readonly SValueComparer Instance = new();
    public bool Equals(SValue x, SValue y) => x.Equals(y);
    public int GetHashCode(SValue obj) => obj.GetHashCode();
}

public sealed class LpcObject
{
    public string Path;
    public Program Prog;
    public Dictionary<string, SValue> Vars = new();
    public int ObjectId;
    private static int _nextId = 1;
    public LpcObject(string path, Program prog)
    {
        Path = path;
        Prog = prog;
        ObjectId = _nextId++;
    }
    public bool TryFindFunction(string name, out int idx, out int addr)
    {
        idx = -1; addr = -1;
        if (Prog?.FunctionTable == null) return false;
        for (int i=0;i<Prog.FunctionTable.Length;i++)
        {
            if (Prog.FunctionTable[i].Name == name)
            {
                idx = i;
                addr = Prog.FunctionTable[i].Address;
                return true;
            }
        }
        return false;
    }
}

public readonly struct SValue : IEquatable<SValue>
{
    public readonly SValueType Type;
    public readonly SValueSubType SubType;
    public readonly long Number;
    public readonly string String;
    public readonly SValueArray Array;
    public readonly SValueMapping Mapping;
    public readonly LpcObject Object;
    public readonly int FunctionIndex;

    private SValue(SValueType type, long number = 0, string str = null, SValueArray arr = null, SValueMapping map = null, LpcObject obj = null, SValueSubType sub = 0, int funcIdx = -1)
    {
        Type = type;
        SubType = sub;
        Number = number;
        String = str;
        Array = arr;
        Mapping = map;
        Object = obj;
        FunctionIndex = funcIdx;
    }

    public static SValue NewNumber(long n) => new(SValueType.T_NUMBER, number: n);
    public static SValue NewString(string s, SValueSubType sub = SValueSubType.STRING_MALLOC) => new(SValueType.T_STRING, str: s, sub: sub);
    public static SValue NewArray(SValueArray arr) => new(SValueType.T_ARRAY, arr: arr);
    public static SValue NewMapping(SValueMapping map) => new(SValueType.T_MAPPING, map: map);
    public static SValue NewFunction(int funcIdx) => new(SValueType.T_FUNCTION, number: funcIdx, funcIdx: funcIdx);
    public static SValue NewObject(LpcObject ob) => new(SValueType.T_OBJECT, obj: ob);
    public static SValue Invalid => new(SValueType.T_INVALID);

    public bool IsNumber => Type == SValueType.T_NUMBER;
    public bool IsString => Type == SValueType.T_STRING;
    public long AsNumber() => Type == SValueType.T_NUMBER ? Number : 0;

    public bool Equals(SValue other)
    {
        if (Type != other.Type) return false;
        return Type switch
        {
            SValueType.T_NUMBER => Number == other.Number,
            SValueType.T_STRING => String == other.String,
            SValueType.T_FUNCTION => FunctionIndex == other.FunctionIndex,
            SValueType.T_OBJECT => Object?.ObjectId == other.Object?.ObjectId,
            _ => false
        };
    }

    public override bool Equals(object obj) => obj is SValue v && Equals(v);
    public override int GetHashCode() => Type switch
    {
        SValueType.T_NUMBER => Number.GetHashCode(),
        SValueType.T_STRING => String?.GetHashCode() ?? 0,
        SValueType.T_FUNCTION => FunctionIndex.GetHashCode(),
        SValueType.T_OBJECT => Object?.ObjectId.GetHashCode() ?? 0,
        _ => (int)Type
    };
    public override string ToString() => Type switch
    {
        SValueType.T_NUMBER => Number.ToString(),
        SValueType.T_STRING => $"\"{String}\"",
        SValueType.T_ARRAY => $"ARRAY({Array?.Items.Count ?? 0})",
        SValueType.T_MAPPING => $"MAPPING({Mapping?.Map.Count ?? 0})",
        SValueType.T_FUNCTION => $"FUNC({FunctionIndex})",
        SValueType.T_OBJECT => $"OBJ({Object?.Path}#{Object?.ObjectId})",
        _ => $"{Type}"
    };
}

public sealed class SValueS
{
    public SValue Value;
    public SValueU U;

    public SValueS()
    {
        Value = SValue.Invalid;
        U = new SValueU();
    }

    public SValueS(long n)
    {
        Value = SValue.NewNumber(n);
        U = new SValueU { Number = n };
    }

    public SValueS(string s)
    {
        Value = SValue.NewString(s);
        U = new SValueU { String = s };
    }

    public SValueType Type => Value.Type;
    public long Number => Value.Number;
    public string String => Value.String;

    public static SValueS Invalid => new SValueS() { Value = SValue.Invalid, U = new SValueU() };
    public static SValueS FromNumber(long n) => new SValueS(n);

    public static implicit operator SValue(SValueS s) => s.Value;
    public static implicit operator SValueS(SValue v) => new SValueS() { Value = v, U = new SValueU { Number = v.Number, String = v.String, Array = v.Array, Mapping = v.Mapping, Object = v.Object } };

    public override string ToString() => Value.ToString();
}

public sealed class SValueU
{
    public long Number;
    public string String;
    public SValueArray Array;
    public SValueMapping Mapping;
    public LpcObject Object;
}

public sealed class ArrayS
{
    public SValueArray Impl { get; } = new SValueArray();
    public int RefCount { get; set; } = 1;
    public List<SValue> Items => Impl.Items;
}

public sealed class MappingS
{
    public SValueMapping Impl { get; } = new SValueMapping();
}
