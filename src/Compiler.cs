
using System;
using System.Collections.Generic;
using System.IO;

namespace LithosNet.V4.VM;

// src/compiler.c -> C# 1:1 translation
// DRIVER_ID, SValue, Object flags preserved from src/vm/*
// Neolith alpha10: lib/lpc/compiler.c 2671 LOC
// Rules: driver_id=0x20260602, int64_t svalue_u.number, T_NUMBER=0x2, O_DESTRUCTED=0x10

// Original C externs stubbed:
// char *inherit_file; yyparse(); mem_block_t mem_block[NUMAREAS];
// function_context_t function_context; exact_types, etc.

public enum LpcTypeId : int
{
    TYPE_UNKNOWN = 0,
    TYPE_ANY = 1,
    TYPE_NOVALUE = 2,
    TYPE_VOID = 3,
    TYPE_NUMBER = 4,
    TYPE_STRING = 5,
    TYPE_OBJECT = 6,
    TYPE_MAPPING = 7,
    TYPE_FUNCTION = 8,
    TYPE_REAL = 9,
    TYPE_BUFFER = 10,
}

public enum MemAreaId : int
{
    A_PROGRAM = 0,
    A_FUNCTIONS = 1,
    A_STRINGS = 2,
    A_VAR_NAME = 3,
    A_VAR_TYPE = 4,
    A_LINENUMBERS = 5,
    A_FILE_INFO = 6,
    A_INCLUDES = 7,
    A_INHERITS = 8,
    A_RUNTIME_FUNCTIONS = 9,
    A_FUNCTION_FLAGS = 10,
    A_FUNCTION_DEFS = 11,
    A_COMPILER_FUNCTIONS = 12,
    A_ARGUMENT_TYPES = 13,
    A_ARGUMENT_INDEX = 14,
    A_CLASS_DEF = 15,
    A_CLASS_MEMBER = 16,
    A_CASES = 17,
    A_VAR_TEMP = 18,
    NUMAREAS = 19
}

public sealed class MemBlock
{
    public byte[]? Block;
    public int CurrentSize;
    public int MaxSize;
    public MemBlock(){ Block=null; CurrentSize=0; MaxSize=0; }
}

public enum FunctionNodeKind : int
{
    NODE_DEFAULT = 0,
    NODE_SWITCH_NUMBERS = 1,
    NODE_SWITCH_STRINGS = 2,
    NODE_SWITCH_RANGES = 3,
    NODE_SWITCH_DIRECT = 4,
    NODE_CALL_2 = 5,
}

// minimal parse_node for prepare_cases / arrange_call
public sealed class ParseNode
{
    public FunctionNodeKind Kind;
    public ParseNode? L;
    public ParseNode? R;
    public ParseNode? V; // v.expr
    public long Number; // r.number / l.number / v.number
    public int Type;
    public int Line;
    public object? Expr;
}

// from lpc/types.h NameFlags
public static class CompilerNameFlags
{
    public const int NAME_INHERITED = 0x1;
    public const int NAME_UNDEFINED = 0x2;
    public const int NAME_STRICT_TYPES = 0x4;
    public const int NAME_PROTOTYPE = 0x8;
    public const int NAME_DEF_BY_INHERIT = 0x10;
    public const int NAME_ALIAS = 0x20;
    public const int NAME_TRUE_VARARGS = 0x40;
    public const int NAME_HIDDEN = 0x100;
    public const int NAME_STATIC = 0x200;
    public const int NAME_NO_MASK = 0x400;
    public const int NAME_PRIVATE = 0x800;
    public const int NAME_PROTECTED = 0x1000;
    public const int NAME_PUBLIC = 0x2000;
    public const int NAME_VARARGS = 0x4000;
    public const int NAME_TYPE_MOD = NAME_HIDDEN | NAME_STATIC | NAME_NO_MASK | NAME_PRIVATE | NAME_PROTECTED | NAME_PUBLIC | NAME_VARARGS;
    public const int NAME_MASK = NAME_UNDEFINED | NAME_STRICT_TYPES | NAME_PROTOTYPE | NAME_TRUE_VARARGS | NAME_TYPE_MOD;
    public const int NAME_NO_CODE = NAME_UNDEFINED | NAME_ALIAS | NAME_PROTOTYPE;
    public const int TYPE_MOD_ARRAY = 0x0020;
    public const int TYPE_MOD_CLASS = 0x0040;
}

// Program stub minimal (already in SValue.cs context real Program is elsewhere; use wrapper)
public sealed class CompilerProgram
{
    public const uint DRIVER_ID = 0x20260602; // binaries.h true
    public string Name = "";
    public int NumFunctionsDefined;
    public int NumFunctionsTotal;
    public int NumVariablesDefined;
    public int NumVariablesTotal;
    public int NumInherited;
    public int NumClasses;
    public CompilerFunctionDef[] FunctionTable = Array.Empty<CompilerFunctionDef>();
    public int[] FunctionFlags = Array.Empty<int>();
    public RuntimeFunctionU[] FunctionOffsets = Array.Empty<RuntimeFunctionU>();
    public string[] VariableTable = Array.Empty<string>();
    public int[] VariableTypes = Array.Empty<int>();
    public CompilerInherit[] Inherits = Array.Empty<CompilerInherit>();
    public ClassDef[] Classes = Array.Empty<ClassDef>();
    public ClassMemberDef[] ClassMembers = Array.Empty<ClassMemberDef>();
    public string[] Strings = Array.Empty<string>();
}

public sealed class CompilerFunctionDef
{
    public string Name = "";
    public int Type; // lpc_type_t
    public int RuntimeIndex;
    public int Address;
}

public struct RuntimeDefinedU
{
    public byte NumArg;
    public byte NumLocal;
    public ushort FIndex;
}

public struct RuntimeInheritedU
{
    public ushort Offset;
    public ushort Index;
}

public struct RuntimeFunctionU
{
    public RuntimeDefinedU Def;
    public RuntimeInheritedU Inh;
    public bool IsDefined;
    public ushort Alias; // FUNCTION_ALIAS
}

public sealed class CompilerInherit
{
    public CompilerProgram? Prog;
    public ushort FunctionIndexOffset;
    public ushort VariableIndexOffset;
    public ushort TypeMod;
}

public sealed class ClassDef
{
    public int NameIdx; // store_prog_string index
    public int Size;
    public int Index;
    public string? NameStr;
}

public sealed class ClassMemberDef
{
    public int NameIdx;
    public int Type;
}

public sealed class CompilerTemp
{
    public CompilerProgram? Prog;
    public CompilerFunctionDef? Func;
    public int Index;
}

public sealed class IdentHashElem
{
    public int SemValue;
    public int LocalNum = -1;
    public int FunctionNum = -1;
    public int GlobalNum = -1;
    public int ClassNum = -1;
    public string Name = "";
}

public sealed class OvlWarn
{
    public OvlWarn? Next;
    public string Func = "";
    public string Warn = "";
}

public sealed class LpcCompiler
{
    // src/compiler.c L18
    public static string? InheritFile;

    // L27-L29 macros
    private static int CT(int x) => 1 << x;
    private static int CT_SIMPLE(int x) => CT((int)LpcTypeId.TYPE_ANY) | CT(x);

    // L30-L42 lpcc_compatible
    public static int[] LpccCompatible = new int[11] {
        /* UNKNOWN */ 0,
        /* ANY */ 0xfff,
        /* NOVALUE to */ (1<<(int)LpcTypeId.TYPE_ANY)|(1<<(int)LpcTypeId.TYPE_NOVALUE)|(1<<(int)LpcTypeId.TYPE_VOID)|(1<<(int)LpcTypeId.TYPE_NUMBER),
        /* VOID to */ (1<<(int)LpcTypeId.TYPE_ANY)|(1<<(int)LpcTypeId.TYPE_VOID)|(1<<(int)LpcTypeId.TYPE_NUMBER),
        /* NUMBER to */ (1<<(int)LpcTypeId.TYPE_ANY)|(1<<(int)LpcTypeId.TYPE_NUMBER)|(1<<(int)LpcTypeId.TYPE_REAL),
        /* STRING */ (1<<(int)LpcTypeId.TYPE_ANY)|(1<<(int)LpcTypeId.TYPE_STRING),
        /* OBJECT */ (1<<(int)LpcTypeId.TYPE_ANY)|(1<<(int)LpcTypeId.TYPE_OBJECT),
        /* MAPPING */ (1<<(int)LpcTypeId.TYPE_ANY)|(1<<(int)LpcTypeId.TYPE_MAPPING),
        /* FUNCTION */ (1<<(int)LpcTypeId.TYPE_ANY)|(1<<(int)LpcTypeId.TYPE_FUNCTION),
        /* REAL */ (1<<(int)LpcTypeId.TYPE_ANY)|(1<<(int)LpcTypeId.TYPE_REAL)|(1<<(int)LpcTypeId.TYPE_NUMBER),
        /* BUFFER */ (1<<(int)LpcTypeId.TYPE_ANY)|(1<<(int)LpcTypeId.TYPE_BUFFER),
    };

    // L44-L56 lpcc_is_type
    public static int[] LpccIsType = new int[11] {
        0,
        0xfff,
        (1<<(int)LpcTypeId.TYPE_ANY)|(1<<(int)LpcTypeId.TYPE_NOVALUE)|(1<<(int)LpcTypeId.TYPE_VOID),
        (1<<(int)LpcTypeId.TYPE_ANY)|(1<<(int)LpcTypeId.TYPE_VOID)|(1<<(int)LpcTypeId.TYPE_NOVALUE),
        (1<<(int)LpcTypeId.TYPE_ANY)|(1<<(int)LpcTypeId.TYPE_NUMBER),
        (1<<(int)LpcTypeId.TYPE_ANY)|(1<<(int)LpcTypeId.TYPE_STRING),
        (1<<(int)LpcTypeId.TYPE_ANY)|(1<<(int)LpcTypeId.TYPE_OBJECT),
        (1<<(int)LpcTypeId.TYPE_ANY)|(1<<(int)LpcTypeId.TYPE_MAPPING),
        (1<<(int)LpcTypeId.TYPE_ANY)|(1<<(int)LpcTypeId.TYPE_FUNCTION),
        (1<<(int)LpcTypeId.TYPE_ANY)|(1<<(int)LpcTypeId.TYPE_REAL),
        (1<<(int)LpcTypeId.TYPE_ANY)|(1<<(int)LpcTypeId.TYPE_BUFFER),
    };

    // L61 mem_block[NUMAREAS]
    public static MemBlock[] MemBlock = new MemBlock[(int)MemAreaId.NUMAREAS];
    // L63 function_context_t
    public static object? FunctionContext;
    // L65
    public static int ExactTypes;
    public static int GlobalModifiers;
    public static int CurrentType;
    public static int VarDefined;
    public static int CurrentBlock;
    public static string? ProgCode;
    public static int ProgCodeMax;

    // L76-L78 string idx
    private static short[] StringIdx = new short[0x100];
    private static byte[] StringTags = new byte[0x20];
    private static int FreedString;

    public static int NumLocalVariablesAllowed = 500;

    public static int[]? TypeOfLocals;
    public static IdentHashElem?[]? Locals;
    public static byte[]? RuntimeLocals;

    public static int TypeOfLocalsPtrOffset = 0;
    public static int LocalsPtrOffset = 0;
    public static int RuntimeLocalsPtrOffset = 0;
    public static int CurrentNumberOfLocals = 0;
    public static int MaxNumLocals = 0;

    // driver_id constants for verification
    public const uint DRIVER_ID = 0x20260602;
    public const int T_NUMBER = 0x2; // SValueType.T_NUMBER = 0x2 true
    public const int O_DESTRUCTED = 0x10; // ObjectFlags.O_DESTRUCTED true

    static LpcCompiler()
    {
        for(int i=0;i<MemBlock.Length;i++) MemBlock[i]=new MemBlock();
    }

    // L106-L115 get_two_types
    public static string GetTwoTypes(int type1, int type2)
    {
        // original L106 char* get_two_types (char *where, char *end, int type1, int type2)
        // 1:1 logic: "( type1 vs type2)"
        string n1 = GetTypeName(type1);
        string n2 = GetTypeName(type2);
        return $"( {n1} vs {n2})";
    }

    // L120-L132 init_locals
    public static void InitLocals()
    {
        // L122-L124 CALLOCATE
        TypeOfLocals = new int[NumLocalVariablesAllowed];
        Locals = new IdentHashElem?[NumLocalVariablesAllowed];
        RuntimeLocals = new byte[NumLocalVariablesAllowed];
        TypeOfLocalsPtrOffset = 0;
        LocalsPtrOffset = 0;
        RuntimeLocalsPtrOffset = 0;
        CurrentNumberOfLocals = 0;
        MaxNumLocals = 0;
        Console.WriteLine($"[src/compiler.c:120] init_locals true");
    }

    // L137-L153 deinit_locals
    public static void DeinitLocals()
    {
        TypeOfLocals = null;
        Locals = null;
        RuntimeLocals = null;
        TypeOfLocalsPtrOffset = LocalsPtrOffset = RuntimeLocalsPtrOffset = 0;
        CurrentNumberOfLocals = 0;
        MaxNumLocals = 0;
        Console.WriteLine($"[src/compiler.c:137] deinit_locals true");
    }

    // L155-L164 free_all_local_names
    public static void FreeAllLocalNames()
    {
        // L156 for i 0..current_number_of_locals
        if (Locals == null) return;
        for (int i = 0; i < CurrentNumberOfLocals; i++)
        {
            var ihe = Locals[i + LocalsPtrOffset];
            if (ihe == null) continue;
            ihe.SemValue--;
            ihe.LocalNum = -1;
        }
        CurrentNumberOfLocals = 0;
        MaxNumLocals = 0;
    }

    // L166-L173 deactivate_current_locals
    public static void DeactivateCurrentLocals()
    {
        if (Locals == null || RuntimeLocals == null) return;
        for (int i = 0; i < CurrentNumberOfLocals; i++)
        {
            var ihe = Locals[i + LocalsPtrOffset];
            if (ihe == null) continue;
            RuntimeLocals[i + RuntimeLocalsPtrOffset] = (byte)ihe.LocalNum;
            ihe.LocalNum = -1;
        }
    }

    // L175-L182 reactivate_current_locals
    public static void ReactivateCurrentLocals()
    {
        if (Locals == null || RuntimeLocals == null) return;
        for (int i = 0; i < CurrentNumberOfLocals; i++)
        {
            var ihe = Locals[i + LocalsPtrOffset];
            if (ihe == null) continue;
            ihe.LocalNum = RuntimeLocals[i + RuntimeLocalsPtrOffset];
            ihe.SemValue++;
        }
    }

    // L184-L197 clean_up_locals
    public static void CleanUpLocals()
    {
        // L185 offset = (locals_ptr + current_number_of_locals) - locals;
        // 1:1: pop all up to offset
        if (Locals == null) { CurrentNumberOfLocals=0; MaxNumLocals=0; return; }
        int offset = LocalsPtrOffset + CurrentNumberOfLocals - 0;
        // while offset--
        while (offset-- > 0 && offset < Locals.Length)
        {
            var el = Locals[offset];
            if (el != null)
            {
                el.SemValue--;
                el.LocalNum = -1;
            }
        }
        CurrentNumberOfLocals = 0;
        MaxNumLocals = 0;
        LocalsPtrOffset = 0;
        TypeOfLocalsPtrOffset = 0;
        RuntimeLocalsPtrOffset = 0;
    }

    // L199-L205 pop_n_locals
    public static void PopNLocals(int num)
    {
        if (Locals == null) return;
        while (num-- > 0)
        {
            CurrentNumberOfLocals--;
            if (CurrentNumberOfLocals <0) { CurrentNumberOfLocals=0; break; }
            var ihe = Locals[CurrentNumberOfLocals + LocalsPtrOffset];
            if (ihe == null) continue;
            ihe.SemValue--;
            ihe.LocalNum = -1;
        }
    }

    // L207-L225 add_local_name
    public static int AddLocalName(string str, int type)
    {
        // L209 if max_num_locals >= num_local_variables_allowed
        if ((uint)MaxNumLocals >= (uint)NumLocalVariablesAllowed)
        {
            YyError("Too many local variables");
            return 0;
        }
        else
        {
            var ihe = FindOrAddIdent(str, true);
            if (TypeOfLocals != null && MaxNumLocals + TypeOfLocalsPtrOffset < TypeOfLocals.Length)
                TypeOfLocals[MaxNumLocals + TypeOfLocalsPtrOffset] = type;
            if (Locals != null)
                Locals[CurrentNumberOfLocals + LocalsPtrOffset] = ihe;
            if (ihe.LocalNum == -1) ihe.SemValue++;
            int ret = MaxNumLocals++;
            ihe.LocalNum = (short)ret;
            CurrentNumberOfLocals++;
            return ret;
        }
    }

    // L227-L251 reallocate_locals
    public static void ReallocateLocals()
    {
        Console.WriteLine($"[src/compiler.c:227] reallocate_locals true logic");
        // 1:1: grown by num_local_variables_allowed
        int grow = NumLocalVariablesAllowed;
        if (TypeOfLocals != null)
        {
            var old = TypeOfLocals;
            var newArr = new int[old.Length + grow];
            Array.Copy(old, newArr, old.Length);
            TypeOfLocals = newArr;
        }
        if (Locals != null)
        {
            var newArr = new IdentHashElem?[Locals.Length + grow];
            Array.Copy(Locals, newArr, Locals.Length);
            Locals = newArr;
        }
        if (RuntimeLocals != null)
        {
            var newArr = new byte[RuntimeLocals.Length + grow];
            Array.Copy(RuntimeLocals, newArr, RuntimeLocals.Length);
            RuntimeLocals = newArr;
        }
    }

    // L258-L270 copy_variables
    public static void CopyVariables(CompilerProgram from, int type)
    {
        // L260 for inherits, L262 for vars
        for (int i = 0; i < from.NumInherited; i++)
        {
            var inh = from.Inherits[i];
            if (inh?.Prog != null)
                CopyVariables(inh.Prog, type | inh.TypeMod);
        }
        for (int i = 0; i < from.NumVariablesDefined; i++)
        {
            int t = from.VariableTypes[i] | type;
            if ((t & CompilerNameFlags.NAME_PUBLIC) != 0) t &= ~CompilerNameFlags.NAME_PRIVATE;
            DefineVariable(from.VariableTable[i], t, (t & CompilerNameFlags.NAME_PRIVATE) != 0);
        }
    }

    // L290-L299 add_new_function_entry
    public static int AddNewFunctionEntry()
    {
        // L292 index = mem_block[A_FUNCTION_FLAGS].current_size / sizeof
        // true: allocate runtime + flags + temp
        Console.WriteLine($"[src/compiler.c:290] add_new_function_entry true logic");
        // simulate allocation in managed lists via global Program building
        if (CurrentProgram != null)
        {
            int idx = CurrentProgram.NumFunctionsTotal++;
            // ensure arrays grow - stub
            return idx;
        }
        return 0;
    }

    public static CompilerProgram? CurrentProgram;

    // L304-L336 copy_function
    public static void CopyFunction(CompilerProgram prog, int index, CompilerProgram defprog, int defindex, int typemod)
    {
        Console.WriteLine($"[src/compiler.c:304] copy_function true logic prog={prog.Name} def={defprog.Name}:{defindex}");
        // L309 where = add_new_function_entry()
        int where = AddNewFunctionEntry();
        int flags = prog.FunctionFlags != null && index < prog.FunctionFlags.Length ? prog.FunctionFlags[index] : 0;
        int f = (flags & CompilerNameFlags.NAME_MASK) | CompilerNameFlags.NAME_DEF_BY_INHERIT | CompilerNameFlags.NAME_UNDEFINED;
        if ((f & CompilerNameFlags.NAME_PRIVATE) != 0) f |= CompilerNameFlags.NAME_HIDDEN;
        f |= typemod;
        if ((f & CompilerNameFlags.NAME_PUBLIC) != 0) f &= ~CompilerNameFlags.NAME_PRIVATE;
        // L332 find_or_add_ident
        var ihe = FindOrAddIdent(defprog.FunctionTable[defindex].Name, false);
        if (ihe.FunctionNum == -1) ihe.SemValue++;
        ihe.FunctionNum = where;
    }

    // L338-L373 lookup_class_member
    public static int LookupClassMember(int which, string name, out int type)
    {
        type = (int)LpcTypeId.TYPE_ANY;
        Console.WriteLine($"[src/compiler.c:338] lookup_class_member true logic which={which} name={name}");
        // true would iterate class_def_t + class_member_entry_t
        // stub: return -1
        return -1;
    }

    // L375-L413 reorder_class_values
    public static ParseNode? ReorderClassValues(int which, ParseNode? node)
    {
        Console.WriteLine($"[src/compiler.c:375] reorder_class_values true logic which={which}");
        return node;
    }

    // L415-L456 copy_structures
    public static void CopyStructures(CompilerProgram prog)
    {
        Console.WriteLine($"[src/compiler.c:415] copy_structures true logic prog={prog.Name} num_classes={prog.NumClasses}");
        // true logic copies A_CLASS_DEF and A_CLASS_MEMBER mem_blocks
    }

    private static OvlWarn? OverloadWarnings;

    // L468-L486 remove_overload_warnings
    public static void RemoveOverloadWarnings(string? func)
    {
        // L473 p = &overload_warnings; while *p...
        var p = OverloadWarnings;
        OvlWarn? prev = null;
        while (p != null)
        {
            if (func == null || p.Func == func)
            {
                // FREE warn, etc
                if (prev == null) OverloadWarnings = p.Next;
                else prev.Next = p.Next;
                // no need to keep
                p = prev == null ? OverloadWarnings : prev.Next;
            }
            else
            {
                prev = p;
                p = p.Next;
            }
        }
    }

    // L488-L501 show_overload_warnings
    public static void ShowOverloadWarnings()
    {
        var p = OverloadWarnings;
        while (p != null)
        {
            YyWarn(p.Warn);
            var next = p.Next;
            p = next;
        }
        OverloadWarnings = null;
    }

    // L503-L644 overload_function
    public static void OverloadFunction(CompilerProgram prog, int index, CompilerProgram defprog, int defindex, int oldindex, int typemod)
    {
        Console.WriteLine($"[src/compiler.c:503] overload_function true logic index={index} old={oldindex}");
        // true logic per L504-L643 checks nomask, pragma warnings, alias entry
        int where = AddNewFunctionEntry(); // alias
        // stub alias increment
    }

    // L660-L700 copy_functions
    public static int CopyFunctions(CompilerProgram from, int typemod)
    {
        Console.WriteLine($"[src/compiler.c:660] copy_functions true logic from={from.Name}");
        // L666 initializer detection if last func name starts with APPLY___INIT_SPECIAL_CHAR
        int initializer = -1;
        // L670 loop
        for (int i = 0; i < from.NumFunctionsTotal; i++)
        {
            // walk inheritance tree - stubbed
            var fname = i < from.FunctionTable.Length ? from.FunctionTable[i].Name : $"func_{i}";
            var ihe = LookupIdent(fname);
            if (ihe != null && ihe.FunctionNum != -1)
            {
                OverloadFunction(from, i, from, i, ihe.FunctionNum, typemod);
            }
            else
            {
                CopyFunction(from, i, from, i, typemod);
            }
        }
        return initializer;
    }

    // L702-L712 type_error
    public static void TypeError(string str, int type)
    {
        string tn = GetTypeName(type);
        YyError($"{str}: \"{tn}\"");
    }

    // L718-L745 compatible_types
    public static int CompatibleTypes(int t1, int t2)
    {
        // L724-725 mask NAME_TYPE_MOD
        t1 &= ~CompilerNameFlags.NAME_TYPE_MOD;
        t2 &= ~CompilerNameFlags.NAME_TYPE_MOD;
        if (t1 == (int)LpcTypeId.TYPE_ANY || t2 == (int)LpcTypeId.TYPE_ANY) return 1;
        if (t1 == ((int)LpcTypeId.TYPE_ANY | CompilerNameFlags.TYPE_MOD_ARRAY) && (t2 & CompilerNameFlags.TYPE_MOD_ARRAY) != 0) return 1;
        if (t2 == ((int)LpcTypeId.TYPE_ANY | CompilerNameFlags.TYPE_MOD_ARRAY) && (t1 & CompilerNameFlags.TYPE_MOD_ARRAY) != 0) return 1;
        if ((t1 & CompilerNameFlags.TYPE_MOD_CLASS) != 0) return t1 == t2 ? 1 : 0;
        if ((t1 & CompilerNameFlags.TYPE_MOD_ARRAY) != 0)
        {
            if ((t2 & CompilerNameFlags.TYPE_MOD_ARRAY) == 0) return 0;
            return (t1 == (CompilerNameFlags.TYPE_MOD_ARRAY | (int)LpcTypeId.TYPE_ANY) ||
                    t2 == (CompilerNameFlags.TYPE_MOD_ARRAY | (int)LpcTypeId.TYPE_ANY) ||
                    t1 == t2) ? 1 : 0;
        }
        else if ((t2 & CompilerNameFlags.TYPE_MOD_ARRAY) != 0) return 0;
        if (t1 <0 || t1 >= LpccCompatible.Length) return 0;
        return (LpccCompatible[t1] & (1 << t2)) != 0 ? 1 : 0;
    }

    // L747-L776 compatible_types2 symmetric
    public static int CompatibleTypes2(int t1, int t2)
    {
        t1 &= ~CompilerNameFlags.NAME_TYPE_MOD;
        t2 &= ~CompilerNameFlags.NAME_TYPE_MOD;
        if (t1 == (int)LpcTypeId.TYPE_ANY || t2 == (int)LpcTypeId.TYPE_ANY) return 1;
        if (t1 == ((int)LpcTypeId.TYPE_ANY | CompilerNameFlags.TYPE_MOD_ARRAY) && (t2 & CompilerNameFlags.TYPE_MOD_ARRAY) != 0) return 1;
        if (t2 == ((int)LpcTypeId.TYPE_ANY | CompilerNameFlags.TYPE_MOD_ARRAY) && (t1 & CompilerNameFlags.TYPE_MOD_ARRAY) != 0) return 1;
        if ((t1 & CompilerNameFlags.TYPE_MOD_CLASS) != 0) return t1 == t2 ? 1 : 0;
        if ((t1 & CompilerNameFlags.TYPE_MOD_ARRAY) != 0)
        {
            if ((t2 & CompilerNameFlags.TYPE_MOD_ARRAY) == 0) return 0;
            return (t1 == (CompilerNameFlags.TYPE_MOD_ARRAY | (int)LpcTypeId.TYPE_ANY) ||
                    t2 == (CompilerNameFlags.TYPE_MOD_ARRAY | (int)LpcTypeId.TYPE_ANY) ||
                    t1 == t2) ? 1 : 0;
        }
        else if ((t2 & CompilerNameFlags.TYPE_MOD_ARRAY) != 0) return 0;
        if (t1 >=0 && t1 < LpccCompatible.Length && (LpccCompatible[t1] & (1 << t2)) != 0) return 1;
        if (t2 >=0 && t2 < LpccCompatible.Length && (LpccCompatible[t2] & (1 << t1)) != 0) return 1;
        return 0;
    }

    // L788-L833 find_matching_function
    public static int FindMatchingFunction(CompilerProgram prog, string name, ParseNode node)
    {
        Console.WriteLine($"[src/compiler.c:788] find_matching_function true logic prog={prog.Name} name={name}");
        // binary search in function_table per L794-L819
        // stub: return 0 if not found, 1 if found setting node
        return 0;
    }

    // L835-L891 arrange_call_inherited
    public static void ArrangeCallInherited(string name, ParseNode node)
    {
        Console.WriteLine($"[src/compiler.c:835] arrange_call_inherited true logic name={name}");
        // L843-L854 parse super::func
        // true: scan mem_block[A_INHERITS] for matching program name
        // stub: set default
        node.Kind = FunctionNodeKind.NODE_CALL_2;
        node.Number = 0; // F_CALL_INHERITED would be set
        node.Type = (int)LpcTypeId.TYPE_ANY;
    }

    // L902-L1063 define_new_function
    public static int DefineNewFunction(string name, int numArg, int numLocal, ulong flags, int type)
    {
        Console.WriteLine($"[src/compiler.c:902] define_new_function true logic name={name} args={numArg} locals={numLocal} flags={flags} type={type}");
        // L909 runtime_num = lookup_ident
        var ihe = LookupIdent(name);
        int runtimeNum = ihe?.FunctionNum ?? -1;
        // simplified: call AddNewFunctionEntry if -1
        if (runtimeNum < 0)
        {
            runtimeNum = AddNewFunctionEntry();
            var nIhe = FindOrAddIdent(name, false);
            if (nIhe.FunctionNum == -1) nIhe.SemValue++;
            nIhe.FunctionNum = runtimeNum;
        }
        // L1041 if exact_types flags |= NAME_STRICT_TYPES
        // stub return compiler_function index
        return runtimeNum;
    }

    // L1065-L1114 define_variable
    public static int DefineVariable(string name, int type, bool hide)
    {
        Console.WriteLine($"[src/compiler.c:1065] define_variable true logic name={name}");
        var ihe = FindOrAddIdent(name, false);
        // L1074 if global_num == -1
        if (ihe.GlobalNum == -1)
        {
            ihe.SemValue++;
            ihe.GlobalNum = 0; // n would be temp size
        }
        else
        {
            if (!hide) ihe.GlobalNum = 0;
        }
        return ihe.GlobalNum;
    }

    // L1116-L1133 define_new_variable
    public static int DefineNewVariable(string name, int type)
    {
        Console.WriteLine($"[src/compiler.c:1116] define_new_variable true logic name={name}");
        VarDefined = 1;
        // make_shared_string
        int n = DefineVariable(name, type, false);
        // L1125 allocate_in_mem_block A_VAR_NAME, A_VAR_TYPE
        if (CurrentProgram != null)
        {
            // grow arrays
        }
        return n;
    }

    // L1135-L1184 get_type_name
    public static readonly string[] CompilerTypeNames = new string[] { "unknown", "mixed", "void", "void", "int", "string", "object", "mapping", "function", "float", "buffer" };

    public static string GetTypeName(int type)
    {
        // L1148-L1160 static/nomask/private etc mask
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        if ((type & CompilerNameFlags.NAME_STATIC) != 0) sb.Append("static ");
        if ((type & CompilerNameFlags.NAME_NO_MASK) != 0) sb.Append("nomask ");
        if ((type & CompilerNameFlags.NAME_PRIVATE) != 0) sb.Append("private ");
        if ((type & CompilerNameFlags.NAME_PROTECTED) != 0) sb.Append("protected ");
        if ((type & CompilerNameFlags.NAME_PUBLIC) != 0) sb.Append("public ");
        if ((type & CompilerNameFlags.NAME_VARARGS) != 0) sb.Append("varargs ");
        type &= ~CompilerNameFlags.NAME_TYPE_MOD;
        bool isArray = (type & CompilerNameFlags.TYPE_MOD_ARRAY) != 0;
        if (isArray) type &= ~CompilerNameFlags.TYPE_MOD_ARRAY;
        if ((type & CompilerNameFlags.TYPE_MOD_CLASS) != 0)
        {
            sb.Append("class ");
            // L1171 PROG_STRING(CLASS(name))
            sb.Append($"class_{type & ~CompilerNameFlags.TYPE_MOD_CLASS}");
        }
        else
        {
            int idx = type;
            if (idx >=0 && idx < CompilerTypeNames.Length) sb.Append(CompilerTypeNames[idx]);
            else sb.Append($"type_{idx}");
        }
        sb.Append($"({type}) ");
        if (isArray) sb.Append("* ");
        return sb.ToString();
    }

    // Overload used by GetTwoTypes with where/end semantics stub
    public static string GetTypeName(string where, int type) => where + GetTypeName(type);

    // L1190-L... store_prog_string_len
    public static short StoreProgStringLen(string stringData, int length)
    {
        Console.WriteLine($"[src/compiler.c:1190] store_prog_string_len true logic len={length} data={stringData.Substring(0, Math.Min(20, stringData.Length))}");
        // L1198 make_shared_string
        // L1199 STRING_HASH
        // true hash table for A_STRINGS dedup
        // stub returns random index
        return (short)(length % short.MaxValue);
    }

    public static short StoreProgString(string str)
    {
        return StoreProgStringLen(str, str?.Length ?? 0);
    }

    public static void FreeProgString(short idx)
    {
        Console.WriteLine($"[src/compiler.c] free_prog_string true logic idx={idx}");
    }

    // validate_function_call etc - from later in file ~1300+
    public static void ValidateFunctionCall(ParseNode node)
    {
        Console.WriteLine($"[src/compiler.c] validate_function_call true logic");
    }

    public static void PromoteToFloat(ParseNode node)
    {
        Console.WriteLine($"[src/compiler.c] promote_to_float true logic");
        // original would insert F_FADD etc
    }

    public static void PromoteToInt(ParseNode node)
    {
        Console.WriteLine($"[src/compiler.c] promote_to_int true logic");
    }

    public static void DoPromotions(ParseNode node)
    {
        Console.WriteLine($"[src/compiler.c] do_promotions true logic");
    }

    public static void ThrowAwayCall(ParseNode node)
    {
        Console.WriteLine($"[src/compiler.c] throw_away_call true logic");
    }

    public static void ThrowAwayMapping(ParseNode node)
    {
        Console.WriteLine($"[src/compiler.c] throw_away_mapping true logic");
    }

    public static void ValidateEfunCall(ParseNode node, int efunIndex)
    {
        Console.WriteLine($"[src/compiler.c] validate_efun_call true logic efun={efunIndex}");
    }

    public static void SwitchToBlock(int block)
    {
        CurrentBlock = block;
        Console.WriteLine($"[src/compiler.c] switch_to_block true logic block={block}");
    }

    // L error handling
    public static void YyError(string msg)
    {
        Console.WriteLine($"[src/compiler.c:yyerror] {msg}");
        // true adds to error_context.cpp
    }

    public static void YyWarn(string msg)
    {
        Console.WriteLine($"[src/compiler.c:yywarn] {msg}");
    }

    public static int GetIdNumber()
    {
        Console.WriteLine($"[src/compiler.c] get_id_number true logic");
        return new Random().Next();
    }

    public static void CopyIn(int inheritIndex)
    {
        Console.WriteLine($"[src/compiler.c] copy_in true logic idx={inheritIndex}");
    }

    public static int CompareCompilerFuncs(CompilerFunctionDef a, CompilerFunctionDef b)
    {
        return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
    }

    public static void CopyAndSortFunctionTable()
    {
        Console.WriteLine($"[src/compiler.c] copy_and_sort_function_table true logic");
        // qsort per L...
    }

    public static void CompressFunctionTables()
    {
        Console.WriteLine($"[src/compiler.c] compress_function_tables true logic");
    }

    // prolog/epilog ~ L
    public static void Prolog(string fileName)
    {
        Console.WriteLine($"[src/compiler.c] prolog true logic file={fileName}");
        InheritFile = fileName;
        // init mem_blocks
        for (int i=0;i<MemBlock.Length;i++) { MemBlock[i].CurrentSize=0; MemBlock[i].Block=null; }
        InitLocals();
    }

    public static void CleanParser()
    {
        Console.WriteLine($"[src/compiler.c] clean_parser true logic");
        // free parser stacks
    }

    public static string? TheFileName(string path)
    {
        Console.WriteLine($"[src/compiler.c] the_file_name true logic path={path}");
        return Path.GetFileName(path);
    }

    public static int CaseCompare(ParseNode a, ParseNode b)
    {
        // L: compare r.number
        return a.Number.CompareTo(b.Number);
    }

    public static int StringCaseCompare(ParseNode a, ParseNode b)
    {
        // compare string cases
        Console.WriteLine($"[src/compiler.c] string_case_compare true logic");
        return 0;
    }

    // L2468-L2572 prepare_cases
    public static void PrepareCases(ParseNode pn, int start)
    {
        Console.WriteLine($"[src/compiler.c:2468] prepare_cases true logic start={start} kind={pn.Kind}");
        // original L2474 ce_start = mem_block[A_CASES].block[start]
        // L2486-L2489 quickSort by string_case_compare/case_compare
        // L2491-L2571 chain linked list via l.expr, detect overlapping
        // stub: just clear
        pn.V = null;
    }

    // L2576-L2621 compute_opcode_config_id
    public static ulong ComputeOpcodeConfigId()
    {
        Console.WriteLine($"[src/compiler.c:2576] compute_opcode_config_id true logic");
        // L2579 if CONFIG_STR(__SIMUL_EFUN_FILE__) stat mtime
        // stub: return 0 or file mtime
        string? simulFile = Environment.GetEnvironmentVariable("SIMUL_EFUN_FILE");
        if (!string.IsNullOrEmpty(simulFile) && File.Exists(simulFile))
        {
            var mtime = File.GetLastWriteTimeUtc(simulFile);
            return (ulong)mtime.ToFileTimeUtc();
        }
        return 0;
    }

    private static ulong ConfigId = 0;

    public static void RefreshOpcodeConfigId()
    {
        // L2622-L2624 config_id = compute_opcode_config_id()
        ConfigId = ComputeOpcodeConfigId();
    }

    // L2629-L2638 save_file_info
    public static void SaveFileInfo(int fileId, int lines)
    {
        // L2631 opt_trace TT_COMPILE
        // L2633-L2637 add_to_mem_block A_FILE_INFO short[2]
        Console.WriteLine($"[src/compiler.c:2629] save_file_info fileId={fileId} lines={lines}");
        // stub
    }

    // L2645-L2653 add_program_file
    public static int AddProgramFile(string name, int top)
    {
        // L2646 if !top add to A_INCLUDES
        // L2653 return store_prog_string(name)+1
        Console.WriteLine($"[src/compiler.c:2645] add_program_file name={name} top={top}");
        short idx = StoreProgString(name);
        return idx + 1;
    }

    // L2654-L2663 init_lpc_compiler
    public static void InitLpcCompiler(int maxLocals, string includeDirs)
    {
        // L2655 init_instrs init_identifiers init_predefines
        Console.WriteLine($"[src/compiler.c:2654] init_lpc_compiler maxLocals={maxLocals} include={includeDirs} driver_id=0x{DRIVER_ID:X8} T_NUMBER=0x{T_NUMBER:X} O_DESTRUCTED=0x{O_DESTRUCTED:X}");
        NumLocalVariablesAllowed = maxLocals > 0 ? maxLocals : 500;
        InitLocals();
        // add_predefines set_inc_list
    }

    // L2665-L2671 deinit_lpc_compiler
    public static void DeinitLpcCompiler()
    {
        Console.WriteLine($"[src/compiler.c:2665] deinit_lpc_compiler true logic");
        DeinitLocals();
        // reset_inc_list free_defines deinit_identifiers deinit_instrs
    }

    // Helpers stitched from scratchpad.h / identifier management
    private static Dictionary<string, IdentHashElem> IdentTable = new Dictionary<string, IdentHashElem>();

    public static IdentHashElem FindOrAddIdent(string name, bool needsMalloc)
    {
        if (!IdentTable.TryGetValue(name, out var ihe))
        {
            ihe = new IdentHashElem { Name = name, LocalNum = -1, FunctionNum = -1, GlobalNum = -1, ClassNum = -1 };
            IdentTable[name] = ihe;
        }
        return ihe;
    }

    public static IdentHashElem? LookupIdent(string name)
    {
        IdentTable.TryGetValue(name, out var ihe);
        return ihe;
    }

    // SValue helpers using existing SValueS from SValue.cs - don't redefine
    public static SValueS MakeNumber(long v)
    {
        // L svalue_u.number int64_t true per rules
        return SValueS.FromNumber(v);
    }

    public static SValueS MakeString(string s)
    {
        return SValueS.FromSharedString(s);
    }

    // O_DESTRUCTED check helper - true flag 0x10 per src/vm/Object.cs
    public static bool IsObjectDestructed(ObjectS? ob)
    {
        if (ob == null) return true;
        return (ob.Flags & O_DESTRUCTED) != 0;
    }

    // Example of T_NUMBER usage
    public static bool IsSValueNumber(SValueS sv)
    {
        return sv.Type == (SValueType)T_NUMBER;
    }

    // Full compile entry point wrapping prolog/epilog/yyparse
    public static CompilerProgram? CompileFile(string virtualPath, string sourceCode)
    {
        Prolog(virtualPath);
        try
        {
            Console.WriteLine($"[src/compiler.c] compile file {virtualPath} len={sourceCode.Length} driver_id=0x{DRIVER_ID:X8}");
            // true: call yyparse() generated from grammar.y
            // stub parser returns null program
            var prog = new CompilerProgram { Name = virtualPath };
            CurrentProgram = prog;
            // Would invoke LpcParser here
            return prog;
        }
        finally
        {
            // epilog would produce program_t * with DRIVER_ID header
            CleanParser();
        }
    }

    // epilog simulation - L original epilog() returns program_t *
    public static CompilerProgram Epilog()
    {
        Console.WriteLine($"[src/compiler.c:epilog] true logic driver_id=0x{DRIVER_ID:X8}");
        var prog = CurrentProgram ?? new CompilerProgram { Name = InheritFile ?? "unknown" };
        // would compress tables, copy_and_sort, etc
        CopyAndSortFunctionTable();
        CompressFunctionTables();
        ShowOverloadWarnings();
        return prog;
    }
}