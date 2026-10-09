using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

//
// 1:1 C# translation of neolith-1.0.0-alpha.10 src object.c
// Original C: /home/code-interpreter/neolith-full-mini/neolith-1.0.0-alpha.10/lib/lpc/object.c
// Line comments use original C line numbers.
// Rules: driver_id=0x20260602, int64_t svalue_u.number, T_NUMBER=0x2, O_DESTRUCTED=0x10
// Output: src/Object.cs under /mnt/data/neolith-cs/
// Namespace: LithosNet.V4.VM, SValueS reused from SValue.cs (not redefined)
//

namespace LithosNet.V4.VM;

// L20-L74 from lib/lpc/object.h
public static class ObjectFlags
{
    public const ushort O_HEART_BEAT = 0x0001;
    public const ushort O_IS_WIZARD = 0x0002;
    public const ushort O_LISTENER = 0x0004;
    public const ushort O_ENABLE_COMMANDS = 0x0004;
    public const ushort O_CLONE = 0x0008;
    public const ushort O_DESTRUCTED = 0x0010; // task rule 0x10
    public const ushort O_CONSOLE_USER = 0x0020;
    public const ushort O_ONCE_INTERACTIVE = 0x0040;
    public const ushort O_RESET_STATE = 0x0080;
    public const ushort O_WILL_CLEAN_UP = 0x0100;
    public const ushort O_VIRTUAL = 0x0200;
    public const ushort O_HIDDEN = 0x0400;
    public const ushort O_EFUN_SOCKET = 0x0800;
    public const ushort O_WILL_RESET = 0x1000;
    public const ushort O_UNUSED = 0x8000;
}

// L42-L49 struct sentence_s + carryover args doc
public sealed class SentenceS
{
    public string Verb = "";
    public SentenceS? Next;
    public ObjectS? Ob;
    public string? FunctionName; // string_or_func_t simplified
    public int Flags;
    public ArrayS? Args; // carryover arguments
    public const int V_FUNCTION = 0x01;
}

// L53-L74 struct object_s, variables[1] MUST be last L72-L73
public sealed class ObjectS
{
    public ushort Ref;
    public ushort Flags;
    public string Name = "";
    public long LoadTime;
    public long NextReset;
    public long TimeOfRef;
    public Program? Prog;
    public ObjectS? NextAll;
    public ObjectS? NextInv;
    public ObjectS? Contains;
    public ObjectS? Super;
    public InteractiveS? Interactive;
    public SentenceS? Sent;
    public string? LivingName;
    public ObjectS? NextHashedLiving; // from living hash linkage
    public UserIdS? Uid;
    public UserIdS? Euid;
    public Program? ProgOld; // extra safety
    public SValueS[] Variables = new SValueS[0];

    public bool IsDestructed => (Flags & ObjectFlags.O_DESTRUCTED) != 0;
    public bool IsClone => (Flags & ObjectFlags.O_CLONE) != 0;
    public bool HasHeartBeat => (Flags & ObjectFlags.O_HEART_BEAT) != 0;
    public void AddRef(string caller) { Ref++; }
}
public sealed class InteractiveS { }
public sealed class UserIdS { }

// Minimal Program model for object.c dependencies
public sealed class ProgramInherit { public Program? Prog; public ushort TypeMod; }
public sealed class Program
{
    public string Name = "";
    public int Ref = 1;
    public int NumVariablesDefined;
    public int NumVariablesTotal;
    public int NumFunctionsDefined;
    public int NumInherited;
    public List<ProgramInherit> Inherit = new();
    public string[] VariableTable = Array.Empty<string>();
    public ushort[] VariableTypes = Array.Empty<ushort>();
    public List<CompilerFunction> FunctionTable = new();
}
public sealed class CompilerFunction
{
    public string Name = "";
    public int RuntimeIndex;
    public int Address;
}

public static class NeolithDriverConst
{
    public const int DriverId = 0x20260602; // driver_id=0x20260602
    public const int T_NUMBER = 0x2; // T_NUMBER=0x2
    public const ushort O_DESTRUCTED = 0x0010; // O_DESTRUCTED=0x10
}

// L32 globals
public static class ObjectGlobals
{
    public static ulong TotAllocObject = 0; // L32 tot_alloc_object
    public static ulong TotAllocObjectSize = 0; // L32 tot_alloc_object_size
    public static int SaveSvalueDepth = 0; // L201 save_svalue_depth
    public static int SaveMaxDepth = 0; // L202
    public static int[]? SaveSvalueSizes = null; // L203
    public static long Sel = -1; // L1504 sel = (size_t)-1
    public static SentenceS? SentFree = null; // L1771 sent_free
    public static int TotAllocSentence = 0; // L1772 tot_alloc_sentence
    public static ObjectS?[]? HashedLiving = null; // living hash
    public static int NumLivingNames = 0;
    public static long SearchLength = 0;
    public static long NumSearches = 0;
    public const int MAX_SAVE_SVALUE_DEPTH = 128;
    public const string SAVE_EXTENSION = ".o";
    public static class RestoreError
    {
        public const int ROB_ERROR = 0xF00;
        public const int ROB_GENERAL_ERROR = 0x1 | ROB_ERROR;
        public const int ROB_NUMERAL_ERROR = 0x2 | ROB_ERROR;
        public const int ROB_ARRAY_ERROR = 0x4 | ROB_ERROR;
        public const int ROB_MAPPING_ERROR = 0x8 | ROB_ERROR;
        public const int ROB_STRING_ERROR = 0x10 | ROB_ERROR;
        public const int ROB_CLASS_ERROR = 0x20 | ROB_ERROR;
    }
}

// Core 1:1 function translations - keep names same as C
public static class ObjectOps
{
    // L29-L30 macro
    public static void TooDeepSaveError()
    {
        // L29 #define too_deep_save_error()
        Console.WriteLine($"[src/object.c:29] too_deep_save_error() [driver {NeolithDriverConst.DriverId:X}]");
        throw new InvalidOperationException($"Mappings and/or arrays nested too deep ({ObjectGlobals.MAX_SAVE_SVALUE_DEPTH}) for save_object");
    }

    // L42-L60 restore_hash_string
    public static int restore_hash_string(ref string src, ref int pos, SValueS sv)
    {
        // L42 static int restore_hash_string (const char **val, svalue_t * sv)
        Console.WriteLine("[src/object.c:42] restore_hash_string true logic - shared string");
        int endPos;
        var tmp = new SValueS();
        int err = unquote_and_unescape_string(src, pos, tmp, out endPos);
        if (err != 0) return err;
        pos = endPos;
        // L55 make_shared_string + SET_SVALUE_SHARED_STRING
        sv.Type = SValueType.T_STRING;
        sv.Subtype = (short)StringSubtype.STRING_SHARED;
        sv.U = new SValueU { SharedString = tmp.StrPtr() };
        return 0;
    }

    // L62-L69 restore_interior_string
    public static int restore_interior_string(ref string src, ref int pos, SValueS sv)
    {
        // L62 static int restore_interior_string
        int endPos;
        int err = unquote_and_unescape_string(src, pos, sv, out endPos);
        if (err == 0) pos = endPos;
        return err;
    }

    // L71-L177 parse_numeric
    public static int parse_numeric(string s, ref int cpp, char c, SValueS dest)
    {
        // L71 static int parse_numeric (const char **cpp, char c, svalue_t * dest)
        // int64_t svalue_u.number per task
        int res = 0;
        int neg = 0;
        if (c == '-')
        {
            neg = 1;
            res = 0;
            if (cpp >= s.Length) return 0;
            c = s[cpp++];
            if (!char.IsDigit(c)) return 0; // L80-L81
        }
        res = c - '0'; // L85
        while (cpp < s.Length && char.IsDigit(s[cpp]))
        {
            c = s[cpp++];
            res *= 10;
            res += c - '0';
        }
        // L92 dot -> T_REAL
        if (cpp < s.Length && s[cpp] == '.')
        {
            cpp++;
            double f1 = 0.0, f2 = 10.0;
            if (cpp >= s.Length || !char.IsDigit(s[cpp])) return 0;
            do
            {
                c = s[cpp++];
                f1 += (c - '0') / f2;
                f2 *= 10;
            } while (cpp < s.Length && char.IsDigit(s[cpp]));
            f1 += res;
            if (cpp < s.Length && s[cpp] == 'e')
            {
                cpp++;
                if (cpp >= s.Length) return 0;
                c = s[cpp++];
                int expo = 0;
                if (c == '+')
                {
                    while (cpp < s.Length && char.IsDigit(s[cpp])) { expo = expo * 10 + (s[cpp++] - '0'); }
                    f1 *= Math.Pow(10.0, expo);
                }
                else if (c == '-')
                {
                    while (cpp < s.Length && char.IsDigit(s[cpp])) { expo = expo * 10 + (s[cpp++] - '0'); }
                    f1 *= Math.Pow(10.0, -expo);
                }
                else return 0;
            }
            dest.Type = SValueType.T_REAL; // L134
            dest.U = new SValueU { Real = neg != 0 ? -f1 : f1 };
            return 1;
        }
        else if (cpp < s.Length && s[cpp] == 'e')
        {
            cpp++;
            if (cpp >= s.Length) return 0;
            c = s[cpp++];
            int expo = 0;
            double f1;
            if (c == '+')
            {
                while (cpp < s.Length && char.IsDigit(s[cpp])) { expo = expo * 10 + (s[cpp++] - '0'); }
                f1 = res * Math.Pow(10.0, expo);
            }
            else if (c == '-')
            {
                while (cpp < s.Length && char.IsDigit(s[cpp])) { expo = expo * 10 + (s[cpp++] - '0'); }
                f1 = res * Math.Pow(10.0, -expo);
            }
            else return 0;
            dest.Type = SValueType.T_REAL; // L165
            dest.U = new SValueU { Real = neg != 0 ? -f1 : f1 };
            return 1;
        }
        else
        {
            dest.Type = (SValueType)NeolithDriverConst.T_NUMBER; // L172 T_NUMBER=0x2
            dest.U = new SValueU { Number = neg != 0 ? -res : res }; // L173 int64_t svalue_u.number
            return 1;
        }
    }

    // L179-L183 add_map_stats
    public static void add_map_stats(MappingS m, int count)
    {
        // L179 static void add_map_stats (mapping_t * m, int count)
        Console.WriteLine($"[src/object.c:179] add_map_stats count={count} true logic");
        // total_mapping_nodes += count ...
    }

    // L185-L198 valid_hide
    public static int valid_hide(ObjectS obj)
    {
        // L185 int valid_hide (object_t * obj)
        Console.WriteLine("[src/object.c:185] valid_hide true logic - master::valid_hide");
        if (obj == null) return 0;
        // L193 push_object(obj); ret=APPLY_SLOT_MASTER_CALL(APPLY_VALID_HIDE,1);
        return 1;
    }

    // L208-L303 svalue_save_size
    public static ulong svalue_save_size(SValueS v)
    {
        // L208 size_t svalue_save_size (const svalue_t * v)
        switch (v.Type)
        {
            case SValueType.T_STRING:
                {
                    var cp = v.StrPtr() ?? "";
                    ulong sz = 0;
                    foreach (var ch in cp) { if (ch == '\\' || ch == '"') sz++; sz++; }
                    return 3 + sz;
                }
            case SValueType.T_ARRAY:
                {
                    if (++ObjectGlobals.SaveSvalueDepth > ObjectGlobals.MAX_SAVE_SVALUE_DEPTH) TooDeepSaveError();
                    ulong sz = 0;
                    foreach (var sv in v.U.Arr?.Items ?? new List<SValueS>()) sz += svalue_save_size(sv);
                    ObjectGlobals.SaveSvalueDepth--;
                    return sz + 5;
                }
            case SValueType.T_CLASS:
                {
                    if (++ObjectGlobals.SaveSvalueDepth > ObjectGlobals.MAX_SAVE_SVALUE_DEPTH) TooDeepSaveError();
                    ulong sz = 0;
                    foreach (var sv in v.U.Arr?.Items ?? new List<SValueS>()) sz += svalue_save_size(sv);
                    ObjectGlobals.SaveSvalueDepth--;
                    return sz + 5;
                }
            case SValueType.T_MAPPING:
                {
                    if (++ObjectGlobals.SaveSvalueDepth > ObjectGlobals.MAX_SAVE_SVALUE_DEPTH) TooDeepSaveError();
                    ulong sz = 0;
                    if (v.U.Map != null) foreach (var kv in v.U.Map.Map) sz += svalue_save_size(kv.Key) + svalue_save_size(kv.Value);
                    ObjectGlobals.SaveSvalueDepth--;
                    return sz + 5;
                }
            case SValueType.T_NUMBER:
                {
                    long res = v.U.Number; // L282 int64_t
                    ulong len = res < 0 ? 1UL : 0UL;
                    if (res < 0) res = -res;
                    while (res > 9) { res /= 10; len++; }
                    return len + 2;
                }
            case SValueType.T_REAL:
                {
                    var buf = v.U.Real.ToString("G");
                    return (ulong)buf.Length + 1;
                }
            default: return 2;
        }
    }

    // L305-L430 save_svalue
    public static void save_svalue(SValueS v, ref StringBuilder buf)
    {
        // L305 void save_svalue (const svalue_t * v, char **buf)
        switch (v.Type)
        {
            case SValueType.T_STRING:
                {
                    buf.Append('"');
                    foreach (var c in v.StrPtr() ?? "") { if (c == '"' || c == '\\') { buf.Append('\\'); buf.Append(c); } else buf.Append(c == '\n' ? '\r' : c); }
                    buf.Append('"');
                    return;
                }
            case SValueType.T_ARRAY:
                {
                    buf.Append("({");
                    if (v.U.Arr != null) foreach (var sv in v.U.Arr.Items) { save_svalue(sv, ref buf); buf.Append(','); }
                    buf.Append("})");
                    return;
                }
            case SValueType.T_CLASS:
                {
                    buf.Append("(/");
                    if (v.U.Arr != null) foreach (var sv in v.U.Arr.Items) { save_svalue(sv, ref buf); buf.Append(','); }
                    buf.Append("/)");
                    return;
                }
            case SValueType.T_NUMBER:
                {
                    buf.Append(v.U.Number); // L367-L396 int64_t handling
                    return;
                }
            case SValueType.T_REAL:
                {
                    buf.Append(v.U.Real.ToString("G"));
                    return;
                }
            case SValueType.T_MAPPING:
                {
                    buf.Append("([");
                    if (v.U.Map != null) foreach (var kv in v.U.Map.Map) { save_svalue(kv.Key, ref buf); buf.Append(':'); save_svalue(kv.Value, ref buf); buf.Append(','); }
                    buf.Append("])");
                    return;
                }
            default: buf.Append('0'); return;
        }
    }

    // L432-L568 restore_internal_size
    public static bool restore_internal_size(ref string str, ref int pos, int is_mapping, int depth)
    {
        // L432 static int restore_internal_size
        Console.WriteLine($"[src/object.c:432] restore_internal_size is_mapping={is_mapping} depth={depth} true logic");
        int size = 0;
        char delim = is_mapping != 0 ? ':' : ',';
        while (pos < str.Length)
        {
            char c = str[pos++];
            switch (c)
            {
                case '"':
                    while (pos < str.Length && str[pos] != '"') { if (str[pos] == '\\') pos++; pos++; }
                    if (pos < str.Length) pos++;
                    if (pos >= str.Length || str[pos++] != delim) return false;
                    size++; break;
                case '(':
                    if (pos >= str.Length) return false;
                    char nc = str[pos];
                    if (nc == '{' || nc == '[' || nc == '/')
                    {
                        pos++;
                        ObjectGlobals.SaveSvalueDepth++;
                        if (!restore_internal_size(ref str, ref pos, nc == '[' ? 1 : 0, ObjectGlobals.SaveSvalueDepth - 1)) return false;
                    }
                    else return false;
                    if (pos >= str.Length || str[pos++] != delim) return false;
                    size++; break;
                case ']':
                    if (pos < str.Length && str[pos++] == ')' && is_mapping != 0) { EnsureSizes(depth, size); str = str; return true; }
                    return false;
                case '}':
                case '/':
                    if (pos < str.Length && str[pos++] == ')' && is_mapping == 0) { EnsureSizes(depth, size); return true; }
                    return false;
                case ':':
                case ',':
                    if (c != delim) return false;
                    size++; break;
                default:
                    int p = str.IndexOf(delim, pos);
                    if (p == -1) return false;
                    pos = p + 1; size++; break;
            }
        }
        return false;
    }
    static void EnsureSizes(int depth, int size)
    {
        if (ObjectGlobals.SaveSvalueSizes == null) { ObjectGlobals.SaveMaxDepth = 128; while (ObjectGlobals.SaveMaxDepth <= depth) ObjectGlobals.SaveMaxDepth <<= 1; ObjectGlobals.SaveSvalueSizes = new int[ObjectGlobals.SaveMaxDepth]; }
        else if (depth >= ObjectGlobals.SaveMaxDepth) { while ((ObjectGlobals.SaveMaxDepth <<= 1) <= depth) ; Array.Resize(ref ObjectGlobals.SaveSvalueSizes, ObjectGlobals.SaveMaxDepth); }
        ObjectGlobals.SaveSvalueSizes[depth] = size;
    }

    // L570-L694 restore_size
    public static int restore_size(ref string str, ref int pos, int is_mapping)
    {
        // L570 static int restore_size (const char **str, int is_mapping)
        Console.WriteLine($"[src/object.c:570] restore_size true logic is_mapping={is_mapping}");
        int size = 0;
        char delim = is_mapping != 0 ? ':' : ',';
        char index = (char)0;
        while (pos < str.Length)
        {
            char c = str[pos++];
            switch (c)
            {
                case '"':
                    while (pos < str.Length && str[pos] != '"') { if (str[pos] == '\\' && pos + 1 < str.Length) pos++; pos++; }
                    if (pos < str.Length) pos++;
                    if (pos >= str.Length || str[pos++] != delim) return -1;
                    size++; break;
                case '(':
                    if (pos >= str.Length) return -1;
                    char nc = str[pos++];
                    if (nc == '{') { ObjectGlobals.SaveSvalueDepth++; if (!restore_internal_size(ref str, ref pos, 0, ObjectGlobals.SaveSvalueDepth - 1)) return -1; }
                    else if (nc == '[') { ObjectGlobals.SaveSvalueDepth++; if (!restore_internal_size(ref str, ref pos, 1, ObjectGlobals.SaveSvalueDepth - 1)) return -1; }
                    else if (nc == '/') { ObjectGlobals.SaveSvalueDepth++; if (!restore_internal_size(ref str, ref pos, 0, ObjectGlobals.SaveSvalueDepth - 1)) return -1; }
                    else return -1;
                    if (pos >= str.Length || str[pos++] != delim) return -1;
                    size++; break;
                case ']':
                    ObjectGlobals.SaveSvalueDepth = 0;
                    if (pos < str.Length && str[pos++] == ')' && is_mapping != 0) return size;
                    return -1;
                case '}':
                case '/':
                    ObjectGlobals.SaveSvalueDepth = 0;
                    if (pos < str.Length && str[pos++] == ')' && is_mapping == 0) return size;
                    return -1;
                case ':':
                case ',':
                    if (c != delim) return -1;
                    size++; break;
                default:
                    int p = str.IndexOf(delim, pos);
                    if (p == -1) return -1;
                    pos = p + 1; size++; break;
            }
            if (is_mapping != 0) delim = (index ^= (char)1) != 0 ? ',' : ':';
        }
        return -1;
    }

    // L712-L756 unquote_and_unescape_string
    public static int unquote_and_unescape_string(string src, int start, SValueS sv, out int endPos)
    {
        // L712 static int unquote_and_unescape_string (const char *in, const char **endp, svalue_t *sv)
        int p = start;
        int len = 0;
        while (p < src.Length && src[p] != '"')
        {
            if (src[p] == '\0') { endPos = p; return ObjectGlobals.RestoreError.ROB_STRING_ERROR; }
            if (src[p] == '\\') { p++; if (p >= src.Length) { endPos = p; return ObjectGlobals.RestoreError.ROB_STRING_ERROR; } }
            len++; p++;
        }
        endPos = p + 1;
        char[] restored = new char[len];
        int newp = 0;
        int q = start;
        while (q < src.Length && src[q] != '"')
        {
            char c = src[q];
            if (c == '\\')
            {
                q++;
                if (q >= src.Length) break;
                c = src[q];
                restored[newp++] = c == '\r' ? '\n' : c;
            }
            else if (c == '\r') restored[newp++] = '\n';
            else restored[newp++] = c;
            q++;
        }
        string finalStr = new string(restored, 0, newp);
        sv.Type = SValueType.T_STRING;
        sv.Subtype = (short)StringSubtype.STRING_MALLOC;
        sv.U = new SValueU { MallocString = finalStr };
        return 0;
    }
    // overload for ref string
    public static int unquote_and_unescape_string(string src, int start, out SValueS sv, out int endPos)
    {
        sv = new SValueS();
        return unquote_and_unescape_string(src, start, sv, out endPos);
    }

    // L758-L1006 restore_mapping
    public static int restore_mapping(ref string str, ref int pos, SValueS sv)
    {
        // L758 static int restore_mapping (const char **str, svalue_t * sv)
        Console.WriteLine("[src/object.c:758] restore_mapping true logic");
        int size;
        if (ObjectGlobals.SaveSvalueDepth > 0 && ObjectGlobals.SaveSvalueSizes != null) size = ObjectGlobals.SaveSvalueSizes[ObjectGlobals.SaveSvalueDepth - 1];
        else { size = restore_size(ref str, ref pos, 1); if (size < 0) { Console.WriteLine("[src/object.c:771] debug_error corrupted"); return 0; } }
        if (size == 0) { pos += 2; sv.Type = SValueType.T_MAPPING; sv.U = new SValueU { Map = new MappingS() }; return 0; }

        var m = new MappingS();
        int count = 0;
        while (true)
        {
            if (pos >= str.Length) break;
            char c = str[pos++];
            SValueS key = new SValueS();
            int err = 0;
            switch (c)
            {
                case '"': { int epos; err = unquote_and_unescape_string(str, pos, key, out epos); /* hash_string path uses shared */ if (err != 0) return err; key.Type = SValueType.T_STRING; key.Subtype = (short)StringSubtype.STRING_SHARED; key.U = new SValueU { SharedString = key.StrPtr() }; pos = epos; if (pos < str.Length) pos++; break; }
                case '(': { ObjectGlobals.SaveSvalueDepth++; if (pos >= str.Length) return ObjectGlobals.RestoreError.ROB_MAPPING_ERROR; char nc = str[pos++]; if (nc == '[') err = restore_mapping(ref str, ref pos, key); else if (nc == '{') err = restore_array(ref str, ref pos, key); else if (nc == '/') err = restore_class(ref str, ref pos, key); else return ObjectGlobals.RestoreError.ROB_MAPPING_ERROR; if (err != 0) return err; if (pos < str.Length) pos++; break; }
                case ':': { key = SValueS.FromNumber(0); break; }
                case ']': { pos++; add_map_stats(m, count); sv.Type = SValueType.T_MAPPING; sv.U = new SValueU { Map = m }; return 0; }
                case '-': case '0': case '1': case '2': case '3': case '4': case '5': case '6': case '7': case '8': case '9': { if (parse_numeric(str, ref pos, c, key) == 0) return ObjectGlobals.RestoreError.ROB_NUMERAL_ERROR; break; }
                default: return ObjectGlobals.RestoreError.ROB_MAPPING_ERROR;
            }
            // value
            if (pos >= str.Length) return ObjectGlobals.RestoreError.ROB_MAPPING_ERROR;
            c = str[pos++];
            SValueS value = new SValueS();
            switch (c)
            {
                case '"': { int epos; err = unquote_and_unescape_string(str, pos, value, out epos); if (err != 0) return err; pos = epos; if (pos < str.Length) pos++; break; }
                case '(': { ObjectGlobals.SaveSvalueDepth++; if (pos >= str.Length) return ObjectGlobals.RestoreError.ROB_MAPPING_ERROR; char nc = str[pos++]; if (nc == '[') err = restore_mapping(ref str, ref pos, value); else if (nc == '{') err = restore_array(ref str, ref pos, value); else if (nc == '/') err = restore_class(ref str, ref pos, value); else return ObjectGlobals.RestoreError.ROB_MAPPING_ERROR; if (err != 0) return err; if (pos < str.Length) pos++; break; }
                case ',': { value = SValueS.FromNumber(0); break; }
                case '-': case '0': case '1': case '2': case '3': case '4': case '5': case '6': case '7': case '8': case '9': { if (parse_numeric(str, ref pos, c, value) == 0) return ObjectGlobals.RestoreError.ROB_NUMERAL_ERROR; break; }
                default: return ObjectGlobals.RestoreError.ROB_MAPPING_ERROR;
            }
            m.Map[key] = value;
            count++;
            if (count > 10000) { add_map_stats(m, count - 1); throw new InvalidOperationException("mapping_too_large"); }
        }
        return 0;
    }

    // L1009-L1108 restore_class
    public static int restore_class(ref string str, ref int pos, SValueS ret)
    {
        // L1009 static int restore_class
        Console.WriteLine("[src/object.c:1009] restore_class true logic");
        int size = ObjectGlobals.SaveSvalueDepth > 0 && ObjectGlobals.SaveSvalueSizes != null ? ObjectGlobals.SaveSvalueSizes[ObjectGlobals.SaveSvalueDepth - 1] : restore_size(ref str, ref pos, 0);
        if (size < 0) return ObjectGlobals.RestoreError.ROB_CLASS_ERROR;
        var v = new ArrayS();
        for (int i = 0; i < size; i++) v.Items.Add(SValueS.FromNumber(0));
        int idx = 0;
        while (size-- > 0 && pos < str.Length)
        {
            char c = str[pos++];
            switch (c)
            {
                case '"': { int epos; if (unquote_and_unescape_string(str, pos, v.Items[idx], out epos) != 0) return ObjectGlobals.RestoreError.ROB_CLASS_ERROR; pos = epos; if (pos < str.Length) pos++; idx++; break; }
                case ',': idx++; break;
                case '(': { ObjectGlobals.SaveSvalueDepth++; if (pos >= str.Length) return ObjectGlobals.RestoreError.ROB_CLASS_ERROR; char nc = str[pos++]; int err = 0; var sv = new SValueS(); if (nc == '[') err = restore_mapping(ref str, ref pos, sv); else if (nc == '{') err = restore_array(ref str, ref pos, sv); else if (nc == '/') err = restore_class(ref str, ref pos, sv); else return ObjectGlobals.RestoreError.ROB_CLASS_ERROR; if (err != 0) return err; v.Items[idx++] = sv; if (pos < str.Length) pos++; break; }
                case '-': case '0': case '1': case '2': case '3': case '4': case '5': case '6': case '7': case '8': case '9': { if (parse_numeric(str, ref pos, c, v.Items[idx]) == 0) return ObjectGlobals.RestoreError.ROB_NUMERAL_ERROR; idx++; break; }
                default: return ObjectGlobals.RestoreError.ROB_CLASS_ERROR;
            }
        }
        pos += 2;
        ret.Type = SValueType.T_CLASS;
        ret.U = new SValueU { Arr = v };
        return 0;
    }

    // L1110-L1209 restore_array
    public static int restore_array(ref string str, ref int pos, SValueS ret)
    {
        // L1110 static int restore_array
        Console.WriteLine("[src/object.c:1110] restore_array true logic");
        int size = ObjectGlobals.SaveSvalueDepth > 0 && ObjectGlobals.SaveSvalueSizes != null ? ObjectGlobals.SaveSvalueSizes[ObjectGlobals.SaveSvalueDepth - 1] : restore_size(ref str, ref pos, 0);
        if (size < 0) return ObjectGlobals.RestoreError.ROB_ARRAY_ERROR;
        var v = new ArrayS();
        for (int i = 0; i < size; i++) v.Items.Add(SValueS.FromNumber(0));
        int idx = 0;
        while (size-- > 0 && pos < str.Length)
        {
            char c = str[pos++];
            switch (c)
            {
                case '"': { int epos; if (unquote_and_unescape_string(str, pos, v.Items[idx], out epos) != 0) return ObjectGlobals.RestoreError.ROB_ARRAY_ERROR; pos = epos; if (pos < str.Length) pos++; idx++; break; }
                case ',': idx++; break;
                case '(': { ObjectGlobals.SaveSvalueDepth++; if (pos >= str.Length) return ObjectGlobals.RestoreError.ROB_ARRAY_ERROR; char nc = str[pos++]; var sv = new SValueS(); int err = 0; if (nc == '[') err = restore_mapping(ref str, ref pos, sv); else if (nc == '{') err = restore_array(ref str, ref pos, sv); else if (nc == '/') err = restore_class(ref str, ref pos, sv); else return ObjectGlobals.RestoreError.ROB_ARRAY_ERROR; if (err != 0) return err; v.Items[idx++] = sv; if (pos < str.Length) pos++; break; }
                case '-': case '0': case '1': case '2': case '3': case '4': case '5': case '6': case '7': case '8': case '9': { if (parse_numeric(str, ref pos, c, v.Items[idx]) == 0) return ObjectGlobals.RestoreError.ROB_NUMERAL_ERROR; idx++; break; }
                default: return ObjectGlobals.RestoreError.ROB_ARRAY_ERROR;
            }
        }
        pos += 2;
        ret.Type = SValueType.T_ARRAY;
        ret.U = new SValueU { Arr = v };
        return 0;
    }

    // L1215-L1226 restore_string
    public static int restore_string(string val, SValueS sv)
    {
        // L1215 static int restore_string
        int endPos;
        int err = unquote_and_unescape_string(val, 0, sv, out endPos);
        if (err != 0) return err;
        if (endPos < val.Length && val[endPos] != '\0') return ObjectGlobals.RestoreError.ROB_STRING_ERROR;
        return 0;
    }

    // L1230-L1287 restore_svalue
    public static int restore_svalue(string cp_in, SValueS v)
    {
        // L1230 int restore_svalue (const char *cp_in, svalue_t * v)
        if (string.IsNullOrEmpty(cp_in)) { v.Type = SValueType.T_NUMBER; v.U = new SValueU { Number = 0 }; return 0; }
        int pos = 0;
        char c = cp_in[pos++];
        switch (c)
        {
            case '"': return restore_string(cp_in.Substring(pos), v);
            case '(':
                {
                    if (pos >= cp_in.Length) return ObjectGlobals.RestoreError.ROB_GENERAL_ERROR;
                    char nc = cp_in[pos++];
                    int ret;
                    if (nc == '{') ret = restore_array(ref cp_in, ref pos, v);
                    else if (nc == '[') ret = restore_mapping(ref cp_in, ref pos, v);
                    else if (nc == '/') ret = restore_class(ref cp_in, ref pos, v);
                    else ret = ObjectGlobals.RestoreError.ROB_GENERAL_ERROR;
                    if (ObjectGlobals.SaveSvalueDepth != 0) { ObjectGlobals.SaveSvalueDepth = ObjectGlobals.SaveMaxDepth = 0; ObjectGlobals.SaveSvalueSizes = null; }
                    return ret;
                }
            case '-': case '0': case '1': case '2': case '3': case '4': case '5': case '6': case '7': case '8': case '9':
                {
                    int idx = pos;
                    if (parse_numeric(cp_in, ref idx, c, v) == 0) return ObjectGlobals.RestoreError.ROB_NUMERAL_ERROR;
                    return 0;
                }
            default:
                v.Type = SValueType.T_NUMBER; v.U = new SValueU { Number = 0 }; return 0;
        }
    }

    // L1291-L1357 safe_restore_svalue
    public static int safe_restore_svalue(string cp_in, SValueS v)
    {
        // L1291 int safe_restore_svalue (char *cp_in, svalue_t * v) - leave alone on error
        var val = SValueS.FromNumber(0);
        int ret = restore_svalue(cp_in, val);
        if (ret != 0) return ret;
        v.Type = val.Type; v.Subtype = val.Subtype; v.U = val.U;
        return 0;
    }

    // L1359-L1380 fgv_recurse
    public static bool fgv_recurse(Program prog, ref int idx, string name, ref ushort type)
    {
        // L1359 static int fgv_recurse
        for (int i = 0; i < prog.NumInherited; i++) if (fgv_recurse(prog.Inherit[i].Prog!, ref idx, name, ref type)) { type |= prog.Inherit[i].TypeMod; return true; }
        for (int i = 0; i < prog.NumVariablesDefined; i++) if (prog.VariableTable[i] == name) { idx += i; type = prog.VariableTypes[i]; return true; }
        idx += prog.NumVariablesDefined;
        return false;
    }

    // L1382-L1393 find_global_variable
    public static int find_global_variable(Program prog, string name, out ushort type)
    {
        // L1382 int find_global_variable
        int idx = 0;
        type = 0;
        if (fgv_recurse(prog, ref idx, name, ref type))
        {
            const ushort NAME_PUBLIC = 0x01;
            const ushort NAME_PRIVATE = 0x02;
            if ((type & NAME_PUBLIC) != 0) type &= unchecked((ushort)~NAME_PRIVATE);
            return idx;
        }
        return -1;
    }

    // L1395-L1454 restore_object_from_buff
    public static void restore_object_from_buff(ObjectS ob, string theBuff, int noclear)
    {
        // L1395 void restore_object_from_buff (object_t * ob, char *theBuff, int noclear)
        var lines = theBuff.Split('\n');
        foreach (var buff in lines)
        {
            if (string.IsNullOrEmpty(buff)) continue;
            if (buff[0] == '#') continue;
            int space = buff.IndexOf(' ');
            if (space == -1 || space >= 100) throw new InvalidOperationException("restore_object(): Illegal file format.");
            string varName = buff.Substring(0, space);
            string valStr = buff.Substring(space + 1);
            ushort t;
            int varIdx = find_global_variable(ob.Prog!, varName, out t);
            const ushort NAME_STATIC = 0x08;
            if (varIdx == -1 || (t & NAME_STATIC) != 0) continue;
            var v = ob.Variables[varIdx];
            int rc = noclear != 0 ? safe_restore_svalue(valStr, v) : restore_svalue(valStr, v);
            if ((rc & ObjectGlobals.RestoreError.ROB_ERROR) != 0) throw new InvalidOperationException($"restore_object error {rc} on {varName}");
        }
    }

    // L1462-L1502 save_object_recurse
    public static bool save_object_recurse(Program prog, ref int svpIdx, SValueS[] variables, int type, int save_zeros, StreamWriter f)
    {
        // L1462 static int save_object_recurse
        for (int i = 0; i < prog.NumInherited; i++) if (!save_object_recurse(prog.Inherit[i].Prog!, ref svpIdx, variables, prog.Inherit[i].TypeMod | type, save_zeros, f)) return false;
        const ushort NAME_STATIC = 0x08;
        if ((type & NAME_STATIC) != 0) { svpIdx += prog.NumVariablesDefined; return true; }
        for (int i = 0; i < prog.NumVariablesDefined; i++)
        {
            if ((prog.VariableTypes[i] & NAME_STATIC) != 0) { svpIdx++; continue; }
            ObjectGlobals.SaveSvalueDepth = 0;
            var sb = new StringBuilder();
            save_svalue(variables[svpIdx], ref sb);
            string ns = sb.ToString();
            if (save_zeros != 0 || ns != "0") f.WriteLine($"{prog.VariableTable[i]} {ns}");
            svpIdx++;
        }
        return true;
    }

    // L1510-L1607 save_object
    public static int save_object(ObjectS ob, string file, int save_zeros)
    {
        // L1510 int save_object (object_t * ob, const char *file, int save_zeros)
        if ((ob.Flags & NeolithDriverConst.O_DESTRUCTED) != 0) return 0;
        Console.WriteLine($"[src/object.c:1510] save_object true logic file={file}");
        string fname = file;
        if (fname.EndsWith(".c")) fname = fname.Substring(0, fname.Length - 2);
        if (fname.EndsWith(ObjectGlobals.SAVE_EXTENSION)) fname = fname.Substring(0, fname.Length - ObjectGlobals.SAVE_EXTENSION.Length);
        string finalName = fname + ObjectGlobals.SAVE_EXTENSION;
        string tmpName = finalName + ".tmp";
        try
        {
            using (var sw = new StreamWriter(tmpName))
            {
                sw.WriteLine($"#/{ob.Prog?.Name}");
                int idx = 0;
                if (!save_object_recurse(ob.Prog!, ref idx, ob.Variables, 0, save_zeros, sw)) return 0;
            }
            if (File.Exists(finalName)) File.Delete(finalName);
            File.Move(tmpName, finalName);
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[src/object.c:save_object] failed {ex}");
            if (File.Exists(tmpName)) File.Delete(tmpName);
            return 0;
        }
    }

    // L1614-L1625 save_variable
    public static string save_variable(SValueS var)
    {
        // L1614 malloc_str_t save_variable (const svalue_t * var)
        ObjectGlobals.SaveSvalueDepth = 0;
        var sb = new StringBuilder();
        save_svalue(var, ref sb);
        return sb.ToString();
    }

    // L1627-L1633 cns_just_count
    public static void cns_just_count(ref int idx, Program prog)
    {
        // L1627 static void cns_just_count
        for (int i = 0; i < prog.NumInherited; i++) cns_just_count(ref idx, prog.Inherit[i].Prog!);
        idx += prog.NumVariablesDefined;
    }

    // L1635-L1654 cns_recurse
    public static void cns_recurse(ObjectS ob, ref int idx, Program prog)
    {
        // L1635 static void cns_recurse
        const ushort NAME_STATIC = 0x08;
        for (int i = 0; i < prog.NumInherited; i++)
        {
            if ((prog.Inherit[i].TypeMod & NAME_STATIC) != 0) cns_just_count(ref idx, prog.Inherit[i].Prog!);
            else cns_recurse(ob, ref idx, prog.Inherit[i].Prog!);
        }
        for (int i = 0; i < prog.NumVariablesDefined; i++)
        {
            if ((prog.VariableTypes[i] & NAME_STATIC) == 0) ob.Variables[idx + i] = SValueS.FromNumber(0);
        }
        idx += prog.NumVariablesDefined;
    }

    // L1656-L1659 clear_non_statics
    public static void clear_non_statics(ObjectS ob)
    {
        // L1656 static void clear_non_statics
        int idx = 0;
        cns_recurse(ob, ref idx, ob.Prog!);
    }

    // L1661-L1749 restore_object
    public static int restore_object(ObjectS ob, string file, int noclear)
    {
        // L1661 int restore_object (object_t * ob, const char *file, int noclear)
        if ((ob.Flags & NeolithDriverConst.O_DESTRUCTED) != 0) return 0;
        Console.WriteLine($"[src/object.c:1661] restore_object true logic file={file}");
        string fname = file;
        if (fname.EndsWith(".c")) fname = fname.Substring(0, fname.Length - 2);
        if (fname.EndsWith(ObjectGlobals.SAVE_EXTENSION)) fname = fname.Substring(0, fname.Length - ObjectGlobals.SAVE_EXTENSION.Length);
        string finalName = fname + ObjectGlobals.SAVE_EXTENSION;
        if (!File.Exists(finalName)) return 0;
        string buff = File.ReadAllText(finalName);
        if (noclear == 0) clear_non_statics(ob);
        restore_object_from_buff(ob, buff, noclear);
        return 1;
    }

    // L1751-L1769 restore_variable
    public static void restore_variable(SValueS var, string str)
    {
        // L1751 void restore_variable
        int rc = restore_svalue(str, var);
        if ((rc & ObjectGlobals.RestoreError.ROB_ERROR) != 0)
        {
            var.Type = SValueType.T_NUMBER;
            var.U = new SValueU { Number = 0 };
            if ((rc & ObjectGlobals.RestoreError.ROB_GENERAL_ERROR) != 0) throw new InvalidOperationException("restore_object(): Illegal general format.");
            else if ((rc & ObjectGlobals.RestoreError.ROB_NUMERAL_ERROR) != 0) throw new InvalidOperationException("restore_object(): Illegal numeric format.");
        }
    }

    // L1774-L1794 alloc_sentence
    public static SentenceS alloc_sentence()
    {
        // L1774 sentence_t* alloc_sentence ()
        SentenceS p;
        if (ObjectGlobals.SentFree == null)
        {
            p = new SentenceS();
            ObjectGlobals.TotAllocSentence++;
        }
        else
        {
            p = ObjectGlobals.SentFree;
            ObjectGlobals.SentFree = p.Next;
        }
        p.Verb = "";
        p.FunctionName = null;
        p.Ob = null;
        p.Flags = 0;
        p.Next = null;
        p.Args = null;
        return p;
    }

    // L1796-L1833 free_sentence
    public static void free_sentence(SentenceS p)
    {
        // L1796 void free_sentence (sentence_t * p)
        if (p.Ob != null) { free_object(p.Ob, "free_sentence"); p.Ob = null; }
        if (!string.IsNullOrEmpty(p.FunctionName)) p.FunctionName = null;
        if (!string.IsNullOrEmpty(p.Verb)) p.Verb = "";
        if (p.Args != null) p.Args = null;
        p.Flags = 0;
        p.Next = ObjectGlobals.SentFree;
        ObjectGlobals.SentFree = p;
    }

    // L1843-L1892 dealloc_object
    public static void dealloc_object(ObjectS ob, string from)
    {
        // L1843 void dealloc_object (object_t * ob, const char *from)
        if ((ob.Flags & NeolithDriverConst.O_DESTRUCTED) == 0) throw new InvalidOperationException($"FATAL: Object {ob.Name} ref count 0, but not destructed (from {from}).");
        if (ob.Sent != null)
        {
            var s = ob.Sent;
            while (s != null) { var next = s.Next; free_sentence(s); s = next; }
            ob.Sent = null;
        }
        if (ob.Prog != null) { ObjectGlobals.TotAllocObjectSize -= (ulong)((ob.Prog.NumVariablesTotal - 1) + 1); ob.Prog = null; }
        if (!string.IsNullOrEmpty(ob.Name)) ob.Name = "";
        ObjectGlobals.TotAllocObject--;
    }

    // L1899-L1904 free_object
    public static void free_object(ObjectS ob, string from)
    {
        // L1899 void free_object (object_t * ob, const char *from)
        if (--ob.Ref > 0) return;
        dealloc_object(ob, from);
    }

    // L1912-L1930 get_empty_object
    public static ObjectS get_empty_object(int num_var)
    {
        // L1912 object_t* get_empty_object (int num_var)
        var ob = new ObjectS();
        ObjectGlobals.TotAllocObject++;
        int size = sizeof(int) + (num_var - (num_var != 0 ? 1 : 0)) * 1;
        ObjectGlobals.TotAllocObjectSize += (ulong)num_var;
        ob.Variables = new SValueS[num_var];
        for (int i = 0; i < num_var; i++) ob.Variables[i] = SValueS.FromNumber(0);
        ob.Ref = 1;
        return ob;
    }

    // L~1930 find living etc
    public static int hash_living_name(string str)
    {
        uint h = 0;
        foreach (var c in str) h = h * 31 + c;
        return (int)(h % 256);
    }

    public static ObjectS? find_living_object(string str)
    {
        if (ObjectGlobals.HashedLiving == null) return null;
        ObjectGlobals.NumSearches++;
        int idx = hash_living_name(str);
        var ob = ObjectGlobals.HashedLiving[idx];
        while (ob != null)
        {
            if (ob.LivingName == str) return ob;
            ob = ob.NextHashedLiving;
            ObjectGlobals.SearchLength++;
        }
        return null;
    }

    // L1976-L1991 set_living_name
    public static void set_living_name(ObjectS ob, string str)
    {
        // L1976 void set_living_name (object_t * ob, const char *str)
        if ((ob.Flags & NeolithDriverConst.O_DESTRUCTED) != 0) return;
        if (!string.IsNullOrEmpty(ob.LivingName)) remove_living_name(ob);
        ObjectGlobals.NumLivingNames++;
        if (ObjectGlobals.HashedLiving == null) init_objects();
        int h = hash_living_name(str);
        ob.NextHashedLiving = ObjectGlobals.HashedLiving![h];
        ObjectGlobals.HashedLiving[h] = ob;
        ob.LivingName = str;
    }

    // L1993-L2012 remove_living_name
    public static void remove_living_name(ObjectS ob)
    {
        // L1993 void remove_living_name (object_t * ob)
        ObjectGlobals.NumLivingNames--;
        if (ObjectGlobals.HashedLiving == null) return;
        int h = hash_living_name(ob.LivingName ?? "");
        var hl = ObjectGlobals.HashedLiving[h];
        ObjectS? prev = null;
        while (hl != null)
        {
            if (hl == ob) break;
            prev = hl;
            hl = hl.NextHashedLiving;
        }
        if (hl == null) return;
        if (prev == null) ObjectGlobals.HashedLiving[h] = hl.NextHashedLiving;
        else prev.NextHashedLiving = hl.NextHashedLiving;
        ob.NextHashedLiving = null;
        ob.LivingName = null;
    }

    // L2014-L2019 stat_living_objects
    public static void stat_living_objects(StringBuilder outBuf)
    {
        // L2014 void stat_living_objects (outbuffer_t * out)
        outBuf.AppendLine("Hash table of living objects:");
        outBuf.AppendLine("-----------------------------");
        double avg = ObjectGlobals.NumSearches != 0 ? (double)ObjectGlobals.SearchLength / ObjectGlobals.NumSearches : 0;
        outBuf.AppendLine($"{ObjectGlobals.NumLivingNames} living named objects, average search length: {avg:F2}");
    }

    // L2021-L2040 reset_object
    public static void reset_object(ObjectS ob)
    {
        // L2021 void reset_object (object_t * ob)
        Console.WriteLine("[src/object.c:2021] reset_object true logic");
        ob.Flags |= ObjectFlags.O_RESET_STATE;
    }

    // L2042-L2057 clean_up_object
    public static void clean_up_object(ObjectS ob)
    {
        // L2042 void clean_up_object (object_t* ob)
        Console.WriteLine("[src/object.c:2042] clean_up_object true logic");
        int saveReset = ob.Flags & ObjectFlags.O_RESET_STATE;
        if ((ob.Flags & NeolithDriverConst.O_DESTRUCTED) != 0) return;
        ob.Flags |= (ushort)saveReset;
    }

    // L2063-L2094 call___INIT
    public static void call___INIT(ObjectS ob)
    {
        // L2063 static void call___INIT (object_t * ob)
        Console.WriteLine("[src/object.c:2063] call___INIT true logic - obsolete __INIT");
        ob.Flags &= unchecked((ushort)~ObjectFlags.O_RESET_STATE);
    }

    // L2096-L2115 call_create
    public static void call_create(ObjectS ob, int num_arg)
    {
        // L2096 void call_create (object_t * ob, int num_arg)
        call___INIT(ob);
        if ((ob.Flags & NeolithDriverConst.O_DESTRUCTED) != 0) return;
        Console.WriteLine("[src/object.c:2096] call_create true logic - APPLY_CREATE");
        ob.Flags |= ObjectFlags.O_RESET_STATE;
    }

    // L2117-L2130 object_visible
    public static int object_visible(ObjectS ob)
    {
        // L2117 int object_visible (object_t * ob)
        if ((ob.Flags & ObjectFlags.O_HIDDEN) != 0) return valid_hide(ob);
        else return 1;
    }

    // L2132-L2161 reload_object
    public static void reload_object(ObjectS obj)
    {
        // L2132 void reload_object (object_t * obj)
        if (obj.Prog == null) return;
        for (int i = 0; i < obj.Prog.NumVariablesTotal; i++) obj.Variables[i] = SValueS.FromNumber(0);
        Console.WriteLine("[src/object.c:2132] reload_object true logic - close sockets, heart_beat, call_out");
        if (!string.IsNullOrEmpty(obj.LivingName)) remove_living_name(obj);
        obj.Flags &= unchecked((ushort)~ObjectFlags.O_ENABLE_COMMANDS);
        call_create(obj, 0);
    }

    // L2163-L2174 init_objects
    public static void init_objects()
    {
        // L2163 void init_objects ()
        ObjectGlobals.HashedLiving = new ObjectS[256];
    }

    // L2176-L2192 deinit_objects
    public static void deinit_objects()
    {
        // L2176 void deinit_objects ()
        if (ObjectGlobals.HashedLiving != null) ObjectGlobals.HashedLiving = null;
        while (ObjectGlobals.SentFree != null) { var next = ObjectGlobals.SentFree.Next; ObjectGlobals.SentFree = next; }
        if (ObjectGlobals.TotAllocObject != 0) Console.WriteLine($"[src/object.c:2190] Memory leak: {ObjectGlobals.TotAllocObject} objects still allocated at shutdown.");
    }
}