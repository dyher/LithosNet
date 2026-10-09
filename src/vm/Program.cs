namespace LithosNet.V4.VM;
// 1:1 from taedlar/neolith lib/lpc/program/program.h + lib/lpc/program/binaries.h
// DRIVER_ID = 0x20260602 true per binaries.h

public static class NameFlags {
    public const ushort NAME_INHERITED = 0x1;
    public const ushort NAME_UNDEFINED = 0x2;
    public const ushort NAME_STRICT_TYPES = 0x4;
    public const ushort NAME_PROTOTYPE = 0x8;
    public const ushort NAME_DEF_BY_INHERIT = 0x10;
    public const ushort NAME_ALIAS = 0x20;
    public const ushort NAME_TRUE_VARARGS = 0x40;
    public const ushort NAME_HIDDEN = 0x0100;
    public const ushort NAME_STATIC = 0x0200;
    public const ushort NAME_NO_MASK = 0x0400;
    public const ushort NAME_PRIVATE = 0x0800;
    public const ushort NAME_PROTECTED = 0x1000;
    public const ushort NAME_PUBLIC = 0x2000;
    public const ushort NAME_VARARGS = 0x4000;
    public const ushort NAME_TYPE_MOD = NAME_HIDDEN | NAME_STATIC | NAME_NO_MASK | NAME_PRIVATE | NAME_PROTECTED | NAME_PUBLIC | NAME_VARARGS;
    public const ushort NAME_MASK = NAME_UNDEFINED | NAME_STRICT_TYPES | NAME_PROTOTYPE | NAME_TRUE_VARARGS | NAME_TYPE_MOD;
    public const ushort NAME_NO_CODE = NAME_UNDEFINED | NAME_ALIAS | NAME_PROTOTYPE;
}

public static class TypeMod {
    public const ushort TYPE_MOD_ARRAY = 0x0020;
    public const ushort TYPE_MOD_CLASS = 0x0040;
}

// runtime_defined_s L98-L103
public struct RuntimeDefined {
    public byte NumArg;
    public byte NumLocal;
    public ushort FIndex;
}

// runtime_inherited_s L105-L109
public struct RuntimeInherited {
    public ushort Offset;
    public ushort Index;
}

public struct RuntimeFunction {
    public RuntimeDefined Def;
    public RuntimeInherited Inh;
    public bool IsDefined;
}

// compiler_function_s L139-L145
public sealed class CompilerFunction {
    public string Name = "";
    public ushort Type;
    public ushort RuntimeIndex;
    public ushort Address;
}

// inherit_s L187-L193
public sealed class Inherit {
    public Program? Prog;
    public ushort FunctionIndexOffset;
    public ushort VariableIndexOffset;
    public ushort TypeMod;
}

public sealed class ClassDef {
    public ushort NameIdx;
    public ushort Type;
    public ushort Size;
    public ushort Index;
}

public sealed class ClassMember {
    public ushort NameIdx;
    public ushort Type;
}

// program_s L196-L248 - true layout per program.h
public sealed class Program {
    public const uint DRIVER_ID = 0x20260602; // LPCBIN_DRIVER_ID from binaries.h true
    public const string MAGIC = "NEOL";

    public string Name = "";
    public int Flags;
    public ushort Ref;
    public ushort FuncRef;
    public byte[] Code = new byte[0]; // A_PROGRAM 65535 max
    public int IdNumber;
    public ulong ConfigId; // simul_efun mtime uint64_t
    public byte[]? LineInfo; // A_LINENUMBERS
    public ushort[]? FileInfo; // A_FILE_INFO
    public CompilerFunction[] FunctionTable = new CompilerFunction[0];
    public ushort[] FunctionFlags = new ushort[0];
    public RuntimeFunction[] FunctionOffsets = new RuntimeFunction[0];
    public string[] Strings = new string[0]; // A_STRINGS
    public string[] VariableTable = new string[0]; // A_VAR_NAME
    public ushort[] VariableTypes = new ushort[0];
    public Inherit[] Inherits = new Inherit[0]; // A_INHERITS - renamed from Inherit to Inherits to avoid keyword clash
    public ClassDef[] Classes = new ClassDef[0];
    public ClassMember[] ClassMembers = new ClassMember[0];
    public int TotalSize;
    public int HeartBeat = -1;
    public ushort[]? ArgumentTypes;
    public ushort[]? TypeStart;
    public ushort ProgramSize;
    public ushort NumFunctionsTotal;
    public ushort NumFunctionsDefined;
    public ushort NumStrings;
    public ushort NumVariablesTotal;
    public ushort NumVariablesDefined;
    public ushort NumInherited;

    public RuntimeFunction? FindFuncEntry(int i) {
        if(i < 0 || i >= FunctionOffsets.Length) return null;
        return FunctionOffsets[i];
    }

    public void Reference(string from="") { Ref++; }
    public void Free(int freeSubStrings=1) {
        if(Ref>0) Ref--;
        if(FuncRef>0) return;
        // deallocation would happen here per program.c deallocate_program()
    }
}

// Compatibility aliases - old _S names used in early V4 code, keep them so old files still build
// Remove these after full rename
public sealed class CompilerFunctionS : CompilerFunction {}
public sealed class InheritS : Inherit {}
public sealed class ProgramS : Program {}
public struct RuntimeDefinedS { public byte NumArg; public byte NumLocal; public ushort FIndex; }
public struct RuntimeInheritedS { public ushort Offset; public ushort Index; }
public struct RuntimeFunctionU { public RuntimeDefined Def; public RuntimeInherited Inh; public bool IsDefined; }
