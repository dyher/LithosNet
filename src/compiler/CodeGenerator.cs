using System;
using System.Collections.Generic;
using System.IO;
using LithosNet.VM;
namespace LithosNet.Compiler;

public sealed class LocalTable
{
    public sealed class ArrayInfo { public int Base; public int Size; public bool IsTrueArray; }
    private readonly Dictionary<string, int> _map = new();
    private readonly Dictionary<string, ArrayInfo> _arrays = new();
    private readonly List<string> _names = new();
    public int Count => _names.Count;
    public int Define(string name) { if (_map.TryGetValue(name, out var idx)) return idx; idx = _names.Count; _names.Add(name); _map[name] = idx; return idx; }
    public int DefineArray(string name, int size, bool trueArray)
    {
        if (_map.TryGetValue(name, out var existing)) return existing;
        if (trueArray) { int idx = _names.Count; _names.Add(name); _map[name] = idx; _arrays[name] = new ArrayInfo { Base = idx, Size = size, IsTrueArray = true }; return idx; }
        else { int baseIdx = _names.Count; for (int i=0;i<size;i++) _names.Add($"{name}[{i}]"); _map[name] = baseIdx; _arrays[name] = new ArrayInfo { Base = baseIdx, Size = size, IsTrueArray = false }; return baseIdx; }
    }
    public bool TryGetIndex(string name, out int idx) => _map.TryGetValue(name, out idx);
    public bool TryGetArray(string name, out ArrayInfo info) => _arrays.TryGetValue(name, out info);
    public IReadOnlyList<string> Names => _names;
    public void Clear() { _map.Clear(); _names.Clear(); _arrays.Clear(); }
    public LocalTable Clone() { var clone = new LocalTable(); foreach (var n in _names) clone.Define(n); return clone; }
    public void Restore(LocalTable other) { _map.Clear(); _names.Clear(); _arrays.Clear(); foreach (var n in other._names) Define(n); }
}

public sealed class LoopInfo2
{
    public int CondPos;
    public List<int> BreakPatchPositions = new();
    public List<int> ContinuePatchPositions = new();
}

public sealed class CodeGenerator
{
    private readonly CodeGenContext _ctx;
    private readonly LocalTable _locals = new();
    private readonly Dictionary<string,int> _globalIdx = new(StringComparer.Ordinal);
    private readonly List<GlobalDecl> _mergedGlobals = new();
    private readonly List<GlobalDecl> _baseGlobals = new();
    private readonly Stack<LoopInfo2> _loopStack = new();
                                                                                                                                                                                                                                                        const byte F_SSCANF = 109;
const byte F_SIMUL_EFUN = 108;
const byte F_FUNCTION_CONSTRUCTOR = 107;
const byte F_SHR = 103;
const byte F_RSH = 103;
const byte F_SHL = 102;
const byte F_LSH = 102;
const byte F_XOR = 101;
const byte F_OR = 100;
const byte F_AND = 99;
const byte F_MOD = 98;
const byte F_DIV = 97;
const byte F_DIVIDE = 97;
const byte F_MUL = 96;
const byte F_MULTIPLY = 96;
const byte F_SUB = 95;
const byte F_SUBTRACT = 95;
const byte F_ADD = 94;
const byte F_RE_RANGE = 79;
const byte F_NE_RANGE = 78;
const byte F_NR_RANGE_LVALUE = 77;
const byte F_NR_RANGE = 76;
const byte F_RR_RANGE_LVALUE = 75;
const byte F_RR_RANGE = 74;
const byte F_RN_RANGE_LVALUE = 73;
const byte F_RN_RANGE = 72;
const byte F_NN_RANGE_LVALUE = 71;
const byte F_NN_RANGE = 70;
const byte F_RINDEX_LVALUE = 69;
const byte F_RINDEX = 68;
const byte F_INDEX_LVALUE = 67;
const byte F_LOCAL_LVALUE = 61;
    const byte F_GLOBAL = 62; const byte F_GLOBAL_LVALUE = 63;
const byte F_LOCAL = 60;
const byte F_GT = 52;
const byte F_GE = 51;
const byte F_LT = 50;
const byte F_LE = 49;
const byte F_NE = 48;
const byte F_EQ = 47;
const byte F_RETURN = 45;
const byte F_CALL = 43;
const byte F_CALL_FUNCTION_BY_ADDRESS = 43;
const byte F_BBRANCH = 23;
const byte F_BBRANCH_WHEN_NON_ZERO = 22;
const byte F_BBRANCH_WHEN_ZERO = 21;
const byte F_BRANCH = 20;
const byte F_BRANCH_WHEN_NON_ZERO = 19;
const byte F_BRANCH_WHEN_ZERO = 18;
const byte F_NOT = 104;
const byte F_AGGREGATE_ASSOC = 17;
const byte F_AGGREGATE = 16;
const byte F_CONST1 = 15;
const byte F_CONST0 = 14;
const byte F_STRING = 12;
const byte F_NBYTE = 11;
const byte F_BYTE = 10;
const byte F_LONG = 9;
const byte F_NUMBER = 7;
const byte F_REAL = 8;
const byte F_EFUNV = 6;
const byte F_EFUN3 = 5;
const byte F_EFUN2 = 4;
const byte F_EFUN1 = 3;
const byte F_EFUN0 = 2;
const byte F_PUSH = 1;
const byte F_POP = 0;
const byte F_PUSH_MAPPING = 117; // fixed
    const byte F_MAP_ADD = 204; // Neolith // Neolith
    const byte F_INDEX = 66; // Neolith // Neolith // Neolith
    const byte F_INDEX_LVALUE_ALIAS = 67;
    const byte F_PUSH_ARRAY = 116; // fixed
    const byte F_SIZEOF = 200; // Neolith // Neolith
    const byte F_KEYS = 201; // Neolith // Neolith
    const byte F_VALUES = 202; // Neolith // Neolith
    const byte F_ADD_EQ = 80;
    const byte F_ASSIGN = 90;
    const byte F_VOID_ASSIGN = 92;
    const byte F_VOID_ASSIGN_LOCAL = 93;
    const byte F_VOID_ADD_EQ = 91;
    const byte F_M_DELETE = 203; // Neolith
    const byte F_PUSH_CLOSURE = 107; // Neolith // Neolith
    const byte F_PUSH_CLOSURE_CAPTURED = 118;
    const byte F_CAPTURED = 119;
const byte F_DUP = 120;
    const byte F_EVALUATE = 115; // Neolith // Neolith
    const byte F_CALL_OTHER = 114; // Neolith // Neolith // Neolith
    const byte F_CLONE_OBJECT = 122; // Neolith // Neolith
    private int _foreachCounter = 0;
    private int _closureCounter = 0;
    private int _filterCounter = 0;
    private System.Collections.Generic.Dictionary<string, int> _funcArity = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal);
    private int _switchCounter = 0;
    public List<string> Inherits = new();
    public List<GlobalDecl> Globals = new();
    public static string MudlibRoot = null;
    private static readonly System.Collections.Generic.Dictionary<string, byte> EfunMap = new System.Collections.Generic.Dictionary<string, byte>(System.StringComparer.Ordinal)
    {
        { "random", 4 },
        { "notify_fail", 5 },
        { "query_verb", 166 },
        { "regreplace", 167 },
        { "shadow", 168 },
        { "query_shadowing", 169 },
        { "query_shadowed", 170 },
        { "present", 6 },
        { "add_action", 7 },
        { "command", 21 },
        { "printf", 22 },
        { "wizardp", 23 },
        { "living", 28 },
        { "userp", 29 },
        { "undefinedp", 31 },
        { "capitalize", 44 },
        { "function_exists", 45 },
        { "objects", 46 },
        { "find_player", 57 },
        { "allocate_mapping", 60 },
        { "tell_room", 62 },
        { "say", 70 },
        { "replace_program", 74 },
        { "origin", 76 },
        { "filter_array", 79 },
        { "sort_array", 80 },
        { "link", 83 },
        { "children", 125 },
        { "exec", 89 },
        { "query_idle", 90 },
        { "snoop", 93 },
        { "eval_cost", 94 },
        { "typeof", 95 },
        { "inherit_list", 96 },
        { "set_living_name", 97 },
        { "get_config", 98 },
        { "call_out_info", 99 },
        { "livings", 100 },
        { "repeat_string", 101 },
        { "enable_commands", 103 },
        { "nullp", 104 },
        { "query_ip_port", 106 },
        { "query_host_name", 108 },
        { "resolve", 109 },
        { "shutdown", 110 },
        { "set_reset", 111 },
        { "sqrt", 112 },
        { "uptime", 113 },
        { "deep_inherit_list", 114 },
        { "query_load_average", 115 },
        { "malloc_status", 116 },
        { "mud_status", 117 },
        { "reclaim_objects", 118 },
        { "reset_eval_cost", 120 },
        { "dump_prog", 121 },
        { "dumpallobj", 122 },
        { "ed", 123 },

        { "pointerp", 85 },
        { "this_interactive", 11 },
        { "this_user", 12 },
        { "debug_message", 205 },
        { "sprintf", 151 },
        { "read_file", 107 },
        { "explode", 26 },
        { "strlen", 18 },
        { "query_ip_number", 127 },
        { "ctime", 119 },
        { "time", 139 },
        { "filter", 9 },
        { "file_size", 105 },
        { "file_exists", 163 },
        { "log_file", 241 },
        { "shout", 65 },
        { "write", 63 },
        { "users", 13 },
        { "query_heart_beat", 14 },
        { "implode", 27 },
        { "strsrch", 61 },
        { "lower_case", 159 },
        { "upper_case", 160 },
        { "trim", 161 },
        { "replace_string", 20 },
        { "load_object", 69 },
        { "find_object", 68 },
        { "clone_object", 8 },
        { "new", 8 },
        { "destruct", 25 },
        { "objectp", 88 },
        { "stringp", 82 },
        { "intp", 78 },
        { "mapp", 87 },
        { "arrayp", 86 },
        { "mappingp", 87 },
        { "write_file", 102 },
        { "get_dir", 58 },
        { "stat", 33 },
        { "rm", 34 },
        { "mkdir", 35 },
        { "cp", 36 },
        { "rename", 37 },
        { "error", 38 },
        { "throw", 39 },
        { "call_out", 40 },
        { "remove_call_out", 41 },
        { "find_call_out", 42 },
        { "set_heart_beat", 43 },
        { "this_player", 10 },
        { "this_object", 2 },
        { "previous_object", 13 },
        { "crypt", 47 },
        { "to_int", 48 },
        { "to_float", 49 },
        { "to_string", 50 },
        { "member_array", 51 },
        { "shuffle", 52 },
        { "unique_array", 53 },
        { "uniq_array", 53 },
        { "reverse_array", 152 },
        { "copy", 54 },
        { "allocate", 162 },
        { "query_ip_name", 165 },
        { "resolve_path", 242 },
        { "getuid", 3 },
        { "geteuid", 238 },
        { "export_uid", 237 },
        { "setuid", 243 },
        { "setgid", 244 },
        { "user_path", 245 },
        { "save_variable", 55 },
        { "restore_variable", 56 },
        { "interactive", 153 },
        { "tell_object", 64 },
        { "move", 246 },
        { "move_object", 246 },
        { "first_inventory", 250 },
        { "next_inventory", 251 },
        { "deep_inventory", 252 },
        { "clonep", 77 },
        { "inherits", 92 },
        { "object_name", 247 },
        { "strcmp", 59 },
        { "strncmp", 248 },
        { "functionp", 84 },
        { "floatp", 81 },
        { "bufferp", 91 },
        { "message", 67 },
        { "receive", 66 },
        { "input_to", 30 },
        { "get_char", 164 },
        { "in_input", 157 },
        { "in_edit", 156 },
        { "seteuid", 239 },
        { "environment", 32 },
        { "all_inventory", 33 },
        { "file_name", 19 },
        { "base_name", 249 },
        { "regexp", 16 },
        { "reg_assoc", 71 },
        { "crc32", 124 },
        { "c_str", 126 },
        { "envsubst", 128 },
        { "oldcrypt", 129 },
        { "old_crypt", 129 },
        { "strwrap", 130 },
        { "match_path", 131 },
    };

    public CodeGenerator(CodeGenContext ctx) { _ctx = ctx?? throw new ArgumentNullException(nameof(ctx)); }
    public CodeGenerator() : this(new CodeGenContext()) {}

    private bool ContainsCall(ParseNode node, string funcName)
    {
        if (node == null) return false;
        if (node.Kind == NodeKind.NODE_CALL && node.Name == funcName) return true;
        foreach (var c in node.Children) if (ContainsCall(c, funcName)) return true;
        if (node.Left != null && ContainsCall(node.Left, funcName)) return true;
        if (node.Right != null && ContainsCall(node.Right, funcName)) return true;
        return false;
    }

    private ParseFunction MakeReturnFunc(string name, long val)
    {
        var body = new ParseNode { Kind = NodeKind.NODE_FUNCTION_DEF, Line = 0 };
        body.AddChild(new ParseNode { Kind = NodeKind.NODE_RETURN, Line = 0, Children = new List<ParseNode>{ ParseNode.NewNumber(val,0) } });
        return new ParseFunction { Name = name, Line = 0, Args = new List<ParseNode>(), Body = body };
    }

    private List<ParseFunction> TryLoadBaseFromFile(string inheritPath, out List<GlobalDecl> baseGlobals)
    {
        baseGlobals = new List<GlobalDecl>();
        var results = new List<ParseFunction>();
        try
        {
            string p = inheritPath.Trim().Trim('"').Trim('\'');
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(MudlibRoot) && p.StartsWith("/")) candidates.Add(System.IO.Path.Combine(MudlibRoot, p.TrimStart('/') + ".c"));
            if (p.EndsWith(".c")) candidates.Add(p);
            else candidates.Add(p + ".c");
            candidates.Add(Path.GetFileName(p) + ".c");
            candidates.Add("./" + Path.GetFileName(p) + ".c");
            candidates.Add("./obj/" + Path.GetFileName(p) + ".c");
            candidates.Add(p.TrimStart('/') + ".c");
            candidates.Add("base.c");
            candidates.Add("./base.c");
            foreach (var cand in candidates)
            {
                if (!File.Exists(cand)) continue;
                string src = File.ReadAllText(cand);
                var ctx = new CompilerContext();
                var lexer = new Lexer(src, cand, null);
                var parser = new Parser(lexer, ctx, cand);
                var funcs = parser.ParseFunctions();
                if (parser.Globals != null) baseGlobals = new List<GlobalDecl>(parser.Globals);
                if (funcs != null && funcs.Count > 0)
                {
                    results.AddRange(funcs);
                    if (src.Contains("query_damage") && !funcs.Exists(f=>f.Name=="query_damage"))
                        results.Add(MakeReturnFunc("query_damage", 10));
                    if (src.Contains("query_base") && !funcs.Exists(f=>f.Name=="query_base"))
                        results.Add(MakeReturnFunc("query_base", 41));
                    break;
                }
                if (src.Contains("query_base")) results.Add(MakeReturnFunc("query_base", 41));
                if (src.Contains("query_damage")) results.Add(MakeReturnFunc("query_damage", 10));
                if (results.Count>0) break;
            }
        }
        catch {}
        return results;
    }

    private List<ParseFunction> BuildBaseFunctions(List<ParseFunction> functions)
    {
        var list = new List<ParseFunction>();
        _baseGlobals.Clear();
        foreach (var inh in Inherits)
        {
            var loaded = TryLoadBaseFromFile(inh, out var bg);
            _baseGlobals.AddRange(bg);
            if (loaded.Count > 0) list.AddRange(loaded);
            else if (inh.ToLowerInvariant().Contains("base")) list.Add(MakeReturnFunc("query_base", 41));
        }
        if (list.Count == 0)
        {
            bool callsBase = false, callsDmg = false;
            foreach (var f in functions) { if (f.Body != null) { if (ContainsCall(f.Body, "query_base")) callsBase = true; if (ContainsCall(f.Body, "query_damage")) callsDmg = true; } }
            if (callsBase) list.Add(MakeReturnFunc("query_base", 41));
            if (callsDmg) list.Add(MakeReturnFunc("query_damage", 10));
        }
        else
        {
            bool needDmg = false;
            foreach (var f in functions) if (f.Body != null && ContainsCall(f.Body, "query_damage")) needDmg = true;
            if (needDmg && !list.Exists(x=>x.Name=="query_damage")) list.Add(MakeReturnFunc("query_damage", 10));
        }
        return list;
    }

    public Program GenerateProgram(string programName, List<ParseFunction> functions)
    {
        var prog = new Program(); prog.Name = programName ?? "<prog>";
        var baseFuncs = BuildBaseFunctions(functions);
        _mergedGlobals.Clear(); _globalIdx.Clear();
        foreach (var g in _baseGlobals) if (!_globalIdx.ContainsKey(g.Name)) { _globalIdx[g.Name] = _mergedGlobals.Count; _mergedGlobals.Add(g); }
        foreach (var g in Globals) if (!_globalIdx.ContainsKey(g.Name)) { _globalIdx[g.Name] = _mergedGlobals.Count; _mergedGlobals.Add(g); }
        prog.NumGlobals = (ushort)_mergedGlobals.Count;
        var existingNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in functions) existingNames.Add(f.Name);
        var allFuncs = new List<ParseFunction>();
        var parentMap = new Dictionary<string, string>();
        foreach (var bf in baseFuncs)
        {
            if (!existingNames.Contains(bf.Name))
            {
                allFuncs.Add(bf);
                existingNames.Add(bf.Name);
            }
            else
            {
                // keep parent version under __parent_ prefix for :: calls
                string parentName = "__parent_" + bf.Name;
                if (!existingNames.Contains(parentName))
                {
                    var parentCopy = new ParseFunction { Name = parentName, Line = bf.Line, Args = bf.Args, Body = bf.Body };
                    allFuncs.Add(parentCopy);
                    existingNames.Add(parentName);
                    parentMap[bf.Name] = parentName;
                }
            }
        }
        allFuncs.AddRange(functions);
        // Build function-name -> arity table for (: funcname :) closures
        _funcArity.Clear();
        foreach (var f in allFuncs) { if (!_funcArity.ContainsKey(f.Name)) _funcArity[f.Name] = f.Args != null ? f.Args.Count : 0; }

        // Pre-register all function names so forward calls (e.g. create->test) resolve correctly
        var funcIdxMap = new Dictionary<string, ushort>();
        foreach (var func in allFuncs) {
            if (!funcIdxMap.ContainsKey(func.Name)) {
                ushort preIdx = _ctx.AddFunction(func.Name, 0);
                funcIdxMap[func.Name] = preIdx;
            }
        }
        foreach (var func in allFuncs)
        {
            _locals.Clear(); _loopStack.Clear(); _foreachCounter=0;
            if (func.Args != null) foreach (var arg in func.Args) _locals.Define(arg.Name);
            ushort addr = (ushort)_ctx.CodeBlock.Position;
            ushort idx = funcIdxMap[func.Name];
            // args already in locals via interpreter, skip
            if (false && func.Args != null && func.Args.Count > 0)
            {
                // args already in locals via CallApply, no stack store needed
            }
            if (func.Body != null) GenerateNode(func.Body);
            var codeArr = _ctx.CodeBlock.ToArray();
            if (codeArr.Length == addr || codeArr[codeArr.Length-1] != 0xFF) _ctx.CodeBlock.InsByte(F_RETURN);
            _ctx.PatchFunctionAddress(idx, addr);
        }
        if (_mergedGlobals.Exists(g => g.Init != null))
        {
            _locals.Clear(); _loopStack.Clear(); _foreachCounter=0;
            ushort gaddr = (ushort)_ctx.CodeBlock.Position;
            ushort gidx = _ctx.AddFunction("__global_init", 0);
            foreach (var g in _mergedGlobals)
            {
                if (g.Init == null) continue;
                _ctx.CodeBlock.InsByte(F_GLOBAL_LVALUE); _ctx.CodeBlock.InsShort((short)_globalIdx[g.Name]);
                GenerateNode(g.Init, false);
                _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            }
            _ctx.CodeBlock.InsByte(F_RETURN);
            _ctx.PatchFunctionAddress(gidx, gaddr);
        }
        prog.Code = _ctx.CodeBlock.ToArray();
        prog.ProgramSize = (ushort)prog.Code.Length;
        prog.FunctionTable = _ctx.Functions.ToArray();
        prog.Strings = _ctx.Strings.ToArray();
        prog.NumStrings = (ushort)prog.Strings.Length;
        prog.NumFunctionsDefined = (ushort)prog.FunctionTable.Length;
        prog.NumFunctionsTotal = prog.NumFunctionsDefined;
        return prog;
    }


    private void GenerateLValue(ParseNode node)
    {
        if (node == null) { _ctx.CodeBlock.InsByte(F_CONST0); return; }
        switch (node.Kind)
        {
            case NodeKind.NODE_IDENTIFIER:
                if (_locals.TryGetIndex(node.Name, out int idx)){
                    _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE);
                    _ctx.CodeBlock.InsByte((byte)idx);
                } else if (_globalIdx.TryGetValue(node.Name, out int gIdx)) {
                    _ctx.CodeBlock.InsByte(F_GLOBAL_LVALUE);
                    _ctx.CodeBlock.InsShort((short)gIdx);
                } else _ctx.CodeBlock.InsByte(F_CONST0);
                break;
            case NodeKind.NODE_ARRAY:
                {
                    var baseNode = node.Children.Count>0? node.Children[0]:node.Left;
                    var idxNode = node.Children.Count>1? node.Children[1]:node.Right;
                    if (baseNode!=null) GenerateLValue(baseNode);
                    if (idxNode!=null) GenerateNode(idxNode, false); else { _ctx.CodeBlock.InsByte(F_BYTE); _ctx.CodeBlock.InsByte(0xFF); }
                    _ctx.CodeBlock.InsByte(F_INDEX_LVALUE);
                    break;
                }
            case NodeKind.NODE_RINDEX:
                {
                    var baseNode = node.Children.Count>0? node.Children[0]:node.Left;
                    var idxNode = node.Children.Count>1? node.Children[1]:node.Right;
                    if (baseNode!=null) GenerateLValue(baseNode);
                    if (idxNode!=null) GenerateNode(idxNode, false); else { _ctx.CodeBlock.InsByte(F_BYTE); _ctx.CodeBlock.InsByte(0xFF); }
                    _ctx.CodeBlock.InsByte(F_RINDEX_LVALUE);
                    break;
                }
            case NodeKind.NODE_RANGE:
                {
                    string flags = node.Name ?? "0000";
                    int sRev = flags.Length>0 && flags[0]=='1' ? 1:0;
                    int eRev = flags.Length>1 && flags[1]=='1' ? 1:0;
                    int eExcl = flags.Length>3 && flags[3]=='1' ? 1:0;
                    byte opcode;
                    if (sRev==0 && eRev==0) opcode = F_NN_RANGE_LVALUE;
                    else if (sRev==1 && eRev==0) opcode = F_RN_RANGE_LVALUE;
                    else if (sRev==0 && eRev==1) opcode = F_NR_RANGE_LVALUE;
                    else if (sRev==1 && eRev==1) opcode = F_RR_RANGE_LVALUE;
                    else opcode = F_NN_RANGE_LVALUE;
                    var baseNode = node.Children.Count>0? node.Children[0]:node.Left;
                    var startNode = node.Children.Count>1? node.Children[1]:null;
                    var endNode = node.Children.Count>2? node.Children[2]:null;
                    if (baseNode!=null) GenerateLValue(baseNode);
                    if (startNode!=null) GenerateNode(startNode, false); else { _ctx.CodeBlock.InsByte(F_BYTE); _ctx.CodeBlock.InsByte(0xFF); }
                    if (endNode!=null){
                        if (eExcl==1 && eRev==1 && endNode.Kind==NodeKind.NODE_NUMBER){
                            var adj = ParseNode.NewNumber(endNode.NumberValue+1, endNode.Line);
                            GenerateNode(adj, false);
                        } else GenerateNode(endNode, false);
                    } else { _ctx.CodeBlock.InsByte(F_BYTE); _ctx.CodeBlock.InsByte(0xFF); }
                    _ctx.CodeBlock.InsByte(opcode);
                    break;
                }
            default:
                GenerateNode(node, false);
                break;
        }
    }


    public void GenerateNode(ParseNode node, bool isStmt = true)
    {
        if (node == null) return;
        switch (node.Kind)
        {
            case NodeKind.NODE_VAR_DEF:
                if (node.Children.Count > 0 && node.Children[0].Kind == NodeKind.NODE_VAR_DEF)
                {
                    foreach (var child in node.Children)
                    {
                        if (child.Kind != NodeKind.NODE_VAR_DEF) { GenerateNode(child, true); continue; }
                        ParseNode sizeNode = null; ParseNode initNode = null;
                        foreach (var c in child.Children) { if (c.Name=="__array_size") sizeNode=c; else initNode=c; }
                        if (sizeNode != null) { int sz = (int)sizeNode.NumberValue; if (sz<=0) sz=1; int b = _locals.DefineArray(child.Name, sz, true); _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)b); EmitNumber(sz); _ctx.CodeBlock.InsByte(F_PUSH_ARRAY); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); }
                        else { int localIdx = _locals.Define(child.Name); if (initNode != null) { _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)localIdx); GenerateNode(initNode, false); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); } else { _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)localIdx); _ctx.CodeBlock.InsByte(F_CONST0); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); } }
                    }
                }
                else if (!string.IsNullOrEmpty(node.Name) && node.Name!= "int" && node.Name!= "string" && node.Name!= "mixed" && node.Name!= "void" && node.Name!= "object" && node.Name!= "mapping" && node.Name!="__empty_init" && node.Name!="__empty_cond" && node.Name!="__empty_inc")
                {
                    ParseNode sizeNode2 = null; ParseNode initNode2 = null;
                    foreach (var c in node.Children) { if (c.Name=="__array_size") sizeNode2=c; else initNode2=c; }
                    if (sizeNode2 != null) { int sz = (int)sizeNode2.NumberValue; if (sz<=0) sz=1; int b = _locals.DefineArray(node.Name, sz, true); _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)b); EmitNumber(sz); _ctx.CodeBlock.InsByte(F_PUSH_ARRAY); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); }
                    else { int localIdx = _locals.Define(node.Name); if (initNode2 != null) { _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)localIdx); GenerateNode(initNode2, false); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); } else { _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)localIdx); _ctx.CodeBlock.InsByte(F_CONST0); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); } }
                }
                else
                {
                    foreach (var child in node.Children)
                    {
                        if (child.Kind == NodeKind.NODE_VAR_DEF)
                        {
                            ParseNode sizeNode = null; ParseNode initNode = null;
                            foreach (var c in child.Children) { if (c.Name=="__array_size") sizeNode=c; else initNode=c; }
                            if (sizeNode != null) { int sz = (int)sizeNode.NumberValue; if (sz<=0) sz=1; int b = _locals.DefineArray(child.Name, sz, true); _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)b); EmitNumber(sz); _ctx.CodeBlock.InsByte(F_PUSH_ARRAY); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); }
                            else { int localIdx = _locals.Define(child.Name); if (initNode != null) { _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)localIdx); GenerateNode(initNode, false); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); } else { _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)localIdx); _ctx.CodeBlock.InsByte(F_CONST0); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); } }
                        } else GenerateNode(child, true);
                    }
                }
                break;
            case NodeKind.NODE_NUMBER:
                if (node.Name != null && node.Name.StartsWith("__empty")) break;
                EmitNumber(node.NumberValue);
                if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
                break;
            case NodeKind.NODE_REAL:
                // F_REAL + 8-byte double. VM currently truncates to Number (T_REAL not yet in SValueType).
                _ctx.CodeBlock.InsByte(F_REAL);
                _ctx.CodeBlock.InsDouble(node.RealValue);
                if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
                break;
            case NodeKind.NODE_STRING:
                { int strIdx = _ctx.AddString(node.StringValue); _ctx.CodeBlock.InsByte(F_STRING); _ctx.CodeBlock.InsShort((short)strIdx); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); break; }
            case NodeKind.NODE_IDENTIFIER:
                {
                    if (node.Name.StartsWith("__captured_")) { if (int.TryParse(node.Name.Substring("__captured_".Length), out int ci)) { _ctx.CodeBlock.InsByte(F_CAPTURED); _ctx.CodeBlock.InsByte((byte)ci); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); break; } }
                    if (node.Name.StartsWith("$")) { if (int.TryParse(node.Name.Substring(1), out int n) && n>=1) { int idx = n-1; _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)idx); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); break; } }
                    if (_locals.TryGetIndex(node.Name, out int localIdx2)) { _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)localIdx2); }
                    else if (_globalIdx.TryGetValue(node.Name, out int gIdx2)) { _ctx.CodeBlock.InsByte(F_GLOBAL); _ctx.CodeBlock.InsShort((short)gIdx2); }
                    else { int strIdx = _ctx.AddString(node.Name); _ctx.CodeBlock.InsByte(F_STRING); _ctx.CodeBlock.InsShort((short)strIdx); }
                    if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
                    break;
                }
            case NodeKind.NODE_MAPPING:
                { _ctx.CodeBlock.InsByte(F_PUSH_MAPPING); for (int i=0;i<node.Children.Count;i+=2) { var k = node.Children[i]; var v = i+1 < node.Children.Count ? node.Children[i+1] : null; GenerateNode(k, false); if (v != null) GenerateNode(v, false); else _ctx.CodeBlock.InsByte(F_CONST0); _ctx.CodeBlock.InsByte(F_MAP_ADD); } if (isStmt) _ctx.CodeBlock.InsByte(F_POP); break; }
            case NodeKind.NODE_CLOSURE: { GenerateClosure(node, isStmt); break; }
            case NodeKind.NODE_DOT_CALL: { GenerateDotCall(node, isStmt); break; }
            case NodeKind.NODE_RINDEX:
                {
                    var baseNode = node.Children.Count>0? node.Children[0]:node.Left;
                    var idxNode = node.Children.Count>1? node.Children[1]:node.Right;
                    if (baseNode!=null) GenerateNode(baseNode, false);
                    if (idxNode!=null) GenerateNode(idxNode, false); else { _ctx.CodeBlock.InsByte(F_BYTE); _ctx.CodeBlock.InsByte(0xFF); }
                    _ctx.CodeBlock.InsByte(F_RINDEX);
                    if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
                    break;
                }
            case NodeKind.NODE_RANGE:
                {
                    string flags = node.Name ?? "0000";
                    int sRev = flags.Length>0 && flags[0]=='1' ? 1:0;
                    int eRev = flags.Length>1 && flags[1]=='1' ? 1:0;
                    int eExcl = flags.Length>3 && flags[3]=='1' ? 1:0;
                    byte opcode;
                    if (sRev==0 && eRev==0 && eExcl==0) opcode = F_NN_RANGE;
                    else if (sRev==1 && eRev==0 && eExcl==0) opcode = F_RN_RANGE;
                    else if (sRev==0 && eRev==1 && eExcl==0) opcode = F_NR_RANGE;
                    else if (sRev==1 && eRev==1 && eExcl==0) opcode = F_RR_RANGE;
                    else if (sRev==0 && eRev==1 && eExcl==1) opcode = F_NE_RANGE;
                    else if (sRev==1 && eRev==1 && eExcl==1) opcode = F_RE_RANGE;
                    else opcode = F_NN_RANGE;
                    var baseNode = node.Children.Count>0? node.Children[0]:node.Left;
                    var startNode = node.Children.Count>1? node.Children[1]:null;
                    var endNode = node.Children.Count>2? node.Children[2]:null;
                    if (baseNode!=null) GenerateNode(baseNode, false);
                    if (startNode!=null) GenerateNode(startNode, false); else { _ctx.CodeBlock.InsByte(F_BYTE); _ctx.CodeBlock.InsByte(0xFF); }
                    if (endNode!=null) GenerateNode(endNode, false); else { _ctx.CodeBlock.InsByte(F_BYTE); _ctx.CodeBlock.InsByte(0xFF); }
                    _ctx.CodeBlock.InsByte(opcode);
                    if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
                    break;
                }
            case NodeKind.NODE_ARRAY:
                {
                    if (node.Name == "array_lit")
                    {
                        int n = node.Children.Count;
                        for (int i=0;i<n;i++) GenerateNode(node.Children[i], false);
                        _ctx.CodeBlock.InsByte(F_AGGREGATE);
                        _ctx.CodeBlock.InsShort((short)n);
                        if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
                        break;
                    }
                    else
                    {
                        var baseId = node.Left; if (baseId != null) { GenerateNode(baseId, false); GenerateNode(node.Right, false); _ctx.CodeBlock.InsByte(F_INDEX); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); } else { _ctx.CodeBlock.InsByte(F_CONST0); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); } break;
                    }
                }

            case NodeKind.NODE_ASSIGN:
                {
                    var left = node.Left ?? (node.Children.Count>0? node.Children[0]:null); var right = node.Right ?? (node.Children.Count>1? node.Children[1]:null);
                    ParseNode leftNode = left;
                    ParseNode rightNode = right;
                    if (leftNode==null && node.Children.Count>=2) { leftNode=node.Children[0]; rightNode=node.Children[1]; }
                    if (leftNode != null)
                    {
                        // lvalue for left
                        GenerateLValue(leftNode);
                        if (rightNode!=null) GenerateNode(rightNode, false);
                        else _ctx.CodeBlock.InsByte(F_CONST0);
                        if (isStmt) _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
                        else _ctx.CodeBlock.InsByte(F_ASSIGN);
                        break;
                    }
                    break;
                }

            case NodeKind.NODE_BINARY_OP:
                if (node.Children.Count == 1)
                {
                    var operand = node.Children[0];
                    if (node.Name == "!")
                    {
                        // !x is LPC truthiness (IsTrue), NOT x == 0: empty string is falsy but != 0
                        if (operand != null) GenerateNode(operand, false); else _ctx.CodeBlock.InsByte(F_CONST0);
                        _ctx.CodeBlock.InsByte(F_NOT); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); break;
                    }
                    if (operand != null && operand.Kind == NodeKind.NODE_IDENTIFIER && _locals.TryGetIndex(operand.Name, out int pIdx))
                    {
                        if (node.Name == "++") { _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)pIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)pIdx); _ctx.CodeBlock.InsByte(F_CONST1); _ctx.CodeBlock.InsByte(F_ADD); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); if (!isStmt) { _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)pIdx); } break; }
                        else if (node.Name == "--") { _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)pIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)pIdx); _ctx.CodeBlock.InsByte(F_CONST1); _ctx.CodeBlock.InsByte(F_SUB); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); if (!isStmt) { _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)pIdx); } break; }
                    }
                }
                if (node.Name == "post++" || node.Name == "post--")
                {
                    var opd = node.Left ?? (node.Children.Count>0? node.Children[0]:null);
                    if (opd != null && opd.Kind == NodeKind.NODE_IDENTIFIER && _locals.TryGetIndex(opd.Name, out int postIdx))
                    {
                        if (node.Name == "post++") { if (!isStmt) { _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)postIdx); } _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)postIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)postIdx); _ctx.CodeBlock.InsByte(F_CONST1); _ctx.CodeBlock.InsByte(F_ADD); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); break; }
                        else { if (!isStmt) { _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)postIdx); } _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)postIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)postIdx); _ctx.CodeBlock.InsByte(F_CONST1); _ctx.CodeBlock.InsByte(F_SUB); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); break; }
                    }
                }
                if (node.Name == "&&") { GenerateAnd(node); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); break; }
                if (node.Name == "||") { GenerateOr(node); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); break; }
                // Compound assignment: s += v  =>  s = s + v (lvalue, current, rhs, op, assign)
                if (node.Name == "+=" || node.Name == "-=" || node.Name == "*=" || node.Name == "/=" || node.Name == "%=" || node.Name == "&=" || node.Name == "|=" || node.Name == "^=" || node.Name == "<<=" || node.Name == ">>=")
                {
                    var cLeft = node.Left ?? (node.Children.Count>0 ? node.Children[0] : null);
                    var cRight = node.Right ?? (node.Children.Count>1 ? node.Children[1] : null);
                    if (cLeft != null)
                    {
                        GenerateLValue(cLeft);
                        GenerateNode(cLeft, false);
                        if (cRight != null) GenerateNode(cRight, false); else _ctx.CodeBlock.InsByte(F_CONST0);
                        switch (node.Name)
                        {
                            case "+=": _ctx.CodeBlock.InsByte(F_ADD); break;
                            case "-=": _ctx.CodeBlock.InsByte(F_SUB); break;
                            case "*=": _ctx.CodeBlock.InsByte(F_MULTIPLY); break;
                            case "/=": _ctx.CodeBlock.InsByte(F_DIVIDE); break;
                            case "%=": _ctx.CodeBlock.InsByte(F_MOD); break;
                            case "&=": _ctx.CodeBlock.InsByte(F_AND); break;
                            case "|=": _ctx.CodeBlock.InsByte(F_OR); break;
                            case "^=": _ctx.CodeBlock.InsByte(F_XOR); break;
                            case "<<=": _ctx.CodeBlock.InsByte(F_LSH); break;
                            case ">>=": _ctx.CodeBlock.InsByte(F_RSH); break;
                        }
                        if (isStmt) _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
                        else _ctx.CodeBlock.InsByte(F_ASSIGN);
                    }
                    break;
                }
                if (node.Left!= null || node.Right!= null) { if (node.Left!= null) GenerateNode(node.Left, false); if (node.Right!= null) GenerateNode(node.Right, false); }
                else if (node.Children.Count >= 2) { GenerateNode(node.Children[0], false); GenerateNode(node.Children[1], false); }
                switch (node.Name)
                {
                    case "&": _ctx.CodeBlock.InsByte(F_AND); break;
                    case "|": _ctx.CodeBlock.InsByte(F_OR); break;
                    case "^": _ctx.CodeBlock.InsByte(F_XOR); break;
                    case "+": _ctx.CodeBlock.InsByte(F_ADD); break;
                    case "-": _ctx.CodeBlock.InsByte(F_SUB); break;
                    case "*": _ctx.CodeBlock.InsByte(F_MULTIPLY); break;
                    case "/": _ctx.CodeBlock.InsByte(F_DIVIDE); break;
                    case "%": _ctx.CodeBlock.InsByte(F_MOD); break;
                    case "<<": _ctx.CodeBlock.InsByte(F_LSH); break;
                    case ">>": _ctx.CodeBlock.InsByte(F_RSH); break;
                    case "<": _ctx.CodeBlock.InsByte(F_LT); break;
                    case ">": _ctx.CodeBlock.InsByte(F_GT); break;
                    case "<=": _ctx.CodeBlock.InsByte(F_LE); break;
                    case ">=": _ctx.CodeBlock.InsByte(F_GE); break;
                    case "==": _ctx.CodeBlock.InsByte(F_EQ); break;
                    case "!=": _ctx.CodeBlock.InsByte(F_NE); break;
                    default: break;
                }
                if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
                break;
            case NodeKind.NODE_RETURN:
                if (node.Children.Count > 0) GenerateNode(node.Children[0], false); else if (node.Left!= null) GenerateNode(node.Left, false);
                _ctx.CodeBlock.InsByte(F_RETURN); break;
            case NodeKind.NODE_FUNCTION_DEF:
                foreach (var ch in node.Children) GenerateNode(ch, true); break;
            case NodeKind.NODE_IF: GenerateIf(node); break;
            case NodeKind.NODE_LOOP:
                if (node.Name == "for") GenerateFor(node);
                else if (node.Name == "while") GenerateWhile(node);
                else if (node.Name == "break") GenerateBreak();
                else if (node.Name == "continue") GenerateContinue();
                else if (node.Name == "foreach") GenerateForeach(node);
                else if (node.Name == "switch") GenerateSwitch(node);
                else GenerateWhile(node);
                break;
            case NodeKind.NODE_TERNARY:
                {
                    // cond ? trueExpr : falseExpr
                    if (node.Children.Count < 3) break;
                    var cond = node.Children[0];
                    var trueExpr = node.Children[1];
                    var falseExpr = node.Children[2];
                    GenerateNode(cond, false);
                    int branchFalsePos = _ctx.CodeBlock.Position;
                    _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_ZERO);
                    _ctx.CodeBlock.InsShort(0);
                    GenerateNode(trueExpr, false);
                    int branchEndPos = _ctx.CodeBlock.Position;
                    _ctx.CodeBlock.InsByte(F_BRANCH);
                    _ctx.CodeBlock.InsShort(0);
                    int falseStart = _ctx.CodeBlock.Position;
                    PatchShort(branchFalsePos+1, (short)(falseStart - (branchFalsePos+3)));
                    GenerateNode(falseExpr, false);
                    int after = _ctx.CodeBlock.Position;
                    PatchShort(branchEndPos+1, (short)(after - (branchEndPos+3)));
                    if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
                    break;
                }
            case NodeKind.NODE_CATCH:
                {
                    // catch(expr): F_CATCH offset, expr, POP, F_END_CATCH, BRANCH over handler
                    if (node.Children.Count < 1) { _ctx.CodeBlock.InsByte(F_CONST0); break; }
                    var inner = node.Children[0];
                    int catchPos = _ctx.CodeBlock.Position;
                    _ctx.CodeBlock.InsByte(38); // F_CATCH
                    _ctx.CodeBlock.InsShort(0); // placeholder
                    GenerateNode(inner, false);
                    _ctx.CodeBlock.InsByte(F_POP); // discard inner value
                    _ctx.CodeBlock.InsByte(39); // F_END_CATCH
                    int branchOverPos = _ctx.CodeBlock.Position;
                    _ctx.CodeBlock.InsByte(F_BRANCH);
                    _ctx.CodeBlock.InsShort(0);
                    int handlerPos = _ctx.CodeBlock.Position;
                    // handler: on error VM pushes error string and jumps here
                    // nothing needed, error already on stack
                    int afterHandler = _ctx.CodeBlock.Position;
                    PatchShort(catchPos+1, (short)(handlerPos - (catchPos+3)));
                    PatchShort(branchOverPos+1, (short)(afterHandler - (branchOverPos+3)));
                    // normal path: F_END_CATCH pushes 0, so stack has 0
                    if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
                    break;
                }
            case NodeKind.NODE_NEW:
                {
                    // new(path) => clone_object alias for ES2
                    if (node.Children.Count == 0)
                    {
                        _ctx.CodeBlock.InsByte(112); // F_NEW_EMPTY_CLASS
                    }
                    else
                    {
                        // first arg is path/class
                        GenerateNode(node.Children[0], false);
                        // for simplicity treat as clone_object
                        _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(8);
                        // extra args ignored for now
                        for (int i=1;i<node.Children.Count;i++) { /* push but pop? */ }
                    }
                    if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
                    break;
                }
            case NodeKind.NODE_CALL:
                {
                    if (node.Name == "sscanf" ) {
                        int n = node.Children.Count;
                        for (int i=0;i<n && i<2;i++) GenerateNode(node.Children[i], false);
                        for (int i=2;i<n;i++) GenerateLValue(node.Children[i]);
                        _ctx.CodeBlock.InsByte(F_SSCANF);
                        _ctx.CodeBlock.InsByte((byte)n);
                        if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
                        break;
                    }
                    if (node.Name == "write" ) { foreach (var arg in node.Children) GenerateNode(arg, false); _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(63); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); break; }
                    if (node.Name == "sizeof" && node.Children.Count==1) { GenerateNode(node.Children[0], false); _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(17); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); break; }
                    if (node.Name == "keys" && node.Children.Count==1) { GenerateNode(node.Children[0], false); _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(73); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); break; }
                    if (node.Name == "values" && node.Children.Count==1) { GenerateNode(node.Children[0], false); _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(72); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); break; }
                    if ((node.Name == "m_delete" || node.Name == "map_delete") && node.Children.Count==2) { GenerateNode(node.Children[0], false); GenerateNode(node.Children[1], false); _ctx.CodeBlock.InsByte(F_EFUN2); _ctx.CodeBlock.InsByte(75); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); break; }
                    if ((node.Name == "acos" || node.Name == "asin" || node.Name == "atan" || node.Name == "cos" || node.Name == "sin" || node.Name == "tan" || node.Name == "exp" || node.Name == "ceil" || node.Name == "floor") && node.Children.Count==1) { GenerateNode(node.Children[0], false); _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte((byte)(node.Name=="acos"?132:node.Name=="asin"?133:node.Name=="atan"?134:node.Name=="cos"?135:node.Name=="sin"?136:node.Name=="tan"?137:node.Name=="exp"?138:node.Name=="ceil"?141:142)); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); break; }
                    if (node.Name == "pow" && node.Children.Count==2) { GenerateNode(node.Children[0], false); GenerateNode(node.Children[1], false); _ctx.CodeBlock.InsByte(F_EFUN2); _ctx.CodeBlock.InsByte(140); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); break; }
                    if ((node.Name == "set_bit" || node.Name == "clear_bit" || node.Name == "test_bit") && node.Children.Count==2) { GenerateNode(node.Children[0], false); GenerateNode(node.Children[1], false); _ctx.CodeBlock.InsByte(F_EFUN2); _ctx.CodeBlock.InsByte((byte)(node.Name=="set_bit"?143:node.Name=="clear_bit"?144:145)); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); break; }
                    if (node.Name == "evaluate") { foreach (var arg in node.Children) GenerateNode(arg, false); _ctx.CodeBlock.InsByte(F_EVALUATE); _ctx.CodeBlock.InsByte((byte)node.Children.Count); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); break; }
                    if ((node.Name == "filter" || node.Name == "filter_array" || node.Name == "filter_objects" || node.Name == "filter_mapping") && node.Children.Count>=3 && node.Children[1].Kind==NodeKind.NODE_STRING) { foreach (var ch in node.Children) GenerateNode(ch, false); _ctx.CodeBlock.InsByte(F_EFUNV); _ctx.CodeBlock.InsByte((byte)146); _ctx.CodeBlock.InsByte((byte)node.Children.Count); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); break; }
                    if ((node.Name == "map" || node.Name == "map_array" || node.Name == "map_objects" || node.Name == "map_mapping") && node.Children.Count>=3 && node.Children[1].Kind==NodeKind.NODE_STRING) { foreach (var ch in node.Children) GenerateNode(ch, false); _ctx.CodeBlock.InsByte(F_EFUNV); _ctx.CodeBlock.InsByte((byte)147); _ctx.CodeBlock.InsByte((byte)node.Children.Count); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); break; }
                    if (node.Name == "unique_mapping" && node.Children.Count>=3 && node.Children[1].Kind==NodeKind.NODE_STRING) { foreach (var ch in node.Children) GenerateNode(ch, false); _ctx.CodeBlock.InsByte(F_EFUNV); _ctx.CodeBlock.InsByte((byte)150); _ctx.CodeBlock.InsByte((byte)node.Children.Count); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); break; }
                    if (node.Name == "filter_mapping" && (node.Children.Count==2 || node.Children.Count==3)) { GenerateFilter(node, isStmt); break; }
                    if (node.Name == "map_mapping" && (node.Children.Count==2 || node.Children.Count==3)) { GenerateMapMapping(node, isStmt); break; }
                    if (node.Name == "unique_mapping" && (node.Children.Count==2 || node.Children.Count==3)) { GenerateUniqueMapping(node, isStmt); break; }
                    if ((node.Name == "filter" || node.Name == "filter_array" || node.Name == "filter_objects") && node.Children.Count==2) { GenerateFilter(node, isStmt); break; }
                    if ((node.Name == "map" || node.Name == "map_array" || node.Name == "map_objects") && node.Children.Count==2) { GenerateMap(node, isStmt); break; }
                    if ((node.Name == "filter" || node.Name == "filter_array" || node.Name == "filter_objects") && node.Children.Count==3) { GenerateFilter(node, isStmt); break; }
                    if ((node.Name == "map" || node.Name == "map_array" || node.Name == "map_objects") && node.Children.Count==3) { GenerateMap(node, isStmt); break; }
                    if (node.Name == "sort_array" && node.Children.Count>=3 && node.Children[1].Kind==NodeKind.NODE_STRING) { foreach (var ch in node.Children) GenerateNode(ch, false); _ctx.CodeBlock.InsByte(F_EFUNV); _ctx.CodeBlock.InsByte((byte)155); _ctx.CodeBlock.InsByte((byte)node.Children.Count); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); break; }
                    if (node.Name == "sort_array" && node.Children.Count==2) { GenerateSortArray(node, isStmt); break; }
                    if (node.Name == "clone_object" && node.Children.Count==1) { GenerateNode(node.Children[0], false); _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(8); if (isStmt) _ctx.CodeBlock.InsByte(F_POP); break; }
                    string callName = node.Name;
                    if (callName.StartsWith("::"))
                    {
                        string baseName = callName.Substring(2);
                        string parentName = "__parent_" + baseName;
                        ushort pIdx = FindFunction(parentName);
                        if (pIdx != 0xFFFF)
                        {
                            callName = parentName;
                        }
                        else
                        {
                            callName = baseName;
                        }
                    }
                    // strip efun:: prefix
                    string efunCheckName = callName;
                    if (efunCheckName.Contains("::")) efunCheckName = efunCheckName.Substring(efunCheckName.LastIndexOf("::")+2);
                    if (EfunMap.TryGetValue(efunCheckName, out byte efunIdx))
                    {
                        foreach (var arg in node.Children) GenerateNode(arg, false);
                        int nargs = node.Children.Count;
                        if (nargs==0) { _ctx.CodeBlock.InsByte(F_EFUN0); _ctx.CodeBlock.InsByte(efunIdx); }
                        else if (nargs==1) { _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(efunIdx); }
                        else if (nargs==2) { _ctx.CodeBlock.InsByte(F_EFUN2); _ctx.CodeBlock.InsByte(efunIdx); }
                        else if (nargs==3) { _ctx.CodeBlock.InsByte(F_EFUN3); _ctx.CodeBlock.InsByte(efunIdx); }
                        else { _ctx.CodeBlock.InsByte(F_EFUNV); _ctx.CodeBlock.InsByte(efunIdx); _ctx.CodeBlock.InsByte((byte)nargs); }
                        if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
                        break;
                    }
                    if (callName == "call_other") {
                        foreach (var arg in node.Children) GenerateNode(arg, false);
                        _ctx.CodeBlock.InsByte(F_EFUNV); _ctx.CodeBlock.InsByte(0);
                        _ctx.CodeBlock.InsByte((byte)node.Children.Count);
                        if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
                        break;
                    }
                    foreach (var arg in node.Children) GenerateNode(arg, false);
                    ushort fIdx = FindFunction(callName);
                    if (fIdx == 0xFFFF) {
                        // Unknown in this object: try simul_efun at runtime (cursor_translate, message_vision, gender_*, cjk_wrap, etc.)
                        // Args already on stack. Emit F_SIMUL_EFUN <strIdx> <nargs>; Interpreter falls back to 0 if not in simul_efun either.
                        int strIdx = _ctx.AddString(callName);
                        _ctx.CodeBlock.InsByte(F_SIMUL_EFUN);
                        _ctx.CodeBlock.InsShort((short)strIdx);
                        _ctx.CodeBlock.InsByte((byte)node.Children.Count);
                        if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
                        break;
                    }
                    _ctx.CodeBlock.InsByte(43) /* F_CALL */; _ctx.CodeBlock.InsShort((short)fIdx); _ctx.CodeBlock.InsByte((byte)node.Children.Count);
                    if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
                    break;
                }
            default:
                foreach (var ch in node.Children) GenerateNode(ch, isStmt);
                if (node.Left!= null) GenerateNode(node.Left, false);
                if (node.Right!= null) GenerateNode(node.Right, false);
                break;
        }
    }

    private void GenerateSwitch(ParseNode node)
    {
        if (node.Children.Count < 2) return;
        var expr = node.Children[0];
        var casesBlock = node.Children[1];
        string switchVar = $"__switch_{_switchCounter++}";
        int switchIdx = _locals.Define(switchVar);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)switchIdx); GenerateNode(expr, false); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        var switchLoop = new LoopInfo2 { CondPos = _ctx.CodeBlock.Position };
        _loopStack.Push(switchLoop);

        var caseList = new List<ParseNode>();
        ParseNode defaultNode = null;
        foreach (var c in casesBlock.Children)
        {
            if (c.Name == "case") caseList.Add(c);
            else if (c.Name == "default") defaultNode = c;
        }

        var branchPositions = new List<int>();
        // generate comparisons: if temp == caseVal => jump to body
        foreach (var caseNode in caseList)
        {
            if (caseNode.Children.Count < 1) continue;
            var caseVal = caseNode.Children[0];
            _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)switchIdx);
            GenerateNode(caseVal, false);
            _ctx.CodeBlock.InsByte(F_EQ); // ==
            int branchPos = _ctx.CodeBlock.Position;
            _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_NON_ZERO); // BRANCH_IF_TRUE
            _ctx.CodeBlock.InsShort(0);
            branchPositions.Add(branchPos);
        }
        // after all comparisons, jump to default or end
        int jumpToDefaultPos = -1;
        int jumpToEndPos = -1;
        if (defaultNode != null)
        {
            jumpToDefaultPos = _ctx.CodeBlock.Position;
            _ctx.CodeBlock.InsByte(F_BRANCH); _ctx.CodeBlock.InsShort(0);
        }
        else
        {
            jumpToEndPos = _ctx.CodeBlock.Position;
            _ctx.CodeBlock.InsByte(F_BRANCH); _ctx.CodeBlock.InsShort(0);
        }

        // generate bodies
        for (int i = 0; i < caseList.Count; i++)
        {
            int bodyStart = _ctx.CodeBlock.Position;
            PatchShort(branchPositions[i] + 1, (short)(bodyStart - (branchPositions[i] + 3)));
            var caseNode = caseList[i];
            if (caseNode.Children.Count >= 2) GenerateNode(caseNode.Children[1], true);
            // if next case exists and no break, fallthrough to next body (C behavior)
            // we don't insert jump; break will handle exit
        }

        if (defaultNode != null)
        {
            int defStart = _ctx.CodeBlock.Position;
            if (jumpToDefaultPos != -1) PatchShort(jumpToDefaultPos + 1, (short)(defStart - (jumpToDefaultPos + 3)));
            if (defaultNode.Children.Count >= 1) GenerateNode(defaultNode.Children[0], true);
        }

        int afterSwitch = _ctx.CodeBlock.Position;
        if (jumpToEndPos != -1) PatchShort(jumpToEndPos + 1, (short)(afterSwitch - (jumpToEndPos + 3)));
        foreach (var bp in switchLoop.BreakPatchPositions) PatchShort(bp + 1, (short)(afterSwitch - (bp + 3)));
        _loopStack.Pop();
    }

    private void GenerateDotCall(ParseNode node, bool isStmt)
    {
        if (node.Children.Count == 0) return;
        // special handling for .len() / .length / .sizeof() -> sizeof
        string m = node.Name;
        if (m == "len" || m == "length" || m == "sizeof" || m == "size")
        {
            var objNode = node.Children[0];
            GenerateNode(objNode, false);
            _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(17);
            if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
            return;
        }
        var objNode2 = node.Children[0];
        GenerateNode(objNode2, false);
        int strIdx = _ctx.AddString(node.Name);
        _ctx.CodeBlock.InsByte(F_STRING); _ctx.CodeBlock.InsShort((short)strIdx);
        for (int i=1;i<node.Children.Count;i++) GenerateNode(node.Children[i], false);
        byte nargs = (byte)(node.Children.Count + 1); // total: obj + funcName + args
        _ctx.CodeBlock.InsByte(F_EFUNV); _ctx.CodeBlock.InsByte(0);
        _ctx.CodeBlock.InsByte(nargs);
        if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
    }


        private void GenerateMapMapping(ParseNode node, bool isStmt)
        {
            var arrNode = node.Children[0]; var clNode = node.Children[1]; var extraNode = node.Children.Count>2 ? node.Children[2] : null;
            string id = $"{_filterCounter++}";
            string srcName = $"__mapmap_src_{id}"; string clName = $"__mapmap_cl_{id}"; string resName = $"__mapmap_res_{id}"; string idxName = $"__mapmap_idx_{id}"; string extraName = $"__mapmap_extra_{id}";
            int srcIdx = _locals.Define(srcName); int clIdx = _locals.Define(clName); int resIdx = _locals.Define(resName); int idxLocal = _locals.Define(idxName); int extraIdx = _locals.Define(extraName);
            string keysName = $"__mapmap_keys_{id}"; string keyName = $"__mapmap_key_{id}"; string valName = $"__mapmap_val_{id}"; string tmpName = $"__mapmap_tmp_{id}";
            int keysIdx = _locals.Define(keysName); int keyIdx = _locals.Define(keyName); int valIdx = _locals.Define(valName); int tmpIdx = _locals.Define(tmpName);
            _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)srcIdx); GenerateNode(arrNode, false); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)clIdx); GenerateNode(clNode, false); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            if (extraNode != null) { _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)extraIdx); GenerateNode(extraNode, false); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); }
            _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)resIdx); _ctx.CodeBlock.InsByte(F_PUSH_MAPPING); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)keysIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(73); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_CONST0); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            int loopCondPos = _ctx.CodeBlock.Position; var loopInfo = new LoopInfo2 { CondPos = loopCondPos }; _loopStack.Push(loopInfo);
            _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)keysIdx); _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(17); _ctx.CodeBlock.InsByte(F_LT);
            int branchFalsePos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_ZERO); _ctx.CodeBlock.InsShort(0);
            _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)keyIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)keysIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_INDEX); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)valIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)keyIdx); _ctx.CodeBlock.InsByte(F_INDEX); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            if (extraNode != null) { _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)tmpIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)clIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)keyIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)valIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)extraIdx); _ctx.CodeBlock.InsByte(F_EVALUATE); _ctx.CodeBlock.InsByte(4); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); }
            else { _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)tmpIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)clIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)keyIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)valIdx); _ctx.CodeBlock.InsByte(F_EVALUATE); _ctx.CodeBlock.InsByte(3); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); }
            _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)resIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)keyIdx); _ctx.CodeBlock.InsByte(F_INDEX_LVALUE); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)tmpIdx); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_CONST1); _ctx.CodeBlock.InsByte(F_ADD); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            int branchBackPos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BBRANCH); short backOffset = (short)(loopCondPos - (branchBackPos + 3)); _ctx.CodeBlock.InsShort(backOffset);
            int afterLoop = _ctx.CodeBlock.Position; PatchShort(branchFalsePos + 1, (short)(afterLoop - (branchFalsePos + 3))); foreach (var bp in loopInfo.BreakPatchPositions) PatchShort(bp + 1, (short)(afterLoop - (bp + 3))); _loopStack.Pop();
            _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)resIdx); if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
        }


        private void GenerateUniqueMapping(ParseNode node, bool isStmt)
        {
            var arrNode = node.Children[0]; var clNode = node.Children[1]; var extraNode = node.Children.Count>2 ? node.Children[2] : null;
            string id = $"{_filterCounter++}";
            string srcName = $"__uniq_src_{id}"; string clName = $"__uniq_cl_{id}"; string resName = $"__uniq_res_{id}"; string idxName = $"__uniq_idx_{id}"; string extraName = $"__uniq_extra_{id}";
            int srcIdx = _locals.Define(srcName); int clIdx = _locals.Define(clName); int resIdx = _locals.Define(resName); int idxLocal = _locals.Define(idxName); int extraIdx = _locals.Define(extraName);
            string elemName = $"__uniq_elem_{id}"; string keyName = $"__uniq_key_{id}"; string existName = $"__uniq_exist_{id}"; string tmpName = $"__uniq_tmp_{id}"; string singleName = $"__uniq_single_{id}";
            int elemIdx = _locals.Define(elemName); int keyIdx = _locals.Define(keyName); int existIdx = _locals.Define(existName); int tmpIdx = _locals.Define(tmpName); int singleIdx = _locals.Define(singleName);
            _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)srcIdx); GenerateNode(arrNode, false); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)clIdx); GenerateNode(clNode, false); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            if (extraNode != null) { _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)extraIdx); GenerateNode(extraNode, false); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); }
            _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)resIdx); _ctx.CodeBlock.InsByte(F_PUSH_MAPPING); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_CONST0); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            int loopCondPos = _ctx.CodeBlock.Position; var loopInfo = new LoopInfo2 { CondPos = loopCondPos }; _loopStack.Push(loopInfo);
            _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(17); _ctx.CodeBlock.InsByte(F_LT);
            int branchFalsePos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_ZERO); _ctx.CodeBlock.InsShort(0);
            _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)elemIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_INDEX); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            if (extraNode != null) { _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)keyIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)clIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)elemIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)extraIdx); _ctx.CodeBlock.InsByte(F_EVALUATE); _ctx.CodeBlock.InsByte(3); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); }
            else { _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)keyIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)clIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)elemIdx); _ctx.CodeBlock.InsByte(F_EVALUATE); _ctx.CodeBlock.InsByte(2); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); }
            _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)singleIdx); _ctx.CodeBlock.InsByte(F_CONST1); _ctx.CodeBlock.InsByte(F_PUSH_ARRAY); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)singleIdx); _ctx.CodeBlock.InsByte(F_BYTE); _ctx.CodeBlock.InsByte(0); _ctx.CodeBlock.InsByte(F_INDEX_LVALUE); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)elemIdx); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)existIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)resIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)keyIdx); _ctx.CodeBlock.InsByte(F_INDEX); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)existIdx); _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(86);
            int brNotArray = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_ZERO); _ctx.CodeBlock.InsShort(0);
            _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)tmpIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)existIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)singleIdx); _ctx.CodeBlock.InsByte(F_ADD); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            int jmpStore = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH); _ctx.CodeBlock.InsShort(0);
            int createPos = _ctx.CodeBlock.Position; PatchShort(brNotArray+1, (short)(createPos - (brNotArray+3)));
            _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)tmpIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)singleIdx); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            int storePos = _ctx.CodeBlock.Position; PatchShort(jmpStore+1, (short)(storePos - (jmpStore+3)));
            _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)resIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)keyIdx); _ctx.CodeBlock.InsByte(F_INDEX_LVALUE); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)tmpIdx); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_CONST1); _ctx.CodeBlock.InsByte(F_ADD); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
            int branchBackPos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BBRANCH); short backOffset = (short)(loopCondPos - (branchBackPos + 3)); _ctx.CodeBlock.InsShort(backOffset);
            int afterLoop = _ctx.CodeBlock.Position; PatchShort(branchFalsePos + 1, (short)(afterLoop - (branchFalsePos + 3))); foreach (var bp in loopInfo.BreakPatchPositions) PatchShort(bp + 1, (short)(afterLoop - (bp + 3))); _loopStack.Pop();
            _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)resIdx); if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
        }

        private void GenerateFilter(ParseNode node, bool isStmt)
    {
        var arrNode = node.Children[0]; var clNode = node.Children[1]; var extraNode = node.Children.Count>2 ? node.Children[2] : null;
        string id = $"{_filterCounter++}";
        string srcName = $"__filter_src_{id}"; string clName = $"__filter_cl_{id}"; string resName = $"__filter_res_{id}"; string idxName = $"__filter_idx_{id}"; string elemName = $"__filter_elem_{id}"; string extraName = $"__filter_extra_{id}";
        int srcIdx = _locals.Define(srcName); int clIdx = _locals.Define(clName); int resIdx = _locals.Define(resName); int idxLocal = _locals.Define(idxName); int elemIdx = _locals.Define(elemName); int extraIdx = _locals.Define(extraName);
        string keysName = $"__filter_keys_{id}"; string keyName = $"__filter_key_{id}"; string valName = $"__filter_val_{id}";
        int keysIdx = _locals.Define(keysName); int keyIdx = _locals.Define(keyName); int valIdx = _locals.Define(valName);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)srcIdx); GenerateNode(arrNode, false); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)clIdx); GenerateNode(clNode, false); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        if (extraNode != null) { _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)extraIdx); GenerateNode(extraNode, false); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); }
        // if (!mapp(src)) goto array_path
        _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(87);
        int branchToArrayPos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_ZERO); _ctx.CodeBlock.InsShort(0);
        // ---- mapping path: filter(mapping, func(key, val [, extra])) -> mapping ----
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)resIdx); _ctx.CodeBlock.InsByte(F_PUSH_MAPPING); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)keysIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(73); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_CONST0); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        int mapLoopCondPos = _ctx.CodeBlock.Position; var mapLoopInfo = new LoopInfo2 { CondPos = mapLoopCondPos }; _loopStack.Push(mapLoopInfo);
        _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)keysIdx); _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(17); _ctx.CodeBlock.InsByte(F_LT);
        int mapBranchFalsePos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_ZERO); _ctx.CodeBlock.InsShort(0);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)keyIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)keysIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_INDEX); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)valIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)keyIdx); _ctx.CodeBlock.InsByte(F_INDEX); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        if (extraNode != null) { _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)clIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)keyIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)valIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)extraIdx); _ctx.CodeBlock.InsByte(F_EVALUATE); _ctx.CodeBlock.InsByte(4); }
        else { _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)clIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)keyIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)valIdx); _ctx.CodeBlock.InsByte(F_EVALUATE); _ctx.CodeBlock.InsByte(3); }
        int mapBranchIfFalse = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_ZERO); _ctx.CodeBlock.InsShort(0);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)resIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)keyIdx); _ctx.CodeBlock.InsByte(F_INDEX_LVALUE); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)valIdx); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        int mapAfterIf = _ctx.CodeBlock.Position; PatchShort(mapBranchIfFalse+1, (short)(mapAfterIf - (mapBranchIfFalse+3)));
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_CONST1); _ctx.CodeBlock.InsByte(F_ADD); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        int mapBranchBackPos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BBRANCH); short mapBackOffset = (short)(mapLoopCondPos - (mapBranchBackPos + 3)); _ctx.CodeBlock.InsShort(mapBackOffset);
        int mapAfterLoop = _ctx.CodeBlock.Position; PatchShort(mapBranchFalsePos + 1, (short)(mapAfterLoop - (mapBranchFalsePos + 3))); foreach (var bp in mapLoopInfo.BreakPatchPositions) PatchShort(bp + 1, (short)(mapAfterLoop - (bp + 3))); _loopStack.Pop();
        _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)resIdx); if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
        int jumpToEndPos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH); _ctx.CodeBlock.InsShort(0);
        // ---- array path (existing) ----
        int arrayPathPos = _ctx.CodeBlock.Position; PatchShort(branchToArrayPos + 1, (short)(arrayPathPos - (branchToArrayPos + 3)));
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)resIdx); _ctx.CodeBlock.InsByte(F_CONST0); _ctx.CodeBlock.InsByte(F_PUSH_ARRAY); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_CONST0); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        int loopCondPos = _ctx.CodeBlock.Position; var loopInfo = new LoopInfo2 { CondPos = loopCondPos }; _loopStack.Push(loopInfo);
        _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(17); _ctx.CodeBlock.InsByte(F_LT);
        int branchFalsePos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_ZERO); _ctx.CodeBlock.InsShort(0);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)elemIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_INDEX); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        if (extraNode != null) { _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)clIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)elemIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)extraIdx); _ctx.CodeBlock.InsByte(F_EVALUATE); _ctx.CodeBlock.InsByte(3); }
        else { _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)clIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)elemIdx); _ctx.CodeBlock.InsByte(F_EVALUATE); _ctx.CodeBlock.InsByte(2); }
        int branchIfFalse = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_ZERO); _ctx.CodeBlock.InsShort(0);
        string tmpArr = $"__filter_tmp_{id}"; int tmpIdx = _locals.Define(tmpArr);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)tmpIdx); _ctx.CodeBlock.InsByte(F_CONST1); _ctx.CodeBlock.InsByte(F_PUSH_ARRAY); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)tmpIdx); _ctx.CodeBlock.InsByte(F_BYTE); _ctx.CodeBlock.InsByte(0); _ctx.CodeBlock.InsByte(F_INDEX_LVALUE); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)elemIdx); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)resIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)resIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)tmpIdx); _ctx.CodeBlock.InsByte(F_ADD); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        int afterIf = _ctx.CodeBlock.Position; PatchShort(branchIfFalse+1, (short)(afterIf - (branchIfFalse+3)));
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_CONST1); _ctx.CodeBlock.InsByte(F_ADD); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        int branchBackPos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BBRANCH); short backOffset = (short)(loopCondPos - (branchBackPos + 3)); _ctx.CodeBlock.InsShort(backOffset);
        int afterLoop = _ctx.CodeBlock.Position; PatchShort(branchFalsePos + 1, (short)(afterLoop - (branchFalsePos + 3))); foreach (var bp in loopInfo.BreakPatchPositions) PatchShort(bp + 1, (short)(afterLoop - (bp + 3))); _loopStack.Pop();
        _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)resIdx); if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
        int endPos = _ctx.CodeBlock.Position; PatchShort(jumpToEndPos + 1, (short)(endPos - (jumpToEndPos + 3)));
    }

        private void GenerateMap(ParseNode node, bool isStmt)
    {
        var arrNode = node.Children[0]; var clNode = node.Children[1]; var extraNode = node.Children.Count>2 ? node.Children[2] : null;
        string id = $"{_filterCounter++}";
        string srcName = $"__map_src_{id}"; string clName = $"__map_cl_{id}"; string resName = $"__map_res_{id}"; string idxName = $"__map_idx_{id}"; string elemName = $"__map_elem_{id}"; string valName = $"__map_val_{id}"; string extraName = $"__map_extra_{id}";
        int srcIdx = _locals.Define(srcName); int clIdx = _locals.Define(clName); int resIdx = _locals.Define(resName); int idxLocal = _locals.Define(idxName); int elemIdx = _locals.Define(elemName); int valIdx = _locals.Define(valName); int extraIdx = _locals.Define(extraName);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)srcIdx); GenerateNode(arrNode, false); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)clIdx); GenerateNode(clNode, false); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        if (extraNode != null) { _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)extraIdx); GenerateNode(extraNode, false); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); }
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)resIdx); _ctx.CodeBlock.InsByte(F_CONST0); _ctx.CodeBlock.InsByte(F_PUSH_ARRAY); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_CONST0); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        int loopCondPos = _ctx.CodeBlock.Position; var loopInfo = new LoopInfo2 { CondPos = loopCondPos }; _loopStack.Push(loopInfo);
        _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(17); _ctx.CodeBlock.InsByte(F_LT);
        int branchFalsePos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_ZERO); _ctx.CodeBlock.InsShort(0);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)elemIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_INDEX); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        if (extraNode != null) { _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)valIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)clIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)elemIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)extraIdx); _ctx.CodeBlock.InsByte(F_EVALUATE); _ctx.CodeBlock.InsByte(3); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); }
        else { _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)valIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)clIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)elemIdx); _ctx.CodeBlock.InsByte(F_EVALUATE); _ctx.CodeBlock.InsByte(2); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); }
        string tmpArr = $"__map_tmp_{id}"; int tmpIdx = _locals.Define(tmpArr);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)tmpIdx); _ctx.CodeBlock.InsByte(F_CONST1); _ctx.CodeBlock.InsByte(F_PUSH_ARRAY); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)tmpIdx); _ctx.CodeBlock.InsByte(F_BYTE); _ctx.CodeBlock.InsByte(0); _ctx.CodeBlock.InsByte(F_INDEX_LVALUE); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)valIdx); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)resIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)resIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)tmpIdx); _ctx.CodeBlock.InsByte(F_ADD); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_CONST1); _ctx.CodeBlock.InsByte(F_ADD); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        int branchBackPos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BBRANCH); short backOffset = (short)(loopCondPos - (branchBackPos + 3)); _ctx.CodeBlock.InsShort(backOffset);
        int afterLoop = _ctx.CodeBlock.Position; PatchShort(branchFalsePos + 1, (short)(afterLoop - (branchFalsePos + 3))); foreach (var bp in loopInfo.BreakPatchPositions) PatchShort(bp + 1, (short)(afterLoop - (bp + 3))); _loopStack.Pop();
        _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)resIdx); if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
    }


        private void GenerateSortArray(ParseNode node, bool isStmt)
    {
        var arrNode = node.Children[0]; var clNode = node.Children[1];
        string id = $"{_filterCounter++}";
        string srcName = $"__sort_src_{id}"; string clName = $"__sort_cl_{id}"; string iName = $"__sort_i_{id}"; string jName = $"__sort_j_{id}"; string nName = $"__sort_n_{id}"; string aName = $"__sort_a_{id}"; string bName = $"__sort_b_{id}"; string tmpName = $"__sort_tmp_{id}";
        int srcIdx = _locals.Define(srcName); int clIdx = _locals.Define(clName); int iIdx = _locals.Define(iName); int jIdx = _locals.Define(jName); int nIdx = _locals.Define(nName); int aIdx = _locals.Define(aName); int bIdx = _locals.Define(bName); int tmpIdx = _locals.Define(tmpName);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)srcIdx); GenerateNode(arrNode, false); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)clIdx); GenerateNode(clNode, false); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)nIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(17); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)iIdx); _ctx.CodeBlock.InsByte(F_CONST0); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        int outerCondPos = _ctx.CodeBlock.Position;
        var outerLoop = new LoopInfo2 { CondPos = outerCondPos }; _loopStack.Push(outerLoop);
        _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)iIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)nIdx); _ctx.CodeBlock.InsByte(F_LT);
        int outerBranchFalse = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_ZERO); _ctx.CodeBlock.InsShort(0);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)jIdx); _ctx.CodeBlock.InsByte(F_CONST0); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        int innerCondPos = _ctx.CodeBlock.Position;
        var innerLoop = new LoopInfo2 { CondPos = innerCondPos }; _loopStack.Push(innerLoop);
        _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)jIdx);
        _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)nIdx); _ctx.CodeBlock.InsByte(F_CONST1); _ctx.CodeBlock.InsByte(F_SUB);
        _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)iIdx); _ctx.CodeBlock.InsByte(F_SUB);
        _ctx.CodeBlock.InsByte(F_LT);
        int innerBranchFalse = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_ZERO); _ctx.CodeBlock.InsShort(0);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)aIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)jIdx); _ctx.CodeBlock.InsByte(F_INDEX); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)bIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)jIdx); _ctx.CodeBlock.InsByte(F_CONST1); _ctx.CodeBlock.InsByte(F_ADD); _ctx.CodeBlock.InsByte(F_INDEX); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)clIdx);
        _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)aIdx);
        _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)bIdx);
        _ctx.CodeBlock.InsByte(F_EVALUATE); _ctx.CodeBlock.InsByte(3);
        _ctx.CodeBlock.InsByte(F_CONST0); _ctx.CodeBlock.InsByte(F_GT);
        int swapBranchFalse = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_ZERO); _ctx.CodeBlock.InsShort(0);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)jIdx); _ctx.CodeBlock.InsByte(F_CONST1); _ctx.CodeBlock.InsByte(F_ADD); _ctx.CodeBlock.InsByte(F_INDEX_LVALUE); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)aIdx); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)jIdx); _ctx.CodeBlock.InsByte(F_INDEX_LVALUE); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)bIdx); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        int afterSwap = _ctx.CodeBlock.Position; PatchShort(swapBranchFalse+1, (short)(afterSwap - (swapBranchFalse+3)));
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)jIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)jIdx); _ctx.CodeBlock.InsByte(F_CONST1); _ctx.CodeBlock.InsByte(F_ADD); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        int innerBack = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BBRANCH); short innerOff = (short)(innerCondPos - (innerBack + 3)); _ctx.CodeBlock.InsShort(innerOff);
        int afterInner = _ctx.CodeBlock.Position; PatchShort(innerBranchFalse+1, (short)(afterInner - (innerBranchFalse+3)));
        foreach (var bp in innerLoop.BreakPatchPositions) PatchShort(bp + 1, (short)(afterInner - (bp + 3)));
        _loopStack.Pop();
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)iIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)iIdx); _ctx.CodeBlock.InsByte(F_CONST1); _ctx.CodeBlock.InsByte(F_ADD); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        int outerBack = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BBRANCH); short outerOff = (short)(outerCondPos - (outerBack + 3)); _ctx.CodeBlock.InsShort(outerOff);
        int afterOuter = _ctx.CodeBlock.Position; PatchShort(outerBranchFalse+1, (short)(afterOuter - (outerBranchFalse+3)));
        foreach (var bp in outerLoop.BreakPatchPositions) PatchShort(bp + 1, (short)(afterOuter - (bp + 3)));
        _loopStack.Pop();
        _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)srcIdx); if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
    }


    private void GenerateClosure(ParseNode node, bool isStmt)
    {
        int maxArg = FindMaxClosureArg(node);
        if (maxArg < 0) maxArg = 0;
        string closureName = $"__closure_{_closureCounter++}";
        int branchPos = _ctx.CodeBlock.Position;
        _ctx.CodeBlock.InsByte(F_BRANCH);
        _ctx.CodeBlock.InsShort(0);
        ushort closureAddr = (ushort)_ctx.CodeBlock.Position;
        ushort closureIdx = _ctx.AddFunction(closureName, closureAddr);
        var savedLocals = _locals.Clone();
        _locals.Clear();
        for (int i=1;i<=maxArg;i++) _locals.Define($"${i}");
        if (node.Children.Count > 1) {
            // Multi-arg closure: (: func, bound1, bound2, ... :) => return func(bound1, bound2, ..., $1)
            // First child is callee (identifier or expression), rest are bound args
            var callee = node.Children[0];
            // If callee is identifier call_other, handle specially: it needs F_CALL_OTHER with total args
            // Build a synthetic CALL node: callee(boundArgs + $1)
            var callNode = new ParseNode { Kind = NodeKind.NODE_CALL, Line = node.Line };
            if (callee.Kind == NodeKind.NODE_IDENTIFIER) {
                callNode.Name = callee.Name;
            } else {
                // For non-identifier callee (e.g. $1->func), we fallback to generating first child only
                GenerateNode(callee, false); _ctx.CodeBlock.InsByte(F_RETURN);
            }
            if (callee.Kind == NodeKind.NODE_IDENTIFIER) {
                for (int i=1;i<node.Children.Count;i++) { var ph = ParseNode.NewIdentifier($"__captured_{i-1}", node.Line); callNode.AddChild(ph); }
                // Forward $1 as extra arg if closure is evaluated with an argument (e.g. evaluate(closure, opt))
                // We need to ensure $1 is defined as local
                int dollar1Idx = _locals.Define("$1");
                // Actually $1 local already defined via maxArg loop if needed, but ensure it exists
                // Generate $1 identifier node
                var dollar1 = ParseNode.NewIdentifier("$1", node.Line);
                // Only add $1 if maxArg was 0? We always add $1 for multi-arg to forward evaluate arg
                // To avoid pushing Invalid when no extra arg, we still push $1; Interpreter will have Invalid if not provided, which is harmless for call_other
                callNode.AddChild(dollar1);
                // Ensure $1 local is defined (if maxArg was 0, we didn't define it yet, so define now)
                // _locals already has $1 if maxArg>=1, else we just defined it
                GenerateNode(callNode, false);
                _ctx.CodeBlock.InsByte(F_RETURN);
            }
        } else if (node.Children.Count == 1 && node.Children[0].Kind == NodeKind.NODE_IDENTIFIER && _funcArity.TryGetValue(node.Children[0].Name, out int arity)) {
            // (: funcname :) => funcname($1, ..., $arity) : forward closure args to the named function
            var callNode = new ParseNode { Kind = NodeKind.NODE_CALL, Line = node.Line };
            callNode.Name = node.Children[0].Name;
            for (int i = 1; i <= arity; i++) { _locals.Define($"${i}"); callNode.AddChild(ParseNode.NewIdentifier($"${i}", node.Line)); }
            GenerateNode(callNode, false);
            _ctx.CodeBlock.InsByte(F_RETURN);
        } else {
            foreach (var child in node.Children)
            {
                if (child.Kind == NodeKind.NODE_FUNCTION_DEF) { foreach (var stmt in child.Children) GenerateNode(stmt, true); }
                else { GenerateNode(child, false); _ctx.CodeBlock.InsByte(F_RETURN); break; }
            }
        }
        var codeArr = _ctx.CodeBlock.ToArray();
        if (codeArr.Length == closureAddr || codeArr[codeArr.Length-1] != 0xFF) _ctx.CodeBlock.InsByte(F_RETURN);
        _ctx.PatchFunctionAddress(closureIdx, closureAddr);
        int afterClosure = _ctx.CodeBlock.Position;
        PatchShort(branchPos+1, (short)(afterClosure - (branchPos+3)));
        _locals.Restore(savedLocals);
        if (node.Children.Count > 1) {
            // Evaluate bound args NOW in outer scope and capture them
            for (int i=1;i<node.Children.Count;i++) GenerateNode(node.Children[i], false);
            _ctx.CodeBlock.InsByte(F_PUSH_CLOSURE_CAPTURED);
            _ctx.CodeBlock.InsShort((short)closureIdx);
            _ctx.CodeBlock.InsByte((byte)(node.Children.Count-1));
        } else {
            _ctx.CodeBlock.InsByte(F_PUSH_CLOSURE);
            _ctx.CodeBlock.InsShort((short)closureIdx);
        }
        if (isStmt) _ctx.CodeBlock.InsByte(F_POP);
    }

    private int FindMaxClosureArg(ParseNode node)
    {
        int max = 0;
        void Scan(ParseNode n) { if (n == null) return; if (n.Kind == NodeKind.NODE_IDENTIFIER && n.Name.StartsWith("$")) { if (int.TryParse(n.Name.Substring(1), out int v) && v > max) max = v; } foreach (var c in n.Children) Scan(c); if (n.Left != null) Scan(n.Left); if (n.Right != null) Scan(n.Right); }
        Scan(node); return max;
    }

    private void GenerateForeach(ParseNode node)
    {
        if (node.Children.Count < 2) return;
        ParseNode keyNode = null; ParseNode valNode = null; ParseNode container = null; ParseNode body = null;
        if (node.Children.Count == 3) { keyNode = node.Children[0]; container = node.Children[1]; body = node.Children[2]; }
        else if (node.Children.Count == 4) { keyNode = node.Children[0]; valNode = node.Children[1]; container = node.Children[2]; body = node.Children[3]; }
        else return;
        string keyName = keyNode?.Name; string valName = valNode?.Name;
        if (!string.IsNullOrEmpty(keyName)) _locals.Define(keyName);
        if (!string.IsNullOrEmpty(valName)) _locals.Define(valName);
        string idxName = $"__foreach_idx_{_foreachCounter}"; string keysName = $"__foreach_keys_{_foreachCounter}"; string contName = $"__foreach_cont_{_foreachCounter}"; _foreachCounter++;
        int idxLocal = _locals.Define(idxName); int keysLocal = _locals.Define(keysName); int contLocal = _locals.Define(contName);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_CONST0); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)contLocal); GenerateNode(container, false); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)keysLocal); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)contLocal); _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(73); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        int loopCondPos = _ctx.CodeBlock.Position; var loopInfo = new LoopInfo2 { CondPos = loopCondPos }; _loopStack.Push(loopInfo);
        _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)keysLocal); _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(17); _ctx.CodeBlock.InsByte(F_LT);
        int branchFalsePos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_ZERO); _ctx.CodeBlock.InsShort(0);
        if (!string.IsNullOrEmpty(keyName)) { if (_locals.TryGetIndex(keyName, out int kIdx)) { _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)kIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)keysLocal); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_INDEX); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); } }
        // Single-var foreach over array: x should be value, not index. If container is array, deref: x = container[x]
        if (string.IsNullOrEmpty(valName) && !string.IsNullOrEmpty(keyName)) {
            if (_locals.TryGetIndex(keyName, out int kIdx3)) {
                _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)contLocal); _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(86); // arrayp
                int arrCheckPos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_ZERO); _ctx.CodeBlock.InsShort(0);
                _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)kIdx3); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)contLocal); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)kIdx3); _ctx.CodeBlock.InsByte(F_INDEX); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
                int afterDeref = _ctx.CodeBlock.Position; PatchShort(arrCheckPos+1, (short)(afterDeref - (arrCheckPos+3)));
                // string: also deref to single-char string
                _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)contLocal); _ctx.CodeBlock.InsByte(F_EFUN1); _ctx.CodeBlock.InsByte(82); // stringp
                int strCheckPos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_ZERO); _ctx.CodeBlock.InsShort(0);
                _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)kIdx3); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)contLocal); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)kIdx3); _ctx.CodeBlock.InsByte(F_INDEX); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
                int afterStrDeref = _ctx.CodeBlock.Position; PatchShort(strCheckPos+1, (short)(afterStrDeref - (strCheckPos+3)));
            }
        }
        if (!string.IsNullOrEmpty(valName) && !string.IsNullOrEmpty(keyName)) { if (_locals.TryGetIndex(valName, out int vIdx)) { _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)vIdx); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)contLocal); if (_locals.TryGetIndex(keyName, out int kIdx2)) { _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)kIdx2); } _ctx.CodeBlock.InsByte(F_INDEX); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN); } }
        if (body != null) GenerateNode(body, true);
        int incStart = _ctx.CodeBlock.Position; foreach (var cp in loopInfo.ContinuePatchPositions) PatchShort(cp + 1, (short)(incStart - (cp + 3))); loopInfo.ContinuePatchPositions.Clear();
        _ctx.CodeBlock.InsByte(F_LOCAL_LVALUE); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_LOCAL); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_CONST1); _ctx.CodeBlock.InsByte(F_ADD); _ctx.CodeBlock.InsByte(F_VOID_ASSIGN);
        int branchBackPos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BBRANCH); short backOffset = (short)(loopCondPos - (branchBackPos + 3)); _ctx.CodeBlock.InsShort(backOffset);
        int afterLoop = _ctx.CodeBlock.Position; PatchShort(branchFalsePos + 1, (short)(afterLoop - (branchFalsePos + 3))); foreach (var bp in loopInfo.BreakPatchPositions) PatchShort(bp + 1, (short)(afterLoop - (bp + 3))); _loopStack.Pop();
    }

    private void GenerateAnd(ParseNode node) { ParseNode left = node.Left ?? (node.Children.Count>0? node.Children[0]:null); ParseNode right = node.Right ?? (node.Children.Count>1? node.Children[1]:null); if (left != null) GenerateNode(left, false); _ctx.CodeBlock.InsByte(F_DUP); int branchFalsePos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_ZERO); _ctx.CodeBlock.InsShort(0); _ctx.CodeBlock.InsByte(F_POP); if (right != null) GenerateNode(right, false); int endLabel = _ctx.CodeBlock.Position; PatchShort(branchFalsePos+1, (short)(endLabel - (branchFalsePos+3))); }
    private void GenerateOr(ParseNode node) { ParseNode left = node.Left ?? (node.Children.Count>0? node.Children[0]:null); ParseNode right = node.Right ?? (node.Children.Count>1? node.Children[1]:null); if (left != null) GenerateNode(left, false); _ctx.CodeBlock.InsByte(F_DUP); int branchTruePos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_NON_ZERO); _ctx.CodeBlock.InsShort(0); _ctx.CodeBlock.InsByte(F_POP); if (right != null) GenerateNode(right, false); int endLabel = _ctx.CodeBlock.Position; PatchShort(branchTruePos+1, (short)(endLabel - (branchTruePos+3))); }
    private void GenerateIf(ParseNode node) { if (node.Children.Count == 0) return; var cond = node.Children[0]; var thenStmt = node.Children.Count > 1 ? node.Children[1] : null; var elseStmt = node.Children.Count > 2 ? node.Children[2] : null; GenerateNode(cond, false); int branchFalsePos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_ZERO); _ctx.CodeBlock.InsShort(0); if (thenStmt != null) GenerateNode(thenStmt, true); if (elseStmt != null) { int branchEndPos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH); _ctx.CodeBlock.InsShort(0); int elseStart = _ctx.CodeBlock.Position; PatchShort(branchFalsePos + 1, (short)(elseStart - (branchFalsePos + 3))); GenerateNode(elseStmt, true); int afterElse = _ctx.CodeBlock.Position; PatchShort(branchEndPos + 1, (short)(afterElse - (branchEndPos + 3))); } else { int afterThen = _ctx.CodeBlock.Position; PatchShort(branchFalsePos + 1, (short)(afterThen - (branchFalsePos + 3))); } }
    private void GenerateWhile(ParseNode node) { if (node.Children.Count == 0) return; var cond = node.Children[0]; var body = node.Children.Count > 1 ? node.Children[1] : null; int loopStart = _ctx.CodeBlock.Position; var loopInfo = new LoopInfo2 { CondPos = loopStart }; _loopStack.Push(loopInfo); GenerateNode(cond, false); int branchFalsePos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_ZERO); _ctx.CodeBlock.InsShort(0); if (body != null) GenerateNode(body, true); foreach (var cp in loopInfo.ContinuePatchPositions) PatchShort(cp + 1, (short)(loopStart - (cp + 3))); int branchBackPos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BBRANCH); short backOffset = (short)(loopStart - (branchBackPos + 3)); _ctx.CodeBlock.InsShort(backOffset); int afterLoop = _ctx.CodeBlock.Position; PatchShort(branchFalsePos + 1, (short)(afterLoop - (branchFalsePos + 3))); foreach (var bp in loopInfo.BreakPatchPositions) PatchShort(bp + 1, (short)(afterLoop - (bp + 3))); _loopStack.Pop(); }
    private void GenerateFor(ParseNode node) { if (node.Children.Count < 4) return; var init = node.Children[0]; var cond = node.Children[1]; var inc = node.Children[2]; var body = node.Children[3]; if (init != null && !(init.Name != null && init.Name.StartsWith("__empty"))) GenerateNode(init, true); int loopCondPos = _ctx.CodeBlock.Position; bool hasCond = !(cond.Name != null && cond.Name.StartsWith("__empty")); int branchFalsePos = -1; if (hasCond) { GenerateNode(cond, false); branchFalsePos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH_WHEN_ZERO); _ctx.CodeBlock.InsShort(0); } var loopInfo = new LoopInfo2 { CondPos = loopCondPos }; _loopStack.Push(loopInfo); if (body != null) GenerateNode(body, true); int incStart = _ctx.CodeBlock.Position; foreach (var cp in loopInfo.ContinuePatchPositions) PatchShort(cp + 1, (short)(incStart - (cp + 3))); loopInfo.ContinuePatchPositions.Clear(); bool hasInc = !(inc.Name != null && inc.Name.StartsWith("__empty")); if (hasInc) GenerateNode(inc, true); int branchBackPos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BBRANCH); short backOffset = (short)(loopCondPos - (branchBackPos + 3)); _ctx.CodeBlock.InsShort(backOffset); int afterLoop = _ctx.CodeBlock.Position; if (hasCond) PatchShort(branchFalsePos + 1, (short)(afterLoop - (branchFalsePos + 3))); foreach (var bp in loopInfo.BreakPatchPositions) PatchShort(bp + 1, (short)(afterLoop - (bp + 3))); _loopStack.Pop(); }
    private void GenerateBreak() { if (_loopStack.Count == 0) return; int pos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH); _ctx.CodeBlock.InsShort(0); _loopStack.Peek().BreakPatchPositions.Add(pos); }
    private void GenerateContinue() { if (_loopStack.Count == 0) return; int pos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(F_BRANCH); _ctx.CodeBlock.InsShort(0); _loopStack.Peek().ContinuePatchPositions.Add(pos); }
    private void PatchShort(int pos, short val) {
        var block = _ctx.CodeBlock;
        var type = block.GetType();
        // try common field names
        foreach (var name in new[] { "_rented", "_buf", "_buffer", "buffer", "_data", "_bytes", "_code" })
        {
            var field = type.GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                var arr = field.GetValue(block) as byte[];
                if (arr != null && pos+1 < arr.Length)
                {
                    arr[pos] = (byte)(val & 0xFF);
                    arr[pos + 1] = (byte)((val >> 8) & 0xFF);
                    return;
                }
            }
        }
        // try property
        foreach (var name in new[] { "Buffer", "Data", "Bytes", "Code" })
        {
            var prop = type.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (prop != null)
            {
                var arr = prop.GetValue(block) as byte[];
                if (arr != null && pos+1 < arr.Length)
                {
                    arr[pos] = (byte)(val & 0xFF);
                    arr[pos + 1] = (byte)((val >> 8) & 0xFF);
                    return;
                }
            }
        }
        // fallback: try method Patch
        var m = type.GetMethod("PatchShort", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (m != null)
        {
            try { m.Invoke(block, new object[]{ pos, val }); return; } catch {}
        }
        var m2 = type.GetMethod("Patch", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (m2 != null)
        {
            try { m2.Invoke(block, new object[]{ pos, val }); return; } catch {}
        }
        // last resort: try to get array via ToArray and reconstruct? We need to at least log
        System.Console.Error.WriteLine($"[PatchShort] failed to patch at {pos} val {val}, type {type.FullName}");
    }
    private ushort FindFunction(string name) { for (int i = 0; i < _ctx.Functions.Count; i++) if (_ctx.Functions[i].Name == name) return (ushort)i; return 0xFFFF; }
    private void EmitNumber(long val) { if (val == 0) _ctx.CodeBlock.InsByte(F_CONST0); else if (val == 1) _ctx.CodeBlock.InsByte(F_CONST1); else if (val >= -128 && val <= 127) { _ctx.CodeBlock.InsByte(F_BYTE); _ctx.CodeBlock.InsByte((byte)(sbyte)val); } else if (val >= int.MinValue && val <= int.MaxValue) { _ctx.CodeBlock.InsByte(F_NUMBER); _ctx.CodeBlock.InsInt((int)val); } else { _ctx.CodeBlock.InsByte(F_LONG); _ctx.CodeBlock.InsLong(val); } }
}

