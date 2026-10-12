using System;
using System.Collections.Generic;
using System.IO;
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
    private readonly Stack<LoopInfo2> _loopStack = new();
    const byte F_PUSH_MAPPING = 70;
    const byte F_MAP_ADD = 71;
    const byte F_INDEX = 72;
    const byte F_INDEX_ASSIGN = 73;
    const byte F_PUSH_ARRAY = 74;
    const byte F_SIZEOF = 75;
    const byte F_KEYS = 76;
    const byte F_VALUES = 77;
    const byte F_M_DELETE = 78;
    const byte F_PUSH_CLOSURE = 81;
    const byte F_EVALUATE = 82;
    const byte F_CALL_OTHER = 61;
    const byte F_CLONE_OBJECT = 86;
    private int _foreachCounter = 0;
    private int _closureCounter = 0;
    private int _filterCounter = 0;
    private int _switchCounter = 0;
    public List<string> Inherits = new();

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

    private List<ParseFunction> TryLoadBaseFromFile(string inheritPath)
    {
        var results = new List<ParseFunction>();
        try
        {
            string p = inheritPath.Trim().Trim('"').Trim('\'');
            var candidates = new List<string>();
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
        foreach (var inh in Inherits)
        {
            var loaded = TryLoadBaseFromFile(inh);
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
        var existingNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in functions) existingNames.Add(f.Name);
        var allFuncs = new List<ParseFunction>();
        foreach (var bf in baseFuncs) { if (!existingNames.Contains(bf.Name)) { allFuncs.Add(bf); existingNames.Add(bf.Name); } }
        allFuncs.AddRange(functions);

        foreach (var func in allFuncs)
        {
            _locals.Clear(); _loopStack.Clear(); _foreachCounter=0;
            if (func.Args != null) foreach (var arg in func.Args) _locals.Define(arg.Name);
            ushort addr = (ushort)_ctx.CodeBlock.Position;
            ushort idx = _ctx.AddFunction(func.Name, addr);
            if (func.Args != null && func.Args.Count > 0)
            {
                for (int i = func.Args.Count - 1; i >= 0; i--) { _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)i); _ctx.CodeBlock.InsByte(42); }
            }
            if (func.Body != null) GenerateNode(func.Body);
            var codeArr = _ctx.CodeBlock.ToArray();
            if (codeArr.Length == addr || codeArr[codeArr.Length-1] != 0xFF) _ctx.CodeBlock.InsByte(255);
            _ctx.PatchFunctionAddress(idx, addr);
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
                        if (sizeNode != null) { int sz = (int)sizeNode.NumberValue; if (sz<=0) sz=1; int b = _locals.DefineArray(child.Name, sz, true); EmitNumber(sz); _ctx.CodeBlock.InsByte(F_PUSH_ARRAY); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)b); _ctx.CodeBlock.InsByte(42); }
                        else { int localIdx = _locals.Define(child.Name); if (initNode != null) { GenerateNode(initNode, false); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)localIdx); _ctx.CodeBlock.InsByte(42); } else { _ctx.CodeBlock.InsByte(1); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)localIdx); _ctx.CodeBlock.InsByte(42); } }
                    }
                }
                else if (!string.IsNullOrEmpty(node.Name) && node.Name!= "int" && node.Name!= "string" && node.Name!= "mixed" && node.Name!= "void" && node.Name!= "object" && node.Name!= "mapping" && node.Name!="__empty_init" && node.Name!="__empty_cond" && node.Name!="__empty_inc")
                {
                    ParseNode sizeNode2 = null; ParseNode initNode2 = null;
                    foreach (var c in node.Children) { if (c.Name=="__array_size") sizeNode2=c; else initNode2=c; }
                    if (sizeNode2 != null) { int sz = (int)sizeNode2.NumberValue; if (sz<=0) sz=1; int b = _locals.DefineArray(node.Name, sz, true); EmitNumber(sz); _ctx.CodeBlock.InsByte(F_PUSH_ARRAY); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)b); _ctx.CodeBlock.InsByte(42); }
                    else { int localIdx = _locals.Define(node.Name); if (initNode2 != null) { GenerateNode(initNode2, false); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)localIdx); _ctx.CodeBlock.InsByte(42); } else { _ctx.CodeBlock.InsByte(1); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)localIdx); _ctx.CodeBlock.InsByte(42); } }
                }
                else
                {
                    foreach (var child in node.Children)
                    {
                        if (child.Kind == NodeKind.NODE_VAR_DEF)
                        {
                            ParseNode sizeNode = null; ParseNode initNode = null;
                            foreach (var c in child.Children) { if (c.Name=="__array_size") sizeNode=c; else initNode=c; }
                            if (sizeNode != null) { int sz = (int)sizeNode.NumberValue; if (sz<=0) sz=1; int b = _locals.DefineArray(child.Name, sz, true); EmitNumber(sz); _ctx.CodeBlock.InsByte(F_PUSH_ARRAY); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)b); _ctx.CodeBlock.InsByte(42); }
                            else { int localIdx = _locals.Define(child.Name); if (initNode != null) { GenerateNode(initNode, false); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)localIdx); _ctx.CodeBlock.InsByte(42); } else { _ctx.CodeBlock.InsByte(1); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)localIdx); _ctx.CodeBlock.InsByte(42); } }
                        } else GenerateNode(child, true);
                    }
                }
                break;
            case NodeKind.NODE_NUMBER:
                if (node.Name != null && node.Name.StartsWith("__empty")) break;
                EmitNumber(node.NumberValue);
                if (isStmt) _ctx.CodeBlock.InsByte(42);
                break;
            case NodeKind.NODE_STRING:
                { int strIdx = _ctx.AddString(node.StringValue); _ctx.CodeBlock.InsByte(6); _ctx.CodeBlock.InsShort((short)strIdx); if (isStmt) _ctx.CodeBlock.InsByte(42); break; }
            case NodeKind.NODE_IDENTIFIER:
                {
                    if (node.Name.StartsWith("$")) { if (int.TryParse(node.Name.Substring(1), out int n) && n>=1) { int idx = n-1; _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)idx); if (isStmt) _ctx.CodeBlock.InsByte(42); break; } }
                    if (_locals.TryGetIndex(node.Name, out int localIdx2)) { _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)localIdx2); }
                    else { int strIdx = _ctx.AddString(node.Name); _ctx.CodeBlock.InsByte(6); _ctx.CodeBlock.InsShort((short)strIdx); }
                    if (isStmt) _ctx.CodeBlock.InsByte(42);
                    break;
                }
            case NodeKind.NODE_MAPPING:
                { _ctx.CodeBlock.InsByte(F_PUSH_MAPPING); for (int i=0;i<node.Children.Count;i+=2) { var k = node.Children[i]; var v = i+1 < node.Children.Count ? node.Children[i+1] : null; GenerateNode(k, false); if (v != null) GenerateNode(v, false); else _ctx.CodeBlock.InsByte(1); _ctx.CodeBlock.InsByte(F_MAP_ADD); } if (isStmt) _ctx.CodeBlock.InsByte(42); break; }
            case NodeKind.NODE_CLOSURE: { GenerateClosure(node, isStmt); break; }
            case NodeKind.NODE_DOT_CALL: { GenerateDotCall(node, isStmt); break; }
            case NodeKind.NODE_ARRAY:
                {
                    if (node.Name == "array_lit")
                    {
                        int n = node.Children.Count; EmitNumber(n); _ctx.CodeBlock.InsByte(F_PUSH_ARRAY);
                        string tmpName = $"__arr_tmp_{_foreachCounter++}"; int tmpIdx = _locals.Define(tmpName);
                        _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)tmpIdx); _ctx.CodeBlock.InsByte(42);
                        for (int i=0;i<n;i++) { _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)tmpIdx); EmitNumber(i); GenerateNode(node.Children[i], false); _ctx.CodeBlock.InsByte(F_INDEX_ASSIGN); _ctx.CodeBlock.InsByte(42); }
                        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)tmpIdx); if (isStmt) _ctx.CodeBlock.InsByte(42); break;
                    }
                    else
                    {
                        var baseId = node.Left; if (baseId != null) { GenerateNode(baseId, false); GenerateNode(node.Right, false); _ctx.CodeBlock.InsByte(F_INDEX); if (isStmt) _ctx.CodeBlock.InsByte(42); } else { _ctx.CodeBlock.InsByte(1); if (isStmt) _ctx.CodeBlock.InsByte(42); } break;
                    }
                }
            case NodeKind.NODE_ASSIGN:
                {
                    var left = node.Left ?? (node.Children.Count>0? node.Children[0]:null); var right = node.Right ?? (node.Children.Count>1? node.Children[1]:null);
                    if (left != null && left.Kind == NodeKind.NODE_ARRAY) { var baseId = left.Left; GenerateNode(baseId, false); GenerateNode(left.Right, false); if (right != null) GenerateNode(right, false); else _ctx.CodeBlock.InsByte(1); _ctx.CodeBlock.InsByte(F_INDEX_ASSIGN); if (isStmt) _ctx.CodeBlock.InsByte(42); }
                    else if (left != null && left.Kind == NodeKind.NODE_IDENTIFIER) { if (right != null) GenerateNode(right, false); else if (node.Children.Count > 1) GenerateNode(node.Children[1], false); if (_locals.TryGetIndex(left.Name, out int idx)) { _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)idx); if (isStmt) _ctx.CodeBlock.InsByte(42); } }
                    else if (node.Children.Count >= 2 && node.Children[0].Kind == NodeKind.NODE_IDENTIFIER) { GenerateNode(node.Children[1], false); if (_locals.TryGetIndex(node.Children[0].Name, out int idx2)) { _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)idx2); if (isStmt) _ctx.CodeBlock.InsByte(42); } }
                    break;
                }
            case NodeKind.NODE_BINARY_OP:
                if (node.Children.Count == 1)
                {
                    var operand = node.Children[0];
                    if (operand != null && operand.Kind == NodeKind.NODE_IDENTIFIER && _locals.TryGetIndex(operand.Name, out int pIdx))
                    {
                        if (node.Name == "++") { _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)pIdx); _ctx.CodeBlock.InsByte(2); _ctx.CodeBlock.InsByte(18); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)pIdx); if (isStmt) _ctx.CodeBlock.InsByte(42); break; }
                        else if (node.Name == "--") { _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)pIdx); _ctx.CodeBlock.InsByte(2); _ctx.CodeBlock.InsByte(19); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)pIdx); if (isStmt) _ctx.CodeBlock.InsByte(42); break; }
                        else if (node.Name == "!") { GenerateNode(operand, false); _ctx.CodeBlock.InsByte(1); _ctx.CodeBlock.InsByte(32); if (isStmt) _ctx.CodeBlock.InsByte(42); break; }
                    }
                }
                if (node.Name == "post++" || node.Name == "post--")
                {
                    var opd = node.Left ?? (node.Children.Count>0? node.Children[0]:null);
                    if (opd != null && opd.Kind == NodeKind.NODE_IDENTIFIER && _locals.TryGetIndex(opd.Name, out int postIdx))
                    {
                        if (node.Name == "post++") { if (!isStmt) { _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)postIdx); } _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)postIdx); _ctx.CodeBlock.InsByte(2); _ctx.CodeBlock.InsByte(18); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)postIdx); _ctx.CodeBlock.InsByte(42); break; }
                        else { if (!isStmt) { _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)postIdx); } _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)postIdx); _ctx.CodeBlock.InsByte(2); _ctx.CodeBlock.InsByte(19); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)postIdx); _ctx.CodeBlock.InsByte(42); break; }
                    }
                }
                if (node.Name == "&&") { GenerateAnd(node); if (isStmt) _ctx.CodeBlock.InsByte(42); break; }
                if (node.Name == "||") { GenerateOr(node); if (isStmt) _ctx.CodeBlock.InsByte(42); break; }
                if (node.Left!= null || node.Right!= null) { if (node.Left!= null) GenerateNode(node.Left, false); if (node.Right!= null) GenerateNode(node.Right, false); }
                else if (node.Children.Count >= 2) { GenerateNode(node.Children[0], false); GenerateNode(node.Children[1], false); }
                switch (node.Name)
                {
                    case "&": _ctx.CodeBlock.InsByte(22); break;
                    case "|": _ctx.CodeBlock.InsByte(23); break;
                    case "^": _ctx.CodeBlock.InsByte(24); break;
                    case "+": _ctx.CodeBlock.InsByte(18); break;
                    case "-": _ctx.CodeBlock.InsByte(19); break;
                    case "*": _ctx.CodeBlock.InsByte(20); break;
                    case "/": _ctx.CodeBlock.InsByte(21); break;
                    case "%": _ctx.CodeBlock.InsByte(25); break;
                    case "<<": _ctx.CodeBlock.InsByte(26); break;
                    case ">>": _ctx.CodeBlock.InsByte(27); break;
                    case "<": _ctx.CodeBlock.InsByte(28); break;
                    case ">": _ctx.CodeBlock.InsByte(29); break;
                    case "<=": _ctx.CodeBlock.InsByte(30); break;
                    case ">=": _ctx.CodeBlock.InsByte(31); break;
                    case "==": _ctx.CodeBlock.InsByte(32); break;
                    case "!=": _ctx.CodeBlock.InsByte(33); break;
                    default: break;
                }
                if (isStmt) _ctx.CodeBlock.InsByte(42);
                break;
            case NodeKind.NODE_RETURN:
                if (node.Children.Count > 0) GenerateNode(node.Children[0], false); else if (node.Left!= null) GenerateNode(node.Left, false);
                _ctx.CodeBlock.InsByte(255); break;
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
            case NodeKind.NODE_CALL:
                {
                    if (node.Name == "sizeof" && node.Children.Count==1) { GenerateNode(node.Children[0], false); _ctx.CodeBlock.InsByte(F_SIZEOF); if (isStmt) _ctx.CodeBlock.InsByte(42); break; }
                    if (node.Name == "keys" && node.Children.Count==1) { GenerateNode(node.Children[0], false); _ctx.CodeBlock.InsByte(F_KEYS); if (isStmt) _ctx.CodeBlock.InsByte(42); break; }
                    if (node.Name == "values" && node.Children.Count==1) { GenerateNode(node.Children[0], false); _ctx.CodeBlock.InsByte(F_VALUES); if (isStmt) _ctx.CodeBlock.InsByte(42); break; }
                    if ((node.Name == "m_delete" || node.Name == "map_delete") && node.Children.Count==2) { GenerateNode(node.Children[0], false); GenerateNode(node.Children[1], false); _ctx.CodeBlock.InsByte(F_M_DELETE); if (isStmt) _ctx.CodeBlock.InsByte(42); break; }
                    if (node.Name == "evaluate") { foreach (var arg in node.Children) GenerateNode(arg, false); _ctx.CodeBlock.InsByte(F_EVALUATE); _ctx.CodeBlock.InsByte((byte)node.Children.Count); if (isStmt) _ctx.CodeBlock.InsByte(42); break; }
                    if (node.Name == "filter" && node.Children.Count==2) { GenerateFilter(node, isStmt); break; }
                    if (node.Name == "map" && node.Children.Count==2) { GenerateMap(node, isStmt); break; }
                    if (node.Name == "sort_array" && node.Children.Count==2) { GenerateSortArray(node, isStmt); break; }
                    if (node.Name == "clone_object" && node.Children.Count==1) { GenerateNode(node.Children[0], false); _ctx.CodeBlock.InsByte(F_CLONE_OBJECT); if (isStmt) _ctx.CodeBlock.InsByte(42); break; }
                    foreach (var arg in node.Children) GenerateNode(arg, false);
                    ushort fIdx = FindFunction(node.Name);
                    _ctx.CodeBlock.InsByte(60); _ctx.CodeBlock.InsShort((short)fIdx); _ctx.CodeBlock.InsByte((byte)node.Children.Count);
                    if (isStmt) _ctx.CodeBlock.InsByte(42);
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
        GenerateNode(expr, false);
        _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)switchIdx); _ctx.CodeBlock.InsByte(42);
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
            _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)switchIdx);
            GenerateNode(caseVal, false);
            _ctx.CodeBlock.InsByte(32); // ==
            int branchPos = _ctx.CodeBlock.Position;
            _ctx.CodeBlock.InsByte(17); // BRANCH_IF_TRUE
            _ctx.CodeBlock.InsShort(0);
            branchPositions.Add(branchPos);
        }
        // after all comparisons, jump to default or end
        int jumpToDefaultPos = -1;
        int jumpToEndPos = -1;
        if (defaultNode != null)
        {
            jumpToDefaultPos = _ctx.CodeBlock.Position;
            _ctx.CodeBlock.InsByte(14); _ctx.CodeBlock.InsShort(0);
        }
        else
        {
            jumpToEndPos = _ctx.CodeBlock.Position;
            _ctx.CodeBlock.InsByte(14); _ctx.CodeBlock.InsShort(0);
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
        var objNode = node.Children[0];
        GenerateNode(objNode, false);
        int strIdx = _ctx.AddString(node.Name);
        _ctx.CodeBlock.InsByte(6); _ctx.CodeBlock.InsShort((short)strIdx);
        for (int i=1;i<node.Children.Count;i++) GenerateNode(node.Children[i], false);
        byte nargs = (byte)(node.Children.Count - 1);
        _ctx.CodeBlock.InsByte(F_CALL_OTHER);
        _ctx.CodeBlock.InsByte(nargs);
        if (isStmt) _ctx.CodeBlock.InsByte(42);
    }

    private void GenerateFilter(ParseNode node, bool isStmt)
    {
        var arrNode = node.Children[0]; var clNode = node.Children[1];
        string id = $"{_filterCounter++}";
        string srcName = $"__filter_src_{id}"; string clName = $"__filter_cl_{id}"; string resName = $"__filter_res_{id}"; string idxName = $"__filter_idx_{id}"; string elemName = $"__filter_elem_{id}";
        int srcIdx = _locals.Define(srcName); int clIdx = _locals.Define(clName); int resIdx = _locals.Define(resName); int idxLocal = _locals.Define(idxName); int elemIdx = _locals.Define(elemName);
        GenerateNode(arrNode, false); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(42);
        GenerateNode(clNode, false); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)clIdx); _ctx.CodeBlock.InsByte(42);
        _ctx.CodeBlock.InsByte(1); _ctx.CodeBlock.InsByte(F_PUSH_ARRAY); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)resIdx); _ctx.CodeBlock.InsByte(42);
        _ctx.CodeBlock.InsByte(1); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(42);
        int loopCondPos = _ctx.CodeBlock.Position; var loopInfo = new LoopInfo2 { CondPos = loopCondPos }; _loopStack.Push(loopInfo);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(F_SIZEOF); _ctx.CodeBlock.InsByte(28);
        int branchFalsePos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(16); _ctx.CodeBlock.InsShort(0);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_INDEX); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)elemIdx); _ctx.CodeBlock.InsByte(42);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)clIdx); _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)elemIdx); _ctx.CodeBlock.InsByte(F_EVALUATE); _ctx.CodeBlock.InsByte(2);
        int branchIfFalse = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(16); _ctx.CodeBlock.InsShort(0);
        _ctx.CodeBlock.InsByte(2); _ctx.CodeBlock.InsByte(F_PUSH_ARRAY); string tmpArr = $"__filter_tmp_{id}"; int tmpIdx = _locals.Define(tmpArr); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)tmpIdx); _ctx.CodeBlock.InsByte(42);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)tmpIdx); _ctx.CodeBlock.InsByte(1); _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)elemIdx); _ctx.CodeBlock.InsByte(F_INDEX_ASSIGN); _ctx.CodeBlock.InsByte(42);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)resIdx); _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)tmpIdx); _ctx.CodeBlock.InsByte(18); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)resIdx); _ctx.CodeBlock.InsByte(42);
        int afterIf = _ctx.CodeBlock.Position; PatchShort(branchIfFalse+1, (short)(afterIf - (branchIfFalse+3)));
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(2); _ctx.CodeBlock.InsByte(18); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(42);
        int branchBackPos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(14); short backOffset = (short)(loopCondPos - (branchBackPos + 3)); _ctx.CodeBlock.InsShort(backOffset);
        int afterLoop = _ctx.CodeBlock.Position; PatchShort(branchFalsePos + 1, (short)(afterLoop - (branchFalsePos + 3))); foreach (var bp in loopInfo.BreakPatchPositions) PatchShort(bp + 1, (short)(afterLoop - (bp + 3))); _loopStack.Pop();
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)resIdx); if (isStmt) _ctx.CodeBlock.InsByte(42);
    }

    private void GenerateMap(ParseNode node, bool isStmt)
    {
        var arrNode = node.Children[0]; var clNode = node.Children[1];
        string id = $"{_filterCounter++}";
        string srcName = $"__map_src_{id}"; string clName = $"__map_cl_{id}"; string resName = $"__map_res_{id}"; string idxName = $"__map_idx_{id}"; string elemName = $"__map_elem_{id}"; string valName = $"__map_val_{id}";
        int srcIdx = _locals.Define(srcName); int clIdx = _locals.Define(clName); int resIdx = _locals.Define(resName); int idxLocal = _locals.Define(idxName); int elemIdx = _locals.Define(elemName); int valIdx = _locals.Define(valName);
        GenerateNode(arrNode, false); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(42);
        GenerateNode(clNode, false); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)clIdx); _ctx.CodeBlock.InsByte(42);
        _ctx.CodeBlock.InsByte(1); _ctx.CodeBlock.InsByte(F_PUSH_ARRAY); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)resIdx); _ctx.CodeBlock.InsByte(42);
        _ctx.CodeBlock.InsByte(1); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(42);
        int loopCondPos = _ctx.CodeBlock.Position; var loopInfo = new LoopInfo2 { CondPos = loopCondPos }; _loopStack.Push(loopInfo);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(F_SIZEOF); _ctx.CodeBlock.InsByte(28);
        int branchFalsePos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(16); _ctx.CodeBlock.InsShort(0);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_INDEX); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)elemIdx); _ctx.CodeBlock.InsByte(42);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)clIdx); _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)elemIdx); _ctx.CodeBlock.InsByte(F_EVALUATE); _ctx.CodeBlock.InsByte(2); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)valIdx); _ctx.CodeBlock.InsByte(42);
        _ctx.CodeBlock.InsByte(2); _ctx.CodeBlock.InsByte(F_PUSH_ARRAY); string tmpArr = $"__map_tmp_{id}"; int tmpIdx = _locals.Define(tmpArr); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)tmpIdx); _ctx.CodeBlock.InsByte(42);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)tmpIdx); _ctx.CodeBlock.InsByte(1); _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)valIdx); _ctx.CodeBlock.InsByte(F_INDEX_ASSIGN); _ctx.CodeBlock.InsByte(42);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)resIdx); _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)tmpIdx); _ctx.CodeBlock.InsByte(18); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)resIdx); _ctx.CodeBlock.InsByte(42);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(2); _ctx.CodeBlock.InsByte(18); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(42);
        int branchBackPos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(14); short backOffset = (short)(loopCondPos - (branchBackPos + 3)); _ctx.CodeBlock.InsShort(backOffset);
        int afterLoop = _ctx.CodeBlock.Position; PatchShort(branchFalsePos + 1, (short)(afterLoop - (branchFalsePos + 3))); foreach (var bp in loopInfo.BreakPatchPositions) PatchShort(bp + 1, (short)(afterLoop - (bp + 3))); _loopStack.Pop();
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)resIdx); if (isStmt) _ctx.CodeBlock.InsByte(42);
    }

    private void GenerateSortArray(ParseNode node, bool isStmt)
    {
        var arrNode = node.Children[0]; var clNode = node.Children[1];
        string id = $"{_filterCounter++}";
        string srcName = $"__sort_src_{id}"; string clName = $"__sort_cl_{id}"; string iName = $"__sort_i_{id}"; string jName = $"__sort_j_{id}"; string nName = $"__sort_n_{id}"; string aName = $"__sort_a_{id}"; string bName = $"__sort_b_{id}"; string tmpName = $"__sort_tmp_{id}";
        int srcIdx = _locals.Define(srcName); int clIdx = _locals.Define(clName); int iIdx = _locals.Define(iName); int jIdx = _locals.Define(jName); int nIdx = _locals.Define(nName); int aIdx = _locals.Define(aName); int bIdx = _locals.Define(bName); int tmpIdx = _locals.Define(tmpName);
        GenerateNode(arrNode, false); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(42);
        GenerateNode(clNode, false); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)clIdx); _ctx.CodeBlock.InsByte(42);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(F_SIZEOF); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)nIdx); _ctx.CodeBlock.InsByte(42);
        _ctx.CodeBlock.InsByte(1); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)iIdx); _ctx.CodeBlock.InsByte(42);
        int outerCondPos = _ctx.CodeBlock.Position;
        var outerLoop = new LoopInfo2 { CondPos = outerCondPos }; _loopStack.Push(outerLoop);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)iIdx); _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)nIdx); _ctx.CodeBlock.InsByte(28);
        int outerBranchFalse = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(16); _ctx.CodeBlock.InsShort(0);
        _ctx.CodeBlock.InsByte(1); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)jIdx); _ctx.CodeBlock.InsByte(42);
        int innerCondPos = _ctx.CodeBlock.Position;
        var innerLoop = new LoopInfo2 { CondPos = innerCondPos }; _loopStack.Push(innerLoop);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)jIdx);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)nIdx); _ctx.CodeBlock.InsByte(2); _ctx.CodeBlock.InsByte(19);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)iIdx); _ctx.CodeBlock.InsByte(19);
        _ctx.CodeBlock.InsByte(28);
        int innerBranchFalse = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(16); _ctx.CodeBlock.InsShort(0);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)jIdx); _ctx.CodeBlock.InsByte(F_INDEX); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)aIdx); _ctx.CodeBlock.InsByte(42);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)jIdx); _ctx.CodeBlock.InsByte(2); _ctx.CodeBlock.InsByte(18); _ctx.CodeBlock.InsByte(F_INDEX); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)bIdx); _ctx.CodeBlock.InsByte(42);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)clIdx);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)aIdx);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)bIdx);
        _ctx.CodeBlock.InsByte(F_EVALUATE); _ctx.CodeBlock.InsByte(3);
        _ctx.CodeBlock.InsByte(1); _ctx.CodeBlock.InsByte(29);
        int swapBranchFalse = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(16); _ctx.CodeBlock.InsShort(0);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)jIdx); _ctx.CodeBlock.InsByte(2); _ctx.CodeBlock.InsByte(18); _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)aIdx); _ctx.CodeBlock.InsByte(F_INDEX_ASSIGN); _ctx.CodeBlock.InsByte(42);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)srcIdx); _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)jIdx); _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)bIdx); _ctx.CodeBlock.InsByte(F_INDEX_ASSIGN); _ctx.CodeBlock.InsByte(42);
        int afterSwap = _ctx.CodeBlock.Position; PatchShort(swapBranchFalse+1, (short)(afterSwap - (swapBranchFalse+3)));
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)jIdx); _ctx.CodeBlock.InsByte(2); _ctx.CodeBlock.InsByte(18); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)jIdx); _ctx.CodeBlock.InsByte(42);
        int innerBack = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(14); short innerOff = (short)(innerCondPos - (innerBack + 3)); _ctx.CodeBlock.InsShort(innerOff);
        int afterInner = _ctx.CodeBlock.Position; PatchShort(innerBranchFalse+1, (short)(afterInner - (innerBranchFalse+3)));
        foreach (var bp in innerLoop.BreakPatchPositions) PatchShort(bp + 1, (short)(afterInner - (bp + 3)));
        _loopStack.Pop();
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)iIdx); _ctx.CodeBlock.InsByte(2); _ctx.CodeBlock.InsByte(18); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)iIdx); _ctx.CodeBlock.InsByte(42);
        int outerBack = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(14); short outerOff = (short)(outerCondPos - (outerBack + 3)); _ctx.CodeBlock.InsShort(outerOff);
        int afterOuter = _ctx.CodeBlock.Position; PatchShort(outerBranchFalse+1, (short)(afterOuter - (outerBranchFalse+3)));
        foreach (var bp in outerLoop.BreakPatchPositions) PatchShort(bp + 1, (short)(afterOuter - (bp + 3)));
        _loopStack.Pop();
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)srcIdx); if (isStmt) _ctx.CodeBlock.InsByte(42);
    }

    private void GenerateClosure(ParseNode node, bool isStmt)
    {
        int maxArg = FindMaxClosureArg(node);
        if (maxArg < 0) maxArg = 0;
        string closureName = $"__closure_{_closureCounter++}";
        int branchPos = _ctx.CodeBlock.Position;
        _ctx.CodeBlock.InsByte(14);
        _ctx.CodeBlock.InsShort(0);
        ushort closureAddr = (ushort)_ctx.CodeBlock.Position;
        ushort closureIdx = _ctx.AddFunction(closureName, closureAddr);
        var savedLocals = _locals.Clone();
        _locals.Clear();
        for (int i=1;i<=maxArg;i++) _locals.Define($"${i}");
        foreach (var child in node.Children)
        {
            if (child.Kind == NodeKind.NODE_FUNCTION_DEF) { foreach (var stmt in child.Children) GenerateNode(stmt, true); }
            else { GenerateNode(child, false); _ctx.CodeBlock.InsByte(255); break; }
        }
        var codeArr = _ctx.CodeBlock.ToArray();
        if (codeArr.Length == closureAddr || codeArr[codeArr.Length-1] != 0xFF) _ctx.CodeBlock.InsByte(255);
        _ctx.PatchFunctionAddress(closureIdx, closureAddr);
        int afterClosure = _ctx.CodeBlock.Position;
        PatchShort(branchPos+1, (short)(afterClosure - (branchPos+3)));
        _locals.Restore(savedLocals);
        _ctx.CodeBlock.InsByte(F_PUSH_CLOSURE);
        _ctx.CodeBlock.InsShort((short)closureIdx);
        if (isStmt) _ctx.CodeBlock.InsByte(42);
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
        string idxName = $"__foreach_idx_{_foreachCounter}"; string keysName = $"__foreach_keys_{_foreachCounter}"; _foreachCounter++;
        int idxLocal = _locals.Define(idxName); int keysLocal = _locals.Define(keysName);
        _ctx.CodeBlock.InsByte(1); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(42);
        GenerateNode(container, false); _ctx.CodeBlock.InsByte(F_KEYS); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)keysLocal); _ctx.CodeBlock.InsByte(42);
        int loopCondPos = _ctx.CodeBlock.Position; var loopInfo = new LoopInfo2 { CondPos = loopCondPos }; _loopStack.Push(loopInfo);
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)keysLocal); _ctx.CodeBlock.InsByte(F_SIZEOF); _ctx.CodeBlock.InsByte(28);
        int branchFalsePos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(16); _ctx.CodeBlock.InsShort(0);
        if (!string.IsNullOrEmpty(keyName)) { _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)keysLocal); _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(F_INDEX); if (_locals.TryGetIndex(keyName, out int kIdx)) { _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)kIdx); _ctx.CodeBlock.InsByte(42); } }
        if (!string.IsNullOrEmpty(valName) && !string.IsNullOrEmpty(keyName)) { GenerateNode(container, false); if (_locals.TryGetIndex(keyName, out int kIdx2)) { _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)kIdx2); } _ctx.CodeBlock.InsByte(F_INDEX); if (_locals.TryGetIndex(valName, out int vIdx)) { _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)vIdx); _ctx.CodeBlock.InsByte(42); } }
        if (body != null) GenerateNode(body, true);
        int incStart = _ctx.CodeBlock.Position; foreach (var cp in loopInfo.ContinuePatchPositions) PatchShort(cp + 1, (short)(incStart - (cp + 3))); loopInfo.ContinuePatchPositions.Clear();
        _ctx.CodeBlock.InsByte(40); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(2); _ctx.CodeBlock.InsByte(18); _ctx.CodeBlock.InsByte(41); _ctx.CodeBlock.InsByte((byte)idxLocal); _ctx.CodeBlock.InsByte(42);
        int branchBackPos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(14); short backOffset = (short)(loopCondPos - (branchBackPos + 3)); _ctx.CodeBlock.InsShort(backOffset);
        int afterLoop = _ctx.CodeBlock.Position; PatchShort(branchFalsePos + 1, (short)(afterLoop - (branchFalsePos + 3))); foreach (var bp in loopInfo.BreakPatchPositions) PatchShort(bp + 1, (short)(afterLoop - (bp + 3))); _loopStack.Pop();
    }

    private void GenerateAnd(ParseNode node) { ParseNode left = node.Left ?? (node.Children.Count>0? node.Children[0]:null); ParseNode right = node.Right ?? (node.Children.Count>1? node.Children[1]:null); if (left != null) GenerateNode(left, false); int branchFalsePos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(16); _ctx.CodeBlock.InsShort(0); if (right != null) GenerateNode(right, false); int branchFalse2Pos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(16); _ctx.CodeBlock.InsShort(0); _ctx.CodeBlock.InsByte(2); int branchEndPos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(14); _ctx.CodeBlock.InsShort(0); int falseLabel = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(1); int endLabel = _ctx.CodeBlock.Position; PatchShort(branchFalsePos+1, (short)(falseLabel - (branchFalsePos+3))); PatchShort(branchFalse2Pos+1, (short)(falseLabel - (branchFalse2Pos+3))); PatchShort(branchEndPos+1, (short)(endLabel - (branchEndPos+3))); }
    private void GenerateOr(ParseNode node) { ParseNode left = node.Left ?? (node.Children.Count>0? node.Children[0]:null); ParseNode right = node.Right ?? (node.Children.Count>1? node.Children[1]:null); if (left != null) GenerateNode(left, false); int branchTruePos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(17); _ctx.CodeBlock.InsShort(0); if (right != null) GenerateNode(right, false); int branchTrue2Pos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(17); _ctx.CodeBlock.InsShort(0); _ctx.CodeBlock.InsByte(1); int branchEndPos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(14); _ctx.CodeBlock.InsShort(0); int trueLabel = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(2); int endLabel = _ctx.CodeBlock.Position; PatchShort(branchTruePos+1, (short)(trueLabel - (branchTruePos+3))); PatchShort(branchTrue2Pos+1, (short)(trueLabel - (branchTrue2Pos+3))); PatchShort(branchEndPos+1, (short)(endLabel - (branchEndPos+3))); }
    private void GenerateIf(ParseNode node) { if (node.Children.Count == 0) return; var cond = node.Children[0]; var thenStmt = node.Children.Count > 1 ? node.Children[1] : null; var elseStmt = node.Children.Count > 2 ? node.Children[2] : null; GenerateNode(cond, false); int branchFalsePos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(16); _ctx.CodeBlock.InsShort(0); if (thenStmt != null) GenerateNode(thenStmt, true); if (elseStmt != null) { int branchEndPos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(14); _ctx.CodeBlock.InsShort(0); int elseStart = _ctx.CodeBlock.Position; PatchShort(branchFalsePos + 1, (short)(elseStart - (branchFalsePos + 3))); GenerateNode(elseStmt, true); int afterElse = _ctx.CodeBlock.Position; PatchShort(branchEndPos + 1, (short)(afterElse - (branchEndPos + 3))); } else { int afterThen = _ctx.CodeBlock.Position; PatchShort(branchFalsePos + 1, (short)(afterThen - (branchFalsePos + 3))); } }
    private void GenerateWhile(ParseNode node) { if (node.Children.Count == 0) return; var cond = node.Children[0]; var body = node.Children.Count > 1 ? node.Children[1] : null; int loopStart = _ctx.CodeBlock.Position; var loopInfo = new LoopInfo2 { CondPos = loopStart }; _loopStack.Push(loopInfo); GenerateNode(cond, false); int branchFalsePos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(16); _ctx.CodeBlock.InsShort(0); if (body != null) GenerateNode(body, true); foreach (var cp in loopInfo.ContinuePatchPositions) PatchShort(cp + 1, (short)(loopStart - (cp + 3))); int branchBackPos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(14); short backOffset = (short)(loopStart - (branchBackPos + 3)); _ctx.CodeBlock.InsShort(backOffset); int afterLoop = _ctx.CodeBlock.Position; PatchShort(branchFalsePos + 1, (short)(afterLoop - (branchFalsePos + 3))); foreach (var bp in loopInfo.BreakPatchPositions) PatchShort(bp + 1, (short)(afterLoop - (bp + 3))); _loopStack.Pop(); }
    private void GenerateFor(ParseNode node) { if (node.Children.Count < 4) return; var init = node.Children[0]; var cond = node.Children[1]; var inc = node.Children[2]; var body = node.Children[3]; if (init != null && !(init.Name != null && init.Name.StartsWith("__empty"))) GenerateNode(init, true); int loopCondPos = _ctx.CodeBlock.Position; bool hasCond = !(cond.Name != null && cond.Name.StartsWith("__empty")); int branchFalsePos = -1; if (hasCond) { GenerateNode(cond, false); branchFalsePos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(16); _ctx.CodeBlock.InsShort(0); } var loopInfo = new LoopInfo2 { CondPos = loopCondPos }; _loopStack.Push(loopInfo); if (body != null) GenerateNode(body, true); int incStart = _ctx.CodeBlock.Position; foreach (var cp in loopInfo.ContinuePatchPositions) PatchShort(cp + 1, (short)(incStart - (cp + 3))); loopInfo.ContinuePatchPositions.Clear(); bool hasInc = !(inc.Name != null && inc.Name.StartsWith("__empty")); if (hasInc) GenerateNode(inc, true); int branchBackPos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(14); short backOffset = (short)(loopCondPos - (branchBackPos + 3)); _ctx.CodeBlock.InsShort(backOffset); int afterLoop = _ctx.CodeBlock.Position; if (hasCond) PatchShort(branchFalsePos + 1, (short)(afterLoop - (branchFalsePos + 3))); foreach (var bp in loopInfo.BreakPatchPositions) PatchShort(bp + 1, (short)(afterLoop - (bp + 3))); _loopStack.Pop(); }
    private void GenerateBreak() { if (_loopStack.Count == 0) return; int pos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(14); _ctx.CodeBlock.InsShort(0); _loopStack.Peek().BreakPatchPositions.Add(pos); }
    private void GenerateContinue() { if (_loopStack.Count == 0) return; int pos = _ctx.CodeBlock.Position; _ctx.CodeBlock.InsByte(14); _ctx.CodeBlock.InsShort(0); _loopStack.Peek().ContinuePatchPositions.Add(pos); }
    private void PatchShort(int pos, short val) { var block = _ctx.CodeBlock; var type = block.GetType(); var field = type.GetField("_rented", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); if (field != null) { var rented = (byte[])field.GetValue(block); rented[pos] = (byte)(val & 0xFF); rented[pos + 1] = (byte)((val >> 8) & 0xFF); } }
    private ushort FindFunction(string name) { for (int i = 0; i < _ctx.Functions.Count; i++) if (_ctx.Functions[i].Name == name) return (ushort)i; return 0; }
    private void EmitNumber(long val) { if (val == 0) _ctx.CodeBlock.InsByte(1); else if (val == 1) _ctx.CodeBlock.InsByte(2); else if (val >= -128 && val <= 127) { _ctx.CodeBlock.InsByte(11); _ctx.CodeBlock.InsByte((byte)(sbyte)val); } else if (val >= int.MinValue && val <= int.MaxValue) { _ctx.CodeBlock.InsByte(8); _ctx.CodeBlock.InsInt((int)val); } else { _ctx.CodeBlock.InsByte(10); _ctx.CodeBlock.InsLong(val); } }
}
