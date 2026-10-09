
namespace LithosNet.V4.VM;
// 1:1 from taedlar/neolith lib/lpc/program.h L62-L251

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
public struct RuntimeDefinedS {
    public byte NumArg;
    public byte NumLocal;
    public ushort FIndex; // function_number_t
}

// runtime_inherited_s L105-L109
public struct RuntimeInheritedS {
    public ushort Offset; // inherit offset
    public ushort Index;  // function index in inherited prog
}

public struct RuntimeFunctionU {
    public RuntimeDefinedS Def;
    public RuntimeInheritedS Inh;
    public bool IsDefined;
}

// compiler_function_s L139-L145
public sealed class CompilerFunctionS {
    public string Name = "";
    public ushort Type; // lpc_type_t
    public ushort RuntimeIndex;
    public ushort Address; // function_address_t opcode offset
}

// inherit_s L187-L193
public sealed class InheritS {
    public ProgramS? Prog;
    public ushort FunctionIndexOffset;
    public ushort VariableIndexOffset;
    public ushort TypeMod;
}

// program_s L196-L251 - binary layout 5 blocks contiguous per comment
public sealed class ProgramS {
    public const uint DRIVER_ID = 0x20260113; // from int64-design.md L211 bumped from 0x20251029
    public string Name = "";
    public int Flags;
    public ushort Ref;
    public ushort FuncRef;
    public byte[] Program = new byte[0]; // A_PROGRAM
    public int IdNumber;
    public ulong ConfigId; // simul_efun mtime
    public byte[]? LineInfo; // A_LINENUMBERS
    public ushort[]? FileInfo;
    public ushort[]? IncludeIndices;
    public ushort NumIncludes;
    public CompilerFunctionS[] FunctionTable = new CompilerFunctionS[0]; // A_COMPILER_FUNCTIONS
    public ushort[] FunctionFlags = new ushort[0]; // A_FUNCTION_FLAGS
    public RuntimeFunctionU[] FunctionOffsets = new RuntimeFunctionU[0]; // A_RUNTIME_FUNCTIONS
    public string[] Strings = new string[0]; // A_STRING
    public string[] VariableTable = new string[0]; // A_VAR_NAME
    public ushort[] VariableTypes = new ushort[0];
    public InheritS[] Inherit = new InheritS[0]; // A_INHERITS
    public int TotalSize;
    public int HeartBeat = -1; // -1 means no heart beat
    public ushort[]? ArgumentTypes;
    public ushort[]? TypeStart;
    public ushort ProgramSize; // must <= 65535 per comment L16-L19
    public ushort NumFunctionsTotal;
    public ushort NumFunctionsDefined;
    public ushort NumStrings;
    public ushort NumVariablesTotal;
    public ushort NumVariablesDefined;
    public ushort NumInherited;

    public RuntimeFunctionU? FindFuncEntry(int i) {
        if(i < 0 || i >= FunctionOffsets.Length) return null;
        return FunctionOffsets[i];
    }
}
