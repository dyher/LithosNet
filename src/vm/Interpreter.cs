using System;
using System.Collections.Generic;
using System.Globalization;

namespace LithosNet.VM;

public sealed class ClosureInstance
{
    public int FuncIdx;
    public SValueS[] Captured = System.Array.Empty<SValueS>();
    public LpcObject Owner;
    public Program OwnerProgram;
}
public sealed class CallFrame
{
    public int ReturnPc;
    public SValueS[] Locals = new SValueS[256];
    public int SpBase;
    public LpcObject CurrentObject;
    public SValueS[] CallerCaptured = System.Array.Empty<SValueS>();
    public byte[] SavedProg;
    public string[] SavedStrings;
    public Dictionary<int,int> SavedFuncAddr;
    public Program SavedProgram;
    public CallFrame(int retPc, int spBase, LpcObject curObj = null)
    {
        ReturnPc = retPc;
        SpBase = spBase;
        CurrentObject = curObj;
        for (int i = 0; i < 256; i++) Locals[i] = SValueS.Invalid;
    }
}

public sealed class PendingInput { public LpcObject Owner; public string FuncName; public SValueS[] ExtraArgs = System.Array.Empty<SValueS>(); public bool IsChar; }
public sealed class InterpreterP9
{
    public static readonly System.Collections.Generic.HashSet<LpcObject> LivingObjects = new();
    public static readonly System.Collections.Generic.HashSet<LpcObject> UserObjects = new();
    public static readonly System.Collections.Generic.HashSet<LpcObject> WizardObjects = new();
    public const int STACK_SIZE = 2000;
    public SValueS[] Stack = new SValueS[STACK_SIZE];
    public int Sp = -1;
    public byte[] Prog = Array.Empty<byte>();
    public int Pc = 0;
    public string[] Strings = Array.Empty<string>();
    public Program CurrentProgram;

    // Neolith core opcodes (from NeolithOpcodes.cs)
    const byte F_POP_VALUE = 0;
    const byte F_PUSH = 1;
    const byte F_EFUN0 = 2;
    const byte F_EFUN1 = 3;
    const byte F_EFUN2 = 4;
    const byte F_EFUN3 = 5;
    const byte F_EFUNV = 6;
    const byte F_NUMBER = 7;
    const byte F_REAL = 8;
    const byte F_LONG = 9;
    const byte F_BYTE = 10;
    const byte F_NBYTE = 11;
    const byte F_STRING = 12;
    const byte F_SHORT_STRING = 13;
    const byte F_CONST0 = 14;
    const byte F_CONST1 = 15;
    const byte F_AGGREGATE = 16;
    const byte F_AGGREGATE_ASSOC = 17;
    const byte F_BRANCH_WHEN_ZERO = 18;
    const byte F_BRANCH_WHEN_NON_ZERO = 19;
    const byte F_BRANCH = 20;
    const byte F_BBRANCH_WHEN_ZERO = 21;
    const byte F_BBRANCH_WHEN_NON_ZERO = 22;
    const byte F_BBRANCH = 23;
    const byte F_BRANCH_NE = 24;
    const byte F_BRANCH_GE = 25;
    const byte F_BRANCH_LE = 26;
    const byte F_BRANCH_EQ = 27;
    const byte F_BBRANCH_LT = 28;
    const byte F_FOREACH = 29;
    const byte F_NEXT_FOREACH = 30;
    const byte F_EXIT_FOREACH = 31;
    const byte F_LOOP_COND_LOCAL = 32;
    const byte F_LOOP_COND_NUMBER = 33;
    const byte F_LOOP_INCR = 34;
    const byte F_WHILE_DEC = 35;
    const byte F_LOR = 36;
    const byte F_LAND = 37;
    const byte F_CATCH = 38;
    const byte F_END_CATCH = 39;
    const byte F_TIME_EXPRESSION = 40;
    const byte F_END_TIME_EXPRESSION = 41;
    const byte F_SWITCH = 42;
    const byte F_CALL_FUNCTION_BY_ADDRESS = 43;
    const byte F_CALL = 43;
    const byte F_CALL_INHERITED = 44;
    const byte F_RETURN = 45;
    const byte F_RETURN_ZERO = 46;
    const byte F_EQ = 47;
    const byte F_NE = 48;
    const byte F_LE = 49;
    const byte F_LT = 50;
    const byte F_GE = 51;
    const byte F_GT = 52;
    const byte F_INC = 53;
    const byte F_DEC = 54;
    const byte F_PRE_INC = 55;
    const byte F_POST_INC = 56;
    const byte F_PRE_DEC = 57;
    const byte F_POST_DEC = 58;
    const byte F_TRANSFER_LOCAL = 59;
    const byte F_LOCAL = 60;
    const byte F_LOCAL_LVALUE = 61;
    const byte F_GLOBAL = 62;
    const byte F_GLOBAL_LVALUE = 63;
    const byte F_MEMBER = 64;
    const byte F_MEMBER_LVALUE = 65;
    const byte F_INDEX = 66;
    const byte F_INDEX_LVALUE = 67;
    const byte F_RINDEX = 68;
    const byte F_RINDEX_LVALUE = 69;
    const byte F_NN_RANGE = 70;
    const byte F_NN_RANGE_LVALUE = 71;
    const byte F_RN_RANGE = 72;
    const byte F_RN_RANGE_LVALUE = 73;
    const byte F_RR_RANGE = 74;
    const byte F_RR_RANGE_LVALUE = 75;
    const byte F_NR_RANGE = 76;
    const byte F_NR_RANGE_LVALUE = 77;
    const byte F_NE_RANGE = 78;
    const byte F_RE_RANGE = 79;
    const byte F_ADD_EQ = 80;
    const byte F_SUB_EQ = 81;
    const byte F_AND_EQ = 82;
    const byte F_OR_EQ = 83;
    const byte F_XOR_EQ = 84;
    const byte F_LSH_EQ = 85;
    const byte F_RSH_EQ = 86;
    const byte F_MULT_EQ = 87;
    const byte F_DIV_EQ = 88;
    const byte F_MOD_EQ = 89;
    const byte F_ASSIGN = 90;
    const byte F_VOID_ADD_EQ = 91;
    const byte F_VOID_ASSIGN = 92;
    const byte F_VOID_ASSIGN_LOCAL = 93;
    const byte F_ADD = 94;
    const byte F_SUBTRACT = 95;
    const byte F_MULTIPLY = 96;
    const byte F_DIVIDE = 97;
    const byte F_MOD = 98;
    const byte F_AND = 99;
    const byte F_OR = 100;
    const byte F_XOR = 101;
    const byte F_LSH = 102;
    const byte F_RSH = 103;
    const byte F_NOT = 104;
    const byte F_NEGATE = 105;
    const byte F_COMPL = 106;
    const byte F_FUNCTION_CONSTRUCTOR = 107;
    const byte F_SIMUL_EFUN = 108;
    const byte F_SSCANF = 109;
    const byte F_PARSE_COMMAND = 110;
    const byte F_NEW_CLASS = 111;
    const byte F_NEW_EMPTY_CLASS = 112;
    const byte F_EXPAND_VARARGS = 113;
    const byte F_CALL_OTHER = 114;
    const byte F_EVALUATE = 115;
    const byte F_PUSH_ARRAY = 116;
    const byte F_PUSH_MAPPING = 117;
    const byte F_CLONE_OBJECT = 122;
    const byte F_PUSH_CLOSURE_CAPTURED = 118;
    const byte F_CAPTURED = 119;
    const byte F_DUP = 120;
    SValueS[] CurrentCaptured = System.Array.Empty<SValueS>();
    const byte F_ASSIGN_LVALUE = 80; // alias for add_eq slot reused? keep for lvalue assign handling

    const byte F_SIZEOF = 200;
    const byte F_KEYS = 201;
    const byte F_VALUES = 202;
    const byte F_M_DELETE = 203;
    const byte F_MAP_ADD = 204;

    SValueS[] Locals = new SValueS[256];
    Stack<CallFrame> CallStack = new();
    Dictionary<int,int> FuncAddr = new();
    LpcObject CurrentObject = null;
    LpcObject CommandGiver = null;

    // Neolith object_present2 (simulate.c): 在 ob.Inventory 依序呼叫 id(baseId)，回傳第 count+1 個符合者（count 是 0-based，來自 "name N" 解析）
    LpcObject Present2(string baseId, int count, LpcObject ob)
    {
        if(ob == null || ob.Destructed) return null;
        int remaining = count;
        foreach(var cand in ob.Inventory) {
            var r = CallApplyInObject(cand, "id", new SValueS[]{ new SValueS(baseId) });
            if(cand.Destructed) return null;
            if(IsTrue(r)) { if(remaining-- > 0) continue; return cand; }
        }
        return null;
    }

    // Neolith move_object (simulate.c): 移動 item 到 dest，依 Neolith 順序呼叫 init()
    void MoveObject(LpcObject item, LpcObject dest)
    {
        // 遞迴檢查：dest 的 ancestor 鏈不能包含 item
        for(var ob = dest; ob != null; ob = ob.Environment)
            if(ob == item) throw new Exception("*Can't move object inside itself.");
        if(dest != null && dest.Destructed) throw new Exception("*Can't move to a destructed object.");
        // 從舊 environment unlink（Neolith: remove_sent + unlink）
        if(item.Environment != null) item.Environment.Inventory.Remove(item);
        // 連結到 dest（prepend：item->next_inv = dest->contains; dest->contains = item）
        item.Environment = dest;
        if(dest == null) return;
        dest.Inventory.Insert(0, item);
        var saveCmd = CommandGiver;
        // 1) living item → init() in dest
        if(LivingObjects.Contains(item)) {
            CommandGiver = item;
            CallApplyInObject(dest, "init", new SValueS[0]);
            if(dest.Destructed || item.Environment != dest) { CommandGiver = saveCmd; return; }
        }
        // 2) 對 dest 內每個其他物件 ob（snapshot，Neolith 用 next_ob 鏈，等價）
        var snapshot = new System.Collections.Generic.List<LpcObject>(dest.Inventory);
        foreach(var ob in snapshot) {
            if(ob == item) continue;
            if(ob.Destructed) throw new Exception("*An object was destructed at call of init()");
            if(LivingObjects.Contains(ob)) {
                CommandGiver = ob;
                CallApplyInObject(item, "init", new SValueS[0]);
                if(item.Environment != dest) { CommandGiver = saveCmd; return; }
            }
            if(item.Destructed) throw new Exception("*The object to be moved was destructed at call of init()!");
            if(LivingObjects.Contains(item)) {
                CommandGiver = item;
                CallApplyInObject(ob, "init", new SValueS[0]);
                if(item.Environment != dest) { CommandGiver = saveCmd; return; }
            }
        }
        if(dest.Destructed) throw new Exception("*The destination to move to was destructed at call of init()!");
        // 3) living dest → init() in item
        if(LivingObjects.Contains(dest)) {
            CommandGiver = dest;
            CallApplyInObject(item, "init", new SValueS[0]);
        }
        CommandGiver = saveCmd;
    }

    // Neolith deep_inventory 收集（array.c）：pre-order DFS
    void CollectDeep(LpcObject ob, System.Collections.Generic.List<LpcObject> outList)
    {
        if(ob == null || ob.Destructed) return;
        foreach(var child in ob.Inventory) {
            if(child.Destructed) continue;
            outList.Add(child);
            CollectDeep(child, outList);
        }
    }
    string CurrentOrigin = "driver";
    string CurrentVerb = "";
    public static PendingInput GlobalPending = null;
    public static void FeedInput(string input) {
        var pend = GlobalPending; if(pend==null) return; GlobalPending=null;
        var args = new System.Collections.Generic.List<SValueS>();
        var sv = new SValueS(); sv.Value = SValue.NewString(input ?? ""); sv.U.String = input ?? ""; args.Add(sv);
        args.AddRange(pend.ExtraArgs);
        try { var interp = new InterpreterV4(); interp.CallApply(pend.FuncName, args.ToArray(), pend.Owner); } catch (System.Exception ex) { System.Console.WriteLine($"[FeedInput {pend.FuncName}] {ex.Message}"); }
    }
    Dictionary<string, SValueS> Globals = new();

    public InterpreterP9()
    {
        for(int i=0;i<STACK_SIZE;i++) Stack[i]=SValueS.Invalid;
        for(int i=0;i<256;i++) Locals[i]=SValueS.Invalid;
    }

    public void SetProgram(Program prog)
    {
        if (prog == null) return;
        CurrentProgram = prog;
        Prog = prog.Code ?? Array.Empty<byte>();
        Strings = prog.Strings ?? Array.Empty<string>();
        FuncAddr.Clear();
        if (prog.FunctionTable != null)
        {
            for (int i = 0; i < prog.FunctionTable.Length; i++) FuncAddr[i] = prog.FunctionTable[i].Address;
        }
    }

    static int LoadInt(byte[] prog, ref int pc)
    {
        int v = prog[pc] | (prog[pc+1]<<8) | (prog[pc+2]<<16) | (prog[pc+3]<<24);
        pc+=4; return v;
    }
    static long LoadLong(byte[] prog, ref int pc)
    {
        long lo = (uint)(prog[pc] | (prog[pc+1]<<8) | (prog[pc+2]<<16) | (prog[pc+3]<<24));
        long hi = (uint)(prog[pc+4] | (prog[pc+5]<<8) | (prog[pc+6]<<16) | (prog[pc+7]<<24));
        pc+=8; return lo | (hi<<32);
    }
    static double LoadDouble(byte[] prog, ref int pc)
    {
        double v = BitConverter.ToDouble(prog, pc);
        pc+=8; return v;
    }
    static short LoadShort(byte[] prog, ref int pc)
    {
        short v = (short)(prog[pc] | (prog[pc+1]<<8));
        pc+=2; return v;
    }

    void Push(SValueS v)
    {
        if(Sp+1 >= STACK_SIZE) throw new Exception("*Stack overflow");
        Stack[++Sp]=v;
    }
    void PushNumber(long n){ Push(SValueS.FromNumber(n)); }
    void PushString(string s){ Push(new SValueS(s)); }
    SValueS GetGlobal(int idx){
        var vars = CurrentObject?.Variables;
        if(vars==null || idx<0 || idx>=vars.Length) return SValueS.FromNumber(0);
        return vars[idx] ?? new SValueS();
    }
    void SetGlobal(int idx, SValueS v){
        if(CurrentObject==null || idx<0) return;
        if(CurrentObject.Variables==null) CurrentObject.Variables = new SValueS[Math.Max(idx+1, CurrentProgram?.NumGlobals ?? 0)];
        if(idx>=CurrentObject.Variables.Length) { var nv = new SValueS[idx+1]; Array.Copy(CurrentObject.Variables, nv, CurrentObject.Variables.Length); for(int i=CurrentObject.Variables.Length;i<nv.Length;i++) nv[i]=new SValueS(); CurrentObject.Variables=nv; }
        CurrentObject.Variables[idx]=v;
    }
    void PushMapping(SValueMapping map){ var sv = SValue.NewMapping(map); var s = new SValueS(); s.Value = sv; s.U.Mapping = map; Push(s); }
    void PushArray(SValueArray arr){ var sv = SValue.NewArray(arr); var s = new SValueS(); s.Value = sv; s.U.Array = arr; Push(s); }
    void PushClosure(int funcIdx){ var cap = new ClosureInstance{ FuncIdx=funcIdx, Captured=System.Array.Empty<SValueS>(), Owner=CurrentObject, OwnerProgram=CurrentObject?.CurrentProgram ?? CurrentProgram }; var sv = SValue.NewFunctionCaptured(funcIdx, cap); var s = new SValueS(); s.Value = sv; s.U.Number = funcIdx; s.U.Object = cap; Push(s); }
    void PushClosureCaptured(int funcIdx, SValueS[] captured){ var cap = new ClosureInstance{ FuncIdx=funcIdx, Captured=captured, Owner=CurrentObject, OwnerProgram=CurrentObject?.CurrentProgram ?? CurrentProgram }; var sv = SValue.NewFunctionCaptured(funcIdx, cap); var s = new SValueS(); s.Value = sv; s.U.Number = funcIdx; s.U.Object = cap; Push(s); }
    void PushObject(LpcObject ob){ var sv = SValue.NewObject(ob); var s = new SValueS(); s.Value = sv; s.U.Object = ob as object; Push(s); }
    SValueS Pop(){ if(Sp<0) throw new Exception($"Pop empty at Pc={Pc-1}"); var v=Stack[Sp]; Stack[Sp]=SValueS.Invalid; Sp--; return v; }
    SValueS Peek(int depth=0){ int idx=Sp-depth; if(idx<0) return SValueS.Invalid; return Stack[idx]; }

    bool IsNil(SValueS sv){ return sv.U.Number==255 || sv.U.Number==-1 || sv.Value.Type==SValueType.T_INVALID; }
    bool IsTrue(SValueS sv)
    {
        if(sv.Value.Type==SValueType.T_NUMBER) return sv.Value.Number!=0;
        if(sv.Value.Type==SValueType.T_OBJECT) return sv.Value.Object!=null || sv.U.Object!=null;
        if(sv.Value.Type==SValueType.T_FUNCTION) return true;
        if(sv.Value.Type==SValueType.T_BUFFER || sv.Value.Type==SValueType.T_CLASS) return true;
        if(sv.U.Number!=0 && sv.U.Number!=255 && sv.U.Number!=-1) return true;
        if(sv.U.String!=null) return sv.U.String.Length!=0;
        if(sv.Value.String!=null) return sv.Value.String.Length!=0;
        if(sv.U.Array!=null) return sv.U.Array.Items.Count!=0;
        if(sv.Value.Array!=null) return sv.Value.Array.Items.Count!=0;
        if(sv.U.Mapping!=null) return sv.U.Mapping.Map.Count!=0;
        if(sv.Value.Mapping!=null) return sv.Value.Mapping.Map.Count!=0;
        if(sv.U.Object!=null) return true;
        return false;
    }

    // Type-strict equality (Neolith/MudOS ==): different types are never equal.
    // Object/Array/Mapping/Buffer/Class compare by reference; Invalid==Invalid is true.
    // Value is canonical; fall back to U when Value's field is null (dual-storage mirror).
    static bool SValuesEqual(SValueS a, SValueS b)
    {
        if (a.Value.Type != b.Value.Type) return false;
        return a.Value.Type switch
        {
            SValueType.T_NUMBER => a.Value.Number == b.Value.Number,
            SValueType.T_STRING => (a.Value.String ?? a.U.String) == (b.Value.String ?? b.U.String),
            SValueType.T_FUNCTION => a.Value.FunctionIndex == b.Value.FunctionIndex,
            SValueType.T_OBJECT => object.ReferenceEquals(a.Value.Object ?? a.U.Object, b.Value.Object ?? b.U.Object),
            SValueType.T_ARRAY => object.ReferenceEquals(a.Value.Array ?? a.U.Array, b.Value.Array ?? b.U.Array),
            SValueType.T_MAPPING => object.ReferenceEquals(a.Value.Mapping ?? a.U.Mapping, b.Value.Mapping ?? b.U.Mapping),
            SValueType.T_BUFFER => object.ReferenceEquals(a.Value.Object ?? a.U.Object, b.Value.Object ?? b.U.Object),
            SValueType.T_CLASS => object.ReferenceEquals(a.Value.Object ?? a.U.Object, b.Value.Object ?? b.U.Object),
            SValueType.T_INVALID => true,
            SValueType.T_LVALUE => object.ReferenceEquals(a.Value.Object ?? (object)a.U.LValue, b.Value.Object ?? (object)b.U.LValue),
            _ => false,
        };
    }

    // Relational comparison for < > <= >= : numbers numerically, strings lexicographically (strcmp).
    // Incomparable type pairs (e.g. number vs string, object vs anything) -> false, so all four ops yield 0.
    static bool TrySValueCompare(SValueS a, SValueS b, out int cmp)
    {
        if (a.Value.Type == SValueType.T_NUMBER && b.Value.Type == SValueType.T_NUMBER)
        {
            cmp = a.Value.Number.CompareTo(b.Value.Number);
            return true;
        }
        if (a.Value.Type == SValueType.T_STRING && b.Value.Type == SValueType.T_STRING)
        {
            cmp = string.CompareOrdinal(a.Value.String ?? a.U.String, b.Value.String ?? b.U.String);
            return true;
        }
        cmp = 0;
        return false;
    }

    void AssignLValueChain(LValueDesc lvd, SValueS val)
    {
        if (lvd==null) return;
        var chain = new List<LValueDesc>();
        var cur=lvd;
        while(cur!=null){ chain.Add(cur); cur=cur.Parent; }
        chain.Reverse();
        if(chain.Count==0 || (chain[0].Kind!=LValueKind.Local && chain[0].Kind!=LValueKind.Global)) return;
        bool isGlobal = chain[0].Kind==LValueKind.Global;
        int rootIdx = isGlobal ? chain[0].GlobalIdx : chain[0].LocalIdx;
        SValueS ReadRoot() => isGlobal ? GetGlobal(rootIdx) : (rootIdx>=0 && rootIdx<Locals.Length ? Locals[rootIdx] : new SValueS());
        void WriteRoot(SValueS v) { if(isGlobal) SetGlobal(rootIdx, v); else if(rootIdx>=0 && rootIdx<Locals.Length) Locals[rootIdx]=v; }
        var rootSv = ReadRoot();

        // if chain is only local => direct assign
        if(chain.Count==1)
        {
            WriteRoot(val);
            return;
        }

        var containerSv = rootSv;
        for(int i=1;i<chain.Count;i++)
        {
            var d=chain[i];
            bool isLast = (i==chain.Count-1);
            if(d.Kind==LValueKind.Index)
            {
                // Determine key: could be number or string
                SValue key;
                bool isNumKey=false;
                int numKey=0;
                string strKey=null;
                if(d.Index!=null)
                {
                    var idxSv=d.Index;
                    if(idxSv.Value.Type==SValueType.T_NUMBER){ numKey=(int)idxSv.Value.Number; isNumKey=true; key=idxSv.Value; }
                    else if(idxSv.Value.Type==SValueType.T_STRING){ strKey=idxSv.Value.String; key=idxSv.Value; }
                    else if(idxSv.U.String!=null){ strKey=idxSv.U.String; key=SValue.NewString(strKey); }
                    else { numKey=(int)idxSv.U.Number; isNumKey=true; key=SValue.NewNumber(numKey); }
                }
                else { key=SValue.NewNumber(0); isNumKey=true; numKey=0; }

                // Try array first
                var arr = containerSv.U.Array ?? containerSv.Value.Array;
                var map = containerSv.U.Mapping ?? containerSv.Value.Mapping;

                if(arr!=null && isNumKey)
                {
                    if(numKey<0) numKey=0;
                    if(isLast)
                    {
                        while(arr.Items.Count<=numKey) arr.Items.Add(SValue.NewNumber(0));
                        arr.Items[numKey]=val.Value;
                        // if root was stored via Value.Array path, update local
                        if(containerSv.Value.Array==arr) { /* need propagate? Locals already points? */ }
                        return;
                    }
                    else
                    {
                        // intermediate: auto-create mapping/array if missing
                        if(numKey>=arr.Items.Count)
                        {
                            while(arr.Items.Count<=numKey) arr.Items.Add(SValue.NewMapping(new SValueMapping()));
                        }
                        var nextVal = arr.Items[numKey];
                        var nextSv = new SValueS();
                        nextSv.Value=nextVal;
                        if(nextVal.Array!=null) nextSv.U.Array=nextVal.Array;
                        if(nextVal.Mapping!=null) nextSv.U.Mapping=nextVal.Mapping;
                        if(nextVal.String!=null) nextSv.U.String=nextVal.String;
                        if(nextVal.Type==SValueType.T_INVALID)
                        {
                            // auto-create mapping for nested lvalue like a[0][key] or mapping
                            var newMap=new SValueMapping();
                            var newSvVal=SValue.NewMapping(newMap);
                            arr.Items[numKey]=newSvVal;
                            nextSv.Value=newSvVal;
                            nextSv.U.Mapping=newMap;
                        }
                        containerSv=nextSv;
                        continue;
                    }
                }
                if(map!=null)
                {
                    if(isLast)
                    {
                        map.Map[key]=val.Value;
                        return;
                    }
                    else
                    {
                        if(!map.Map.TryGetValue(key, out SValue nextVal) || nextVal.Type==SValueType.T_INVALID)
                        {
                            // auto-create nested mapping
                            var newMap=new SValueMapping();
                            var newVal=SValue.NewMapping(newMap);
                            map.Map[key]=newVal;
                            var nextSv=new SValueS(); nextSv.Value=newVal; nextSv.U.Mapping=newMap;
                            containerSv=nextSv;
                        }
                        else
                        {
                            var nextSv=new SValueS(); nextSv.Value=nextVal;
                            if(nextVal.Array!=null) nextSv.U.Array=nextVal.Array;
                            if(nextVal.Mapping!=null) nextSv.U.Mapping=nextVal.Mapping;
                            if(nextVal.String!=null) nextSv.U.String=nextVal.String;
                            containerSv=nextSv;
                        }
                        continue;
                    }
                }
                // root is invalid => create mapping or array depending on key type
                if(rootSv.Value.Type==SValueType.T_INVALID && rootSv.U.Array==null && rootSv.U.Mapping==null)
                {
                    if(isNumKey)
                    {
                        var newArr=new SValueArray();
                        while(newArr.Items.Count<=numKey) newArr.Items.Add(SValue.Invalid);
                        if(isLast) newArr.Items[numKey]=val.Value;
                        else
                        {
                            var innerMap=new SValueMapping();
                            newArr.Items[numKey]=SValue.NewMapping(innerMap);
                            containerSv=new SValueS(); containerSv.Value=SValue.NewMapping(innerMap); containerSv.U.Mapping=innerMap;
                            WriteRoot(new SValueS(){ Value=SValue.NewArray(newArr), U=new SValueU(){ Array=newArr } });
                            if(!isLast) continue;
                            return;
                        }
                        WriteRoot(new SValueS(){ Value=SValue.NewArray(newArr), U=new SValueU(){ Array=newArr } });
                        return;
                    }
                    else
                    {
                        var newMap=new SValueMapping();
                        if(isLast) newMap.Map[key]=val.Value;
                        else
                        {
                            var innerMap=new SValueMapping();
                            newMap.Map[key]=SValue.NewMapping(innerMap);
                            containerSv=new SValueS(); containerSv.Value=SValue.NewMapping(innerMap); containerSv.U.Mapping=innerMap;
                            WriteRoot(new SValueS(){ Value=SValue.NewMapping(newMap), U=new SValueU(){ Mapping=newMap } });
                            continue;
                        }
                        WriteRoot(new SValueS(){ Value=SValue.NewMapping(newMap), U=new SValueU(){ Mapping=newMap } });
                        return;
                    }
                }
            }
            else if(d.Kind==LValueKind.Range)
            {
                // simplified range assign not needed for mapping; handle string/array range?
                if(isLast)
                {
                    // for simplicity assign whole
                    WriteRoot(val);
                    return;
                }
            }
        }
        // fallback
        WriteRoot(val);
    }

    bool IsHexDigit(char c)=> (c>='0'&&c<='9')||(c>='a'&&c<='f')||(c>='A'&&c<='F');

    int InterSscanf(string input, string fmt, List<SValueS> lvals)
    {
        int inputPos=0;
        int fmtPos=0;
        int matched=0;
        int lvalIdx=0;
        while(fmtPos<fmt.Length)
        {
            char fc=fmt[fmtPos];
            if(fc=='%')
            {
                fmtPos++;
                if(fmtPos>=fmt.Length) break;
                if(fmt[fmtPos]=='%')
                {
                    if(inputPos<input.Length && input[inputPos]=='%'){ inputPos++; fmtPos++; }
                    else { int idx=input.IndexOf('%',inputPos); if(idx>=0) inputPos=idx+1; else break; fmtPos++; }
                    continue;
                }
                bool skip=false;
                if(fmt[fmtPos]=='*'){ skip=true; fmtPos++; }
                while(fmtPos<fmt.Length && char.IsDigit(fmt[fmtPos])) fmtPos++;
                if(fmtPos>=fmt.Length) break;
                char spec=fmt[fmtPos]; fmtPos++;
                string delim=null;
                if(spec=='s')
                {
                    int nextPct=fmt.IndexOf('%',fmtPos);
                    if(nextPct!=-1)
                    {
                        string lit=fmt.Substring(fmtPos,nextPct-fmtPos);
                        if(lit.Length>0) delim=lit;
                    }
                }
                if(spec=='d'||spec=='i')
                {
                    while(inputPos<input.Length && char.IsWhiteSpace(input[inputPos])) inputPos++;
                    int start=inputPos;
                    if(start<input.Length && (input[start]=='+'||input[start]=='-')) inputPos++;
                    while(inputPos<input.Length && char.IsDigit(input[inputPos])) inputPos++;
                    if(inputPos==start || (inputPos==start+1 && (input[start]=='+'||input[start]=='-'))) break;
                    string numStr=input.Substring(start,inputPos-start);
                    if(!skip)
                    {
                        if(lvalIdx>=lvals.Count) break;
                        if(long.TryParse(numStr,out long v))
                        {
                            var sv=SValueS.FromNumber(v);
                            if(lvals[lvalIdx].U.LValue!=null) AssignLValueChain(lvals[lvalIdx].U.LValue, sv);
                        }
                        matched++; lvalIdx++;
                    }
                }
                else if(spec=='x'||spec=='X')
                {
                    while(inputPos<input.Length && char.IsWhiteSpace(input[inputPos])) inputPos++;
                    int start=inputPos;
                    bool neg=false;
                    if(start<input.Length && (input[start]=='+'||input[start]=='-')){ if(input[start]=='-') neg=true; inputPos++; }
                    if(inputPos+1<input.Length && input[inputPos]=='0' && (input[inputPos+1]=='x'||input[inputPos+1]=='X')) inputPos+=2;
                    int hexStart=inputPos;
                    while(inputPos<input.Length && IsHexDigit(input[inputPos])) inputPos++;
                    if(inputPos==hexStart) break;
                    string hexStr=input.Substring(hexStart,inputPos-hexStart);
                    long v=0; try{ v=Convert.ToInt64(hexStr,16); if(neg) v=-v; }catch{}
                    if(!skip)
                    {
                        if(lvalIdx>=lvals.Count) break;
                        var sv=SValueS.FromNumber(v);
                        if(lvals[lvalIdx].U.LValue!=null) AssignLValueChain(lvals[lvalIdx].U.LValue, sv);
                        matched++; lvalIdx++;
                    }
                }
                else if(spec=='f')
                {
                    while(inputPos<input.Length && char.IsWhiteSpace(input[inputPos])) inputPos++;
                    int start=inputPos;
                    if(start<input.Length && (input[start]=='+'||input[start]=='-')) inputPos++;
                    bool dot=false;
                    while(inputPos<input.Length && (char.IsDigit(input[inputPos]) || (!dot && input[inputPos]=='.')))
                    { if(input[inputPos]=='.') dot=true; inputPos++; }
                    if(inputPos==start) break;
                    string fStr=input.Substring(start,inputPos-start);
                    if(!skip)
                    {
                        if(lvalIdx>=lvals.Count) break;
                        if(double.TryParse(fStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double dv))
                        {
                            var sv=SValueS.FromNumber((long)dv);
                            if(lvals[lvalIdx].U.LValue!=null) AssignLValueChain(lvals[lvalIdx].U.LValue, sv);
                        }
                        matched++; lvalIdx++;
                    }
                }
                else if(spec=='s')
                {
                    string valStr="";
                    if(delim!=null)
                    {
                        int idx=input.IndexOf(delim,inputPos);
                        if(idx>=0)
                        {
                            valStr=input.Substring(inputPos, idx-inputPos);
                            inputPos=idx+delim.Length;
                            fmtPos+=delim.Length;
                        }
                        else
                        {
                            bool isLast=fmt.IndexOf('%',fmtPos+delim.Length)==-1;
                            if(isLast){ valStr=input.Substring(inputPos); inputPos=input.Length; fmtPos+=delim.Length; }
                            else break;
                        }
                    }
                    else
                    {
                        bool isLastSpec=(fmt.IndexOf('%',fmtPos)==-1);
                        if(isLastSpec)
                        {
                            if(lvalIdx>0)
                            {
                                string rem=inputPos<input.Length?input.Substring(inputPos):"";
                                if(rem.Length>0 && rem[0]==' ') valStr=" "+rem.TrimStart();
                                else valStr=rem.TrimStart();
                                inputPos=input.Length;
                            }
                            else
                            {
                                while(inputPos<input.Length && char.IsWhiteSpace(input[inputPos])) inputPos++;
                                int s=inputPos;
                                while(inputPos<input.Length && !char.IsWhiteSpace(input[inputPos])) inputPos++;
                                valStr=input.Substring(s,inputPos-s);
                            }
                        }
                        else
                        {
                            char nextSpec='\0';
                            int np=fmt.IndexOf('%',fmtPos);
                            if(np!=-1){ int p=np+1; if(p<fmt.Length && fmt[p]=='*') p++; while(p<fmt.Length && char.IsDigit(fmt[p])) p++; if(p<fmt.Length) nextSpec=fmt[p]; }
                            if(nextSpec=='d'||nextSpec=='i'||nextSpec=='x'||nextSpec=='X'||nextSpec=='f')
                            {
                                while(inputPos<input.Length && char.IsWhiteSpace(input[inputPos])) inputPos++;
                                int s=inputPos;
                                int digitPos=-1;
                                for(int k=s;k<input.Length;k++){ if(char.IsDigit(input[k])){ digitPos=k; break; } }
                                if(digitPos==-1) valStr=input.Substring(s);
                                else valStr=input.Substring(s,digitPos-s).Trim();
                                inputPos=s+valStr.Length;
                            }
                            else
                            {
                                while(inputPos<input.Length && char.IsWhiteSpace(input[inputPos])) inputPos++;
                                int s=inputPos;
                                while(inputPos<input.Length && !char.IsWhiteSpace(input[inputPos])) inputPos++;
                                valStr=input.Substring(s,inputPos-s);
                            }
                        }
                    }
                    if(!skip)
                    {
                        if(lvalIdx>=lvals.Count) break;
                        var sv=new SValueS(valStr);
                        if(lvals[lvalIdx].U.LValue!=null) AssignLValueChain(lvals[lvalIdx].U.LValue, sv);
                        matched++; lvalIdx++;
                    }
                }
                else break;
            }
            else
            {
                if(char.IsWhiteSpace(fc))
                {
                    while(inputPos<input.Length && char.IsWhiteSpace(input[inputPos])) inputPos++;
                    while(fmtPos<fmt.Length && char.IsWhiteSpace(fmt[fmtPos])) fmtPos++;
                }
                else
                {
                    if(inputPos<input.Length && input[inputPos]==fc){ inputPos++; fmtPos++; }
                    else break;
                }
            }
        }
        return matched;
    }

        string SprintfFormat(string fmt, List<SValueS> fargs)
    {
        var args = new List<object>();
        foreach(var sv in fargs)
        {
            if(sv.U!=null && sv.U.String!=null) args.Add(sv.U.String);
            else if(sv.Value.String!=null) args.Add(sv.Value.String);
            else if(sv.Value.Type==SValueType.T_NUMBER) args.Add(sv.Value.Number);
            else if(sv.U!=null) args.Add(sv.U.Number);
            else if(sv.U!=null && sv.U.Array!=null) args.Add($"ARRAY({sv.U.Array.Items.Count})");
            else if(sv.Value.Array!=null) args.Add($"ARRAY({sv.Value.Array.Items.Count})");
            else if(sv.U!=null && sv.U.Mapping!=null) args.Add($"MAPPING({sv.U.Mapping.Map.Count})");
            else if(sv.Value.Mapping!=null) args.Add($"MAPPING({sv.Value.Mapping.Map.Count})");
            else args.Add(sv.ToString());
        }
        string result = fmt;
        try{
            int argIdx=0;
            var sb2 = new System.Text.StringBuilder();
            for(int p=0;p<fmt.Length;p++)
            {
                if(fmt[p]=='%' && p+1<fmt.Length)
                {
                    if(fmt[p+1]=='%'){ sb2.Append('%'); p++; continue; }
                    int q=p+1;
                    while(q<fmt.Length && !"sdfOoi%xXc".Contains(fmt[q])) q++;
                    if(q<fmt.Length)
                    {
                        if(argIdx<args.Count)
                        {
                            var a = args[argIdx++];
                            char spec = fmt[q];
                            if(spec=='O' || spec=='o')
                                sb2.Append(a?.ToString() ?? "0");
                            else if(spec=='c')
                            {
                                try { sb2.Append((char)System.Convert.ToInt64(a)); }
                                catch { if(a is string ssc && ssc.Length>0) sb2.Append(ssc[0]); }
                            }
                            else
                                sb2.Append(a?.ToString() ?? "");
                            p=q;
                            continue;
                        }
                        else
                        {
                            p=q;
                            continue;
                        }
                    }
                }
                sb2.Append(fmt[p]);
            }
            result = sb2.ToString();
        }catch{}
        return result;
    }

void HandleEfun(byte efunIdx, byte nargs)
    {
        // helper to get arg as string
        string GetArgString(int idxFrom0)
        {
            if (idxFrom0 <0 || idxFrom0>=nargs) return "";
            var v = Stack[Sp-nargs+1+idxFrom0];
            if(v.U!=null && v.U.String!=null) return v.U.String;
            if(v.Value.String!=null) return v.Value.String;
            if(v.Value.Type==SValueType.T_STRING && v.Value.String!=null) return v.Value.String;
            if(v.Value.Type==SValueType.T_NUMBER) return v.Value.Number.ToString();
            if(v.U!=null) return v.U.Number.ToString();
            return v.ToString();
        }
        long GetArgNumber(int idxFrom0)
        {
            if (idxFrom0 <0 || idxFrom0>=nargs) return 0;
            var v = Stack[Sp-nargs+1+idxFrom0];
            if(v.Value.Type==SValueType.T_NUMBER) return v.Value.Number;
            if(v.U!=null) return v.U.Number;
            if(v.Value.String!=null && long.TryParse(v.Value.String, out long n)) return n;
            return 0;
        }
        SValueS GetArg(int idxFrom0)
        {
            if (idxFrom0 <0 || idxFrom0>=nargs) return SValueS.Invalid;
            return Stack[Sp-nargs+1+idxFrom0];
        }
        
        void HandleStrForm(int kind)
        {
            if(nargs<3){ Sp-=nargs; PushNumber(0); return; }
            var srcSv=GetArg(0); string fun=GetArgString(1); var obSv=GetArg(2);
            LpcObject ob=null;
            if(obSv.U.Object is LpcObject lo) ob=lo;
            else if(obSv.Value.Object is LpcObject lo2) ob=lo2;
            else { string pp=obSv.U.String ?? obSv.Value.String ?? ""; if(!string.IsNullOrEmpty(pp)&&pp.StartsWith("/")){ try{ var mg=ObjectManager.Current; if(mg!=null){ ob=mg.FindObject(pp); if(ob==null){ try{ ob=mg.LoadObject(pp);}catch{}} } }catch{} } }
            var extra=new List<SValueS>(); for(int i=3;i<nargs;i++) extra.Add(GetArg(i));
            Sp-=nargs;
            if(ob==null||ob.CurrentProgram==null){ PushNumber(0); return; }
            System.Func<SValue,SValueS> mk=(v)=>{ var s=new SValueS(); s.Value=v; s.U=new SValueU(); if(v.Type==SValueType.T_STRING) s.U.String=v.String; else if(v.Type==SValueType.T_NUMBER) s.U.Number=v.Number; else if(v.Type==SValueType.T_ARRAY) s.U.Array=v.Array; else if(v.Type==SValueType.T_MAPPING) s.U.Mapping=v.Mapping; else if(v.Type==SValueType.T_OBJECT) s.U.Object=v.Object; return s; };
            System.Func<SValueS,SValue> toVal=(sv)=>{ if(sv.Value.Type!=SValueType.T_INVALID) return sv.Value; if(sv.U!=null&&sv.U.String!=null) return SValue.NewString(sv.U.String); if(sv.U!=null&&sv.U.Array!=null) return SValue.NewArray(sv.U.Array); if(sv.U!=null&&sv.U.Mapping!=null) return SValue.NewMapping(sv.U.Mapping); if(sv.U!=null) return SValue.NewNumber(sv.U.Number); return SValue.NewNumber(0); };
            var mapSrc=srcSv.U.Mapping ?? srcSv.Value.Mapping;
            var arrSrc=srcSv.U.Array ?? srcSv.Value.Array;
            if(kind==2){
                if(arrSrc==null){ PushNumber(0); return; }
                var nm=new SValueMapping();
                foreach(var el in arrSrc.Items.ToList()){ var ca=new List<SValueS>(); ca.Add(el); ca.AddRange(extra); var r=CallApplyInObject(ob,fun,ca.ToArray()); SValue k=toVal(r); if(nm.Map.TryGetValue(k,out SValue ex)){ var ea=ex.Array; var na2=new SValueArray(); if(ea!=null) foreach(var it in ea.Items) na2.Items.Add(it); na2.Items.Add(el); nm.Map[k]=SValue.NewArray(na2); } else { var na2=new SValueArray(); na2.Items.Add(el); nm.Map[k]=SValue.NewArray(na2); } }
                PushMapping(nm); return;
            }
            if(mapSrc!=null){
                var nm=new SValueMapping();
                foreach(var kv in mapSrc.Map.ToList()){ var ca=new List<SValueS>(); ca.Add(mk(kv.Key)); ca.Add(mk(kv.Value)); ca.AddRange(extra); var r=CallApplyInObject(ob,fun,ca.ToArray()); if(kind==0){ if(IsTrue(r)) nm.Map[kv.Key]=kv.Value; } else { nm.Map[kv.Key]=toVal(r); } }
                PushMapping(nm); return;
            }
            if(arrSrc!=null){
                var na=new SValueArray();
                foreach(var el in arrSrc.Items.ToList()){ var ca=new List<SValueS>(); ca.Add(el); ca.AddRange(extra); var r=CallApplyInObject(ob,fun,ca.ToArray()); if(kind==0){ if(IsTrue(r)) na.Items.Add(el); } else { na.Items.Add(r); } }
                PushArray(na); return;
            }
            PushNumber(0);
        }

        string ResolveLpcPath(string lpcPath)
        {
            if(string.IsNullOrEmpty(lpcPath)) return "";
            string trimmed = lpcPath.TrimStart('/');
            string mudlib = System.Environment.GetEnvironmentVariable("MUDLIB") ?? "";
            string[] roots = new string[]{ "/home/tiny/es2_auth/mudlib", "/home/tiny/es2_auth", System.IO.Directory.GetCurrentDirectory(), mudlib };
            foreach(var r in roots) {
                if(string.IsNullOrEmpty(r)) continue;
                string full = System.IO.Path.Combine(r, trimmed);
                if(System.IO.File.Exists(full) || System.IO.Directory.Exists(full)) return full;
                string full2 = System.IO.Path.Combine(r, "mudlib", trimmed);
                if(System.IO.File.Exists(full2) || System.IO.Directory.Exists(full2)) return full2;
            }
            // 若都不存在，回傳第一個 root 下的路徑（供 mkdir/write 用）
            if(!string.IsNullOrEmpty(roots[0])) return System.IO.Path.Combine(roots[0], trimmed);
            return System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), trimmed);
        }
        switch(efunIdx)
        {
                        case 146: HandleStrForm(0); break;
            case 147: HandleStrForm(1); break;
            case 150: HandleStrForm(2); break;
            case 0: // call_other
            {
                if(nargs<2){ Sp-=nargs; PushNumber(0); break; }
                var objSv=GetArg(0);
                var funcSv=GetArg(1);
                string funcName=funcSv.U.String ?? funcSv.Value.String ?? "";
                var callArgs=new List<SValueS>();
                for(int i=2;i<nargs;i++) callArgs.Add(GetArg(i));
                LpcObject ob=null;
                if(objSv.U.Object is LpcObject lo) ob=lo;
                else if(objSv.Value.Object is LpcObject lo2) ob=lo2;
                else {
                    string path = objSv.U.String ?? objSv.Value.String ?? "";
                    if(!string.IsNullOrEmpty(path) && path.StartsWith("/")) {
                        try {
                            var mgr = ObjectManager.Current;
                            if(mgr!=null) {
                                ob = mgr.FindObject(path);
                                if(ob==null) { try { ob = mgr.LoadObject(path); } catch {} }
                            }
                        } catch {}
                    }
                }
                Sp-=nargs;
                if(ob!=null && ob.CurrentProgram!=null)
                {
                    var res=CallApplyInObject(ob, funcName, callArgs.ToArray());
                    Push(res);
                }
                else PushNumber(0);
                break;
            }
            case 63: // write
            {
                var sb = new System.Text.StringBuilder();
                for(int i=0;i<nargs;i++)
                {
                    var v = Stack[Sp-nargs+1+i];
                    string str = null;
                    if(v.Value.Type==SValueType.T_STRING && v.Value.String!=null) str=v.Value.String;
                    else if(v.U!=null && v.U.String!=null) str=v.U.String;
                    else if(v.Value.String!=null) str=v.Value.String;
                    else if(v.Value.Type==SValueType.T_NUMBER) str=v.Value.Number.ToString();
                    else if(v.U!=null) str=v.U.Number.ToString();
                    else str=v.ToString();
                    sb.Append(str);
                }
                Console.Write(sb.ToString());
                Sp-=nargs;
                PushNumber(0);
                break;
            }
            case 205: // debug_message
            {
                var sb = new System.Text.StringBuilder();
                for(int i=0;i<nargs;i++) sb.Append(GetArgString(i));
                Console.WriteLine($"[debug] {sb}");
                Sp-=nargs;
                PushNumber(0);
                break;
            }
            case 151: // sprintf
            {
                // very simplified: support %s %d %O %o etc
                if(nargs==0){ Sp-=nargs; PushString(""); break; }
                string fmt = GetArgString(0);
                var args = new List<object>();
                for(int i=1;i<nargs;i++)
                {
                    var sv = GetArg(i);
                    if(sv.U!=null && sv.U.String!=null) args.Add(sv.U.String);
                    else if(sv.Value.String!=null) args.Add(sv.Value.String);
                    else if(sv.Value.Type==SValueType.T_NUMBER) args.Add(sv.Value.Number);
                    else if(sv.U!=null) args.Add(sv.U.Number);
                    else if(sv.U!=null && sv.U.Array!=null) args.Add($"ARRAY({sv.U.Array.Items.Count})");
                    else if(sv.Value.Array!=null) args.Add($"ARRAY({sv.Value.Array.Items.Count})");
                    else if(sv.U!=null && sv.U.Mapping!=null) args.Add($"MAPPING({sv.U.Mapping.Map.Count})");
                    else if(sv.Value.Mapping!=null) args.Add($"MAPPING({sv.Value.Mapping.Map.Count})");
                    else args.Add(sv.ToString());
                }
                string result = fmt;
                try{
                    // replace %s %d %O sequentially with args
                    // handle %% -> %
                    // simple: for each arg, replace next %[^%]*[sdfOoi]
                    int argIdx=0;
                    var sb2 = new System.Text.StringBuilder();
                    for(int p=0;p<fmt.Length;p++)
                    {
                        if(fmt[p]=='%' && p+1<fmt.Length)
                        {
                            if(fmt[p+1]=='%'){ sb2.Append('%'); p++; continue; }
                            // skip flags/width until specifier
                            int q=p+1;
                            while(q<fmt.Length && !"sdfOoi%xXc".Contains(fmt[q])) q++;
                            if(q<fmt.Length)
                            {
                                if(argIdx<args.Count)
                                {
                                    var a = args[argIdx++];
                                    char spec = fmt[q];
                                    if(spec=='O' || spec=='o')
                                        sb2.Append(a?.ToString() ?? "0");
                                    else if(spec=='c')
                                    {
                                        try { sb2.Append((char)System.Convert.ToInt64(a)); }
                                        catch { if(a is string ssc && ssc.Length>0) sb2.Append(ssc[0]); }
                                    }
                                    else
                                        sb2.Append(a?.ToString() ?? "");
                                    p=q;
                                    continue;
                                }
                                else
                                {
                                    p=q;
                                    continue;
                                }
                            }
                        }
                        sb2.Append(fmt[p]);
                    }
                    result = sb2.ToString();
                }catch{}
                Sp-=nargs;
                PushString(result);
                break;
            }
            case 108: // query_host_name()
            {
                Sp-=nargs;
                PushString(System.Environment.MachineName);
                break;
            }
            case 113: // uptime() - seconds since process start
            {
                long up113 = 0;
                try { up113 = (long)(System.DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds; } catch {}
                if(up113<0) up113 = 0;
                Sp-=nargs;
                PushNumber(up113);
                break;
            }
            case 107: // read_file
            {
                string path = GetArgString(0);
                string content = "";
                try{
                    string mudlib = System.Environment.GetEnvironmentVariable("MUDLIB") ?? "";
                    string trimmed = path.TrimStart('/');
                    // LPC absolute paths always start with / but are virtual relative to mudlib root
                    string[] roots = new string[]{ "/home/tiny/es2_auth/mudlib", "/home/tiny/es2_auth", System.IO.Directory.GetCurrentDirectory(), mudlib };
                    foreach(var r in roots)
                    {
                        if(string.IsNullOrEmpty(r)) continue;
                        string full = System.IO.Path.Combine(r, trimmed);
                        if(System.IO.File.Exists(full)){ content = System.IO.File.ReadAllText(full); break; }
                        // also try if mudlib root already includes mudlib subdir
                        string full2 = System.IO.Path.Combine(r, "mudlib", trimmed);
                        if(System.IO.File.Exists(full2)){ content = System.IO.File.ReadAllText(full2); break; }
                        // try filesystem absolute directly (for debugging)
                        if(System.IO.File.Exists(path)){ content = System.IO.File.ReadAllText(path); break; }
                    }
                    // fallback: if path is already filesystem absolute and exists
                    if(string.IsNullOrEmpty(content) && System.IO.File.Exists(path)) content = System.IO.File.ReadAllText(path);
                }catch{}
                Sp-=nargs;
                PushString(content);
                break;
            }
            case 26: // explode
            {
                string str = GetArgString(0);
                string delim = nargs>1? GetArgString(1) : " ";
                string[] parts = str.Split(new string[]{delim}, System.StringSplitOptions.None);
                var arr = new SValueArray();
                foreach(var p in parts) arr.Items.Add(SValue.NewString(p));
                Sp-=nargs;
                PushArray(arr);
                break;
            }
            case 17: // sizeof
            {
                var v = GetArg(0);
                int sz = 0;
                if(v.U.Array!=null) sz=v.U.Array.Items.Count;
                else if(v.Value.Array!=null) sz=v.Value.Array.Items.Count;
                else if(v.U.Mapping!=null) sz=v.U.Mapping.Map.Count;
                else if(v.Value.Mapping!=null) sz=v.Value.Mapping.Map.Count;
                else if(v.U.String!=null) sz=v.U.String.Length;
                else if(v.Value.String!=null) sz=v.Value.String.Length;
                Sp-=nargs;
                PushNumber(sz);
                break;
            }
            case 18: case 20: // strlen/len
            {
                string s = GetArgString(0);
                Sp-=nargs;
                PushNumber(s.Length);
                break;
            }
            case 72: // values
            {
                var v=GetArg(0);
                var map=v.U.Mapping ?? v.Value.Mapping;
                var arr=new SValueArray();
                if(map!=null) foreach(var kv in map.Map) arr.Items.Add(kv.Value);
                Sp-=nargs;
                PushArray(arr);
                break;
            }
            case 73: // keys
            {
                var v=GetArg(0);
                var map=v.U.Mapping ?? v.Value.Mapping;
                var arr=new SValueArray();
                if(map!=null) foreach(var kv in map.Map) arr.Items.Add(kv.Key);
                else {
                    var arrIn = v.U.Array ?? v.Value.Array;
                    var strIn = v.U.String ?? v.Value.String;
                    if (arrIn != null) for(int i=0;i<arrIn.Items.Count;i++) arr.Items.Add(SValue.NewNumber(i));
                    else if (strIn != null) for(int i=0;i<strIn.Length;i++) arr.Items.Add(SValue.NewNumber(i));
                }
                Sp-=nargs;
                PushArray(arr);
                break;
            }
            case 75: // m_delete / map_delete
            {
                var mapSv=GetArg(0);
                var keySv=GetArg(1);
                var map=mapSv.U.Mapping ?? mapSv.Value.Mapping;
                Sp-=nargs;
                if(map!=null)
                {
                    SValue key=keySv.Value;
                    if(key.Type==SValueType.T_INVALID)
                    {
                        if(keySv.U.String!=null) key=SValue.NewString(keySv.U.String);
                        else key=SValue.NewNumber(keySv.U.Number);
                    }
                    map.Map.Remove(key);
                    PushMapping(map);
                }
                else PushNumber(0);
                break;
            }
            case 127: // query_ip_number
            {
                Sp-=nargs;
                PushString("127.0.0.1");
                break;
            }
            case 119: // ctime([time])
            {
                string s119;
                if(nargs>=1) {
                    long t119 = GetArgNumber(0);
                    try { s119 = System.DateTimeOffset.FromUnixTimeSeconds(t119).ToLocalTime().ToString(); }
                    catch { s119 = System.DateTime.Now.ToString(); }
                } else s119 = System.DateTime.Now.ToString();
                Sp-=nargs;
                PushString(s119);
                break;
            }
            case 139: // time
            {
                Sp-=nargs;
                PushNumber(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                break;
            }
            case 105: // file_size
            {
                string path = GetArgString(0);
                long sz = -1;
                try{
                    string trimmed = path.TrimStart('/');
                    string full = System.IO.Path.Combine("/home/tiny/es2_auth/mudlib", trimmed);
                    if(System.IO.File.Exists(full)) sz = new System.IO.FileInfo(full).Length;
                }catch{}
                Sp-=nargs;
                PushNumber(sz);
                break;
            }
            case 241: // log_file
            case 65: // shout
            {
                Sp-=nargs;
                PushNumber(0);
                break;
            }
            case 27: // implode
            {
                var arrSv = GetArg(0);
                string delim = nargs>1? GetArgString(1) : "";
                var arr = arrSv.U.Array ?? arrSv.Value.Array;
                var parts = new List<string>();
                if(arr!=null) foreach(var it in arr.Items) parts.Add(it.String ?? it.Number.ToString());
                Sp-=nargs;
                PushString(string.Join(delim, parts));
                break;
            }
            case 61: // strsrch
            {
                string s = GetArgString(0);
                string sub = nargs>1? GetArgString(1) : "";
                int start = nargs>2? (int)GetArgNumber(2) : 0;
                int idx = -1;
                if(start>=0 && start < s.Length) idx = s.IndexOf(sub, start);
                else idx = s.IndexOf(sub);
                Sp-=nargs;
                PushNumber(idx);
                break;
            }
            case 8: // clone_object / new — Neolith: 載入 blueprint，建全新 instance，重跑 create()
            {
                string path = GetArgString(0);
                LpcObject clone = null;
                try { clone = ObjectManager.Current.CloneObject(path); } catch {}
                Sp-=nargs;
                if(clone != null) PushObject(clone); else PushNumber(0);
                break;
            }
            case 68: // find_object — Neolith: 只找已載入的，不載入
            {
                string path = GetArgString(0);
                LpcObject ob = null;
                try { ob = ObjectManager.Current.FindObject(path); } catch {}
                Sp-=nargs;
                if(ob != null) PushObject(ob); else PushNumber(0);
                break;
            }
            case 69: // load_object — Neolith: 找或載入 blueprint
            {
                string path = GetArgString(0);
                LpcObject ob = null;
                try { ob = ObjectManager.Current.LoadObject(path); } catch {}
                Sp-=nargs;
                if(ob != null) PushObject(ob); else PushNumber(0);
                break;
            }
            case 88: // objectp
            {
                var v=GetArg(0);
                int isObj = (v.Value.Type==SValueType.T_OBJECT || v.U.Object!=null) ? 1 : 0;
                Sp-=nargs; PushNumber(isObj); break;
            }
            case 82: // stringp
            {
                var v=GetArg(0);
                int isStr = (v.Value.Type==SValueType.T_STRING || v.U.String!=null) ? 1 : 0;
                Sp-=nargs; PushNumber(isStr); break;
            }
            case 78: // intp
            {
                var v=GetArg(0);
                int isInt = (v.Value.Type==SValueType.T_NUMBER) ? 1 : 0;
                Sp-=nargs; PushNumber(isInt); break;
            }
            case 87: // mapp
            {
                var v=GetArg(0);
                int isMap = (v.Value.Type==SValueType.T_MAPPING || v.U.Mapping!=null) ? 1 : 0;
                Sp-=nargs; PushNumber(isMap); break;
            }
            case 85: case 86: // pointerp/arrayp
            {
                var v=GetArg(0);
                int isArr = (v.Value.Type==SValueType.T_ARRAY || v.U.Array!=null) ? 1 : 0;
                Sp-=nargs; PushNumber(isArr); break;
            }
            case 102: // write_file(path, content [, append])
            {
                string path = GetArgString(0);
                string content = GetArgString(1);
                long append = nargs >= 3 ? GetArgNumber(2) : 0;
                bool ok = false;
                try {
                    string full = ResolveLpcPath(path);
                    if(!string.IsNullOrEmpty(full)) {
                        string dir = System.IO.Path.GetDirectoryName(full);
                        if(!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                        if(append != 0) System.IO.File.AppendAllText(full, content);
                        else System.IO.File.WriteAllText(full, content);
                        ok = true;
                    }
                } catch {}
                Sp-=nargs;
                PushNumber(ok ? 1 : 0);
                break;
            }
            case 58: // get_dir
            {
                string path = GetArgString(0);
                var arr = new SValueArray();
                try{
                    string trimmed = path.TrimStart('/');
                    string fullDir = System.IO.Path.Combine("/home/tiny/es2_auth/mudlib", trimmed);
                    string dirPart = System.IO.Path.GetDirectoryName(fullDir) ?? fullDir;
                    string pattern = System.IO.Path.GetFileName(fullDir);
                    if(string.IsNullOrEmpty(pattern)) pattern = "*";
                    if(System.IO.Directory.Exists(dirPart))
                    {
                        foreach(var f in System.IO.Directory.GetFiles(dirPart, pattern))
                        {
                            arr.Items.Add(SValue.NewString(System.IO.Path.GetFileName(f)));
                        }
                    }
                }catch{}
                Sp-=nargs;
                PushArray(arr);
                break;
            }
            case 10: // this_player — Neolith: 回傳 command_giver
            {
                Sp-=nargs;
                if(CommandGiver != null) PushObject(CommandGiver); else PushNumber(0);
                break;
            }
            case 11: case 12: // this_interactive/this_user
            case 2: // this_object
            case 13: case 15: // previous_object/all_previous_objects
            {
                Sp-=nargs;
                if(CurrentObject!=null) PushObject(CurrentObject); else PushNumber(0);
                break;
            }
            case 242: // resolve_path
            {
                string cwd = GetArgString(0);
                string file = GetArgString(1);
                string res = file;
                if(!file.StartsWith("/"))
                {
                    if(!string.IsNullOrEmpty(cwd))
                        res = cwd.TrimEnd('/') + "/" + file;
                    else
                        res = "/" + file;
                }
                Sp-=nargs;
                PushString(res);
                break;
            }
            case 245: // user_path
            {
                string uid = GetArgString(0);
                Sp-=nargs;
                PushString($"/data/{uid}/");
                break;
            }
            case 55: // save_variable
            {
                Sp-=nargs;
                PushString("0");
                break;
            }
            case 56: // restore_variable
            {
                Sp-=nargs;
                PushNumber(0);
                break;
            }
            case 152: // reverse_array(arr)
            {
                var arrSv152=GetArg(0);
                var arr152=arrSv152.U.Array ?? arrSv152.Value.Array;
                var out152=new SValueArray();
                if(arr152!=null){ for(int i=arr152.Items.Count-1;i>=0;i--) out152.Items.Add(arr152.Items[i]); }
                Sp-=nargs;
                PushArray(out152);
                break;
            }
            case 153: // interactive
            {
                Sp-=nargs;
                PushNumber(0);
                break;
            }
            case 64: // tell_object
            {
                Sp-=nargs;
                PushNumber(0);
                break;
            }
            case 246: // move_object(dest) — Neolith f_move_object: 1 參數，移動 this_object()，無回傳值
            {
                LpcObject dest = null;
                var a0 = GetArg(0);
                if(a0.U.Object is LpcObject lo) dest = lo;
                else {
                    string destName = GetArgString(0);
                    if(!string.IsNullOrEmpty(destName)) { try { dest = ObjectManager.Current.LoadObject(destName); } catch {} }
                    if(dest == null) { Sp-=nargs; throw new Exception("move_object failed: could not find destination\n"); }
                }
                var item = CurrentObject;
                if(item == null || item.Destructed) { Sp-=nargs; throw new Exception("move_object(): can't move a destructed object\n"); }
                Sp-=nargs;
                MoveObject(item, dest);
                PushNumber(0);
                break;
            }
            case 250: // first_inventory(str|ob) — Neolith: 回傳 ob.Inventory 的第一個非 destructed，否則 0
            {
                LpcObject ob = null;
                var a0 = GetArg(0);
                if(a0.U.Object is LpcObject lo) ob = lo;
                else {
                    string path = GetArgString(0);
                    if(!string.IsNullOrEmpty(path)) { try { ob = ObjectManager.Current.LoadObject(path); } catch {} }
                    if(ob == null) { Sp-=nargs; throw new Exception("Bad argument 1 to first_inventory()\n"); }
                }
                Sp-=nargs;
                LpcObject first = null;
                if(ob != null && !ob.Destructed) {
                    foreach(var child in ob.Inventory) { if(!child.Destructed) { first = child; break; } }
                }
                if(first != null) PushObject(first); else PushNumber(0);
                break;
            }
            case 251: // next_inventory(ob) — Neolith: 回傳 ob 在其 inventory 中的下一個非 destructed，否則 0
            {
                LpcObject ob = null;
                var a0 = GetArg(0);
                if(a0.U.Object is LpcObject lo) ob = lo;
                LpcObject next = null;
                if(ob != null && !ob.Destructed && ob.Environment != null) {
                    var inv = ob.Environment.Inventory;
                    int idx = inv.IndexOf(ob);
                    for(int i = idx + 1; i < inv.Count; i++) { if(!inv[i].Destructed) { next = inv[i]; break; } }
                }
                Sp-=nargs;
                if(next != null) PushObject(next); else PushNumber(0);
                break;
            }
            case 252: // deep_inventory(ob) — Neolith: pre-order DFS 扁平陣列
            {
                LpcObject ob = null;
                var a0 = GetArg(0);
                if(a0.U.Object is LpcObject lo) ob = lo;
                if(ob == null) { Sp-=nargs; throw new Exception("Bad argument 1 to deep_inventory()\n"); }
                var list = new System.Collections.Generic.List<LpcObject>();
                CollectDeep(ob, list);
                var arr = new SValueArray();
                foreach(var child in list) arr.Items.Add(SValue.NewObject(child));
                Sp-=nargs;
                PushArray(arr);
                break;
            }
            case 77: // clonep
            {
                Sp-=nargs;
                PushNumber(0);
                break;
            }
            case 92: // inherits
            {
                Sp-=nargs;
                PushNumber(0);
                break;
            }
            case 247: // object_name(ob?) — Neolith: 回傳物件的程式名稱；無參數時回傳 this_object()
            {
                LpcObject ob = CurrentObject;
                if(nargs >= 1) { var o = GetArg(0); if(o.U.Object is LpcObject lo) ob = lo; else ob = null; }
                Sp-=nargs;
                if(ob == null || ob.Destructed) { PushNumber(0); break; }
                PushString(ob.ProgramName);
                break;
            }
            case 59: // strcmp
            {
                string a = GetArgString(0);
                string b = GetArgString(1);
                int cmp = string.CompareOrdinal(a, b);
                Sp-=nargs;
                PushNumber(cmp);
                break;
            }
            case 67: // message(class, msg, target, ...)
            {
                string cls = nargs>=1? GetArgString(0):"";
                string msg = nargs>=2? GetArgString(1):"";
                Sp-=nargs; PushNumber(0);
                if(nargs>=3) {
                    var tgt = GetArg(2);
                    // target may be object or array of objects
                    var targets = new System.Collections.Generic.List<LpcObject>();
                    if(tgt.U.Object is LpcObject lo) targets.Add(lo);
                    else if(tgt.Value.Object is LpcObject lo2) targets.Add(lo2);
                    else if(tgt.U.Array!=null) foreach(var it in tgt.U.Array.Items) { if(it.Object is LpcObject l3) targets.Add(l3); }
                    else if(tgt.Value.Array!=null) foreach(var it in tgt.Value.Array.Items) { if(it.Object is LpcObject l3) targets.Add(l3); }
                    foreach(var t in targets) {
                        // if target has receive_message, call it; else Console.Write
                        bool has=false; if(t.CurrentProgram!=null) foreach(var f in t.CurrentProgram.FunctionTable) if(f.Name=="receive_message") { has=true; break; }
                        if(has) {
                            var a1=new SValueS(); a1.Value=SValue.NewString(cls); a1.U.String=cls;
                            var a2=new SValueS(); a2.Value=SValue.NewString(msg); a2.U.String=msg;
                            try { CallApplyInObject(t, "receive_message", new SValueS[]{a1,a2}); } catch {}
                        } else System.Console.Write(msg);
                    }
                    if(targets.Count==0) System.Console.Write(msg);
                } else System.Console.Write(msg);
                break;
            }
            case 66: // receive(str)
            {
                string str = nargs>=1? GetArgString(0):"";
                System.Console.Write(str);
                Sp-=nargs; PushNumber(0);
                break;
            }
            case 30: // input_to(func, extra...)
            {
                string fn = nargs>=1? GetArgString(0):"";
                var extra = new System.Collections.Generic.List<SValueS>(); for(int i=1;i<nargs;i++) extra.Add(GetArg(i));
                GlobalPending = new PendingInput{ Owner=CurrentObject, FuncName=fn, ExtraArgs=extra.ToArray(), IsChar=false };
                Sp-=nargs; PushNumber(0);
                break;
            }
            case 164: // get_char(func, flag|data, data|arg1, arg1|arg2, ...)
            {
                string fn = nargs>=1? GetArgString(0):"";
                long flag=0; int dataIdx=1; int extraStart=2;
                if(nargs>=2) {
                    var a1 = GetArg(1);
                    if(a1.Value.Type==SValueType.T_NUMBER) { flag=a1.Value.Number; dataIdx=2; extraStart=3; }
                    else { flag=0; dataIdx=1; extraStart=2; }
                }
                SValueS dataSv = nargs>dataIdx? GetArg(dataIdx): SValueS.FromNumber(0);
                var extra = new System.Collections.Generic.List<SValueS>(); for(int i=extraStart;i<nargs;i++) extra.Add(GetArg(i));
                // prompt target: first object in extra, else CurrentObject
                LpcObject promptTarget = CurrentObject;
                foreach(var e in extra) { if(e.U.Object is LpcObject lo) { promptTarget=lo; break; } else if(e.Value.Object is LpcObject lo2) { promptTarget=lo2; break; } }
                // call input_prompt(func, flag, data, extra[0] or 0) on promptTarget
                if(promptTarget!=null) {
                    var fSv=new SValueS(); fSv.Value=SValue.NewString(fn); fSv.U.String=fn;
                    var flSv=SValueS.FromNumber(flag);
                    var a0 = extra.Count>0? extra[0] : SValueS.FromNumber(0);
                    try { CallApplyInObject(promptTarget, "input_prompt", new SValueS[]{fSv, flSv, dataSv, a0}); } catch {}
                }
                var cbArgs = new System.Collections.Generic.List<SValueS>(); cbArgs.Add(dataSv); cbArgs.AddRange(extra);
                GlobalPending = new PendingInput{ Owner=CurrentObject, FuncName=fn, ExtraArgs=cbArgs.ToArray(), IsChar=true };
                Sp-=nargs; PushNumber(0);
                break;
            }
            case 159: // lower_case(str)
            {
                string s159=GetArgString(0);
                Sp-=nargs;
                PushString(s159.ToLowerInvariant());
                break;
            }
            case 160: // upper_case(str)
            {
                string s160=GetArgString(0);
                Sp-=nargs;
                PushString(s160.ToUpperInvariant());
                break;
            }
            case 168: // shadow(ob, flag) - CurrentObject shadow ob (flag!=0) / unshadow (flag==0)
            {
                LpcObject tgt168 = null;
                if(nargs>=1) { var o168 = GetArg(0); if(o168.U.Object is LpcObject l168) tgt168 = l168; else if(o168.Value.Object is LpcObject l168b) tgt168 = l168b; }
                long flag168 = nargs>=2 ? GetArgNumber(1) : 1;
                int ok168 = 0;
                if(CurrentObject!=null && tgt168!=null && tgt168!=CurrentObject && !tgt168.Destructed && !CurrentObject.Destructed) {
                    if(flag168!=0) {
                        if(tgt168.ShadowedBy==null && CurrentObject.Shadowing==null) { tgt168.ShadowedBy = CurrentObject; CurrentObject.Shadowing = tgt168; ok168 = 1; }
                    } else {
                        if(tgt168.ShadowedBy==CurrentObject && CurrentObject.Shadowing==tgt168) { tgt168.ShadowedBy = null; CurrentObject.Shadowing = null; ok168 = 1; }
                    }
                }
                Sp-=nargs;
                if(ok168==1 && flag168!=0) PushObject(tgt168); else PushNumber(0);
                break;
            }
            case 169: // query_shadowing(ob) - 誰在 shadow ob
            {
                LpcObject tgt169 = null;
                if(nargs>=1) { var o169 = GetArg(0); if(o169.U.Object is LpcObject l169) tgt169 = l169; else if(o169.Value.Object is LpcObject l169b) tgt169 = l169b; }
                Sp-=nargs;
                if(tgt169!=null && tgt169.ShadowedBy!=null && !tgt169.ShadowedBy.Destructed) PushObject(tgt169.ShadowedBy); else PushNumber(0);
                break;
            }
            case 170: // query_shadowed(ob?) - ob 正在 shadow 誰（無 arg 時查 CurrentObject）
            {
                LpcObject sh170 = CurrentObject;
                if(nargs>=1) { var o170 = GetArg(0); if(o170.U.Object is LpcObject l170) sh170 = l170; else if(o170.Value.Object is LpcObject l170b) sh170 = l170b; }
                Sp-=nargs;
                if(sh170!=null && sh170.Shadowing!=null && !sh170.Shadowing.Destructed) PushObject(sh170.Shadowing); else PushNumber(0);
                break;
            }
            case 167: // regreplace(str, pattern, replacement [, flag]) - flag&1=replace all, else first only
            {
                string s167 = GetArgString(0);
                string pat167 = GetArgString(1);
                string rep167 = GetArgString(2);
                int flag167 = nargs > 3 ? (int)GetArgNumber(3) : 0;
                string out167 = s167;
                try {
                    var re167 = new System.Text.RegularExpressions.Regex(pat167);
                    if ((flag167 & 1) != 0) out167 = re167.Replace(s167, rep167);
                    else out167 = re167.Replace(s167, rep167, 1);
                } catch { out167 = s167; }
                Sp-=nargs;
                PushString(out167);
                break;
            }
            case 166: // query_verb() - 目前 command 的 verb
            {
                Sp-=nargs;
                PushString(CurrentVerb ?? "");
                break;
            }
            case 165: // query_ip_name([ob]) - stub, return localhost
            {
                Sp-=nargs;
                PushString("localhost");
                break;
            }
            case 163: // file_exists(path) - 1=file, -1=dir, 0=missing
            {
                string path163 = GetArgString(0);
                long r163 = 0;
                try {
                    string full163 = ResolveLpcPath(path163);
                    if(!string.IsNullOrEmpty(full163)) {
                        if(System.IO.File.Exists(full163)) r163 = 1;
                        else if(System.IO.Directory.Exists(full163)) r163 = -1;
                    }
                } catch {}
                Sp-=nargs;
                PushNumber(r163);
                break;
            }
            case 162: // allocate(n) - array of n zeros
            {
                long n162 = nargs>=1 ? GetArgNumber(0) : 0;
                var arr162 = new SValueArray();
                if(n162>0 && n162<100000) { for(long i=0;i<n162;i++) arr162.Items.Add(SValue.NewNumber(0)); }
                Sp-=nargs;
                PushArray(arr162);
                break;
            }
            case 161: // trim(str) - trim whitespace both ends
            {
                string s161=GetArgString(0);
                Sp-=nargs;
                PushString(s161.Trim());
                break;
            }
            case 158: // values(mapping)
            {
                if(nargs<1){ Sp-=nargs; PushNumber(0); break; }
                var mapSv158=GetArg(0);
                var map158=mapSv158.U.Mapping ?? mapSv158.Value.Mapping;
                var out158=new SValueArray();
                if(map158!=null){ foreach(var kv in map158.Map) out158.Items.Add(kv.Value); }
                Sp-=nargs;
                PushArray(out158);
                break;
            }
            case 157: // in_input(ob?)
            {
                bool has=false;
                if(nargs>=1) { var o=GetArg(0); LpcObject lo=null; if(o.U.Object is LpcObject l1) lo=l1; else if(o.Value.Object is LpcObject l2) lo=l2; has = GlobalPending!=null && GlobalPending.Owner==lo; }
                else has = GlobalPending!=null && GlobalPending.Owner==CurrentObject;
                Sp-=nargs; PushNumber(has?1:0);
                break;
            }
            case 155: // sort_array(arr, "func", ob [, extra...]) via CallApplyInObject
            {
                if(nargs<3){ Sp-=nargs; PushNumber(0); break; }
                var arrSv155=GetArg(0);
                var arr155=arrSv155.U.Array ?? arrSv155.Value.Array;
                string fun155=GetArgString(1);
                var obSv155=GetArg(2);
                LpcObject ob155=null;
                if(obSv155.U.Object is LpcObject lo155) ob155=lo155;
                else if(obSv155.Value.Object is LpcObject lo155b) ob155=lo155b;
                var extra155=new List<SValueS>(); for(int ei=3;ei<nargs;ei++) extra155.Add(GetArg(ei));
                Sp-=nargs;
                if(ob155==null || arr155==null){ PushNumber(0); break; }
                System.Func<SValueS,SValue> toVal155=(sv)=>{ if(sv.Value.Type!=SValueType.T_INVALID) return sv.Value; if(sv.U!=null&&sv.U.String!=null) return SValue.NewString(sv.U.String); if(sv.U!=null&&sv.U.Array!=null) return SValue.NewArray(sv.U.Array); if(sv.U!=null&&sv.U.Mapping!=null) return SValue.NewMapping(sv.U.Mapping); if(sv.U!=null) return SValue.NewNumber(sv.U.Number); return SValue.NewNumber(0); };
                var list155=new List<SValue>(arr155.Items);
                for(int i=0;i<list155.Count;i++) for(int j=0;j<list155.Count-1-i;j++){
                    var ca155=new List<SValueS>(); ca155.Add(list155[j]); ca155.Add(list155[j+1]); ca155.AddRange(extra155);
                    var r155=CallApplyInObject(ob155, fun155, ca155.ToArray());
                    var rv155=toVal155(r155);
                    bool swap155 = rv155.Type==SValueType.T_NUMBER && rv155.Number>0;
                    if(swap155){ var tmp155=list155[j]; list155[j]=list155[j+1]; list155[j+1]=tmp155; }
                }
                var out155=new SValueArray();
                foreach(var it in list155) out155.Items.Add(it);
                PushArray(out155);
                break;
            }
            case 156: // in_edit - not implemented
            {
                Sp-=nargs; PushNumber(0);
                break;
            }
            case 3: // getuid
            {
                Sp-=nargs; PushString("root"); break;
            }
            case 238: // geteuid
            {
                Sp-=nargs; PushString("root"); break;
            }
            case 237: // export_uid
            case 243: // setuid
            case 244: // setgid
            case 239: // seteuid
            {
                Sp-=nargs; PushNumber(0); break;
            }
            case 19: // file_name(ob?)
            case 249: // base_name(ob?)
            {
                string nm = "";
                if(nargs>=1) { var o=GetArg(0); if(o.U.Object is LpcObject lo) nm=lo.ProgramName ?? ""; else if(o.Value.Object is LpcObject lo2) nm=lo2.ProgramName ?? ""; }
                else if(CurrentObject!=null) nm=CurrentObject.ProgramName ?? "";
                Sp-=nargs; PushString(nm); break;
            }
            case 84: // functionp
            {
                var v=GetArg(0);
                int isFunc = (v.Value.Type==SValueType.T_FUNCTION || v.U.Object is ClosureInstance || v.Value.Object is ClosureInstance) ? 1 : 0;
                Sp-=nargs; PushNumber(isFunc); break;
            }
            case 80: // sort_array(arr) default: numbers numeric, strings lexicographic
            {
                if(nargs<1){ Sp-=nargs; PushNumber(0); break; }
                var arrSv80=GetArg(0);
                var arr80=arrSv80.U.Array ?? arrSv80.Value.Array;
                var out80=new SValueArray();
                if(arr80!=null){
                    var list80=new List<SValue>(arr80.Items);
                    for(int i=0;i<list80.Count;i++) for(int j=0;j<list80.Count-1-i;j++){
                        bool swap80=false; if(list80[j].Type==SValueType.T_NUMBER && list80[j+1].Type==SValueType.T_NUMBER) swap80=list80[j].Number>list80[j+1].Number; else if(list80[j].Type==SValueType.T_STRING && list80[j+1].Type==SValueType.T_STRING) swap80=string.CompareOrdinal(list80[j].String, list80[j+1].String)>0; if(swap80){ var tmp80=list80[j]; list80[j]=list80[j+1]; list80[j+1]=tmp80; }
                    }
                    foreach(var it in list80) out80.Items.Add(it);
                }
                Sp-=nargs;
                PushArray(out80);
                break;
            }
            case 81: // floatp
            {
                var v=GetArg(0);
                int isF = 0; // no distinct float type in SValue, floats are T_NUMBER
                Sp-=nargs; PushNumber(isF); break;
            }
            case 91: // bufferp
            {
                var v=GetArg(0);
                int isB = (v.Value.Type==SValueType.T_BUFFER) ? 1 : 0;
                Sp-=nargs; PushNumber(isB); break;
            }
            case 248: // strncmp
            {
                string a = GetArgString(0);
                string b = GetArgString(1);
                long n = nargs>2 ? GetArgNumber(2) : 0;
                if (n>0) {
                    if (a.Length > n) a = a.Substring(0, (int)n);
                    if (b.Length > n) b = b.Substring(0, (int)n);
                }
                int cmp = string.CompareOrdinal(a, b);
                Sp-=nargs;
                PushNumber(cmp);
                break;
            }

            case 48: // to_int
            {
                var v = GetArg(0);
                long n = 0;
                if(v.Value.Type==SValueType.T_NUMBER) n = v.Value.Number;
                else if(v.Value.Type==SValueType.T_STRING && v.Value.String!=null) {
                    string s = v.Value.String.Trim();
                    if(s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && long.TryParse(s.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out long hx)) n = hx;
                    else if(double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d)) n = (long)d;
                    else n = 0;
                }
                Sp-=nargs;
                PushNumber(n);
                break;
            }
            case 49: // to_float (T_REAL 尚未支援，先回 Number 截斷值)
            {
                var v = GetArg(0);
                long n = 0;
                if(v.Value.Type==SValueType.T_NUMBER) n = v.Value.Number;
                else if(v.Value.Type==SValueType.T_STRING && v.Value.String!=null) {
                    string s = v.Value.String.Trim();
                    if(double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d)) n = (long)d;
                }
                Sp-=nargs;
                PushNumber(n);
                break;
            }
            case 50: // to_string
            {
                var v = GetArg(0);
                string s = "";
                if(v.Value.Type==SValueType.T_STRING && v.Value.String!=null) s = v.Value.String;
                else if(v.U!=null && v.U.String!=null) s = v.U.String;
                else if(v.Value.Type==SValueType.T_NUMBER) s = v.Value.Number.ToString();
                else if(v.U!=null) s = v.U.Number.ToString();
                else if(v.Value.String!=null) s = v.Value.String;
                Sp-=nargs;
                PushString(s);
                break;
            }
            case 51: // member_array(item,arr[,start]) or (arr,item[,start])
            {
                var a0=GetArg(0); var a1=GetArg(1);
                var arr51 = a0.U.Array ?? a0.Value.Array;
                SValueS item51; long start51=0;
                if(nargs>=3){ var sv51=GetArg(2); start51 = sv51.Value.Type==SValueType.T_NUMBER ? sv51.Value.Number : sv51.U.Number; }
                if(arr51!=null){ item51=a1; }
                else { arr51 = a1.U.Array ?? a1.Value.Array; item51=a0; }
                long idx51=-1;
                if(arr51!=null){
                    if(start51<0) start51=0;
                    for(long i=start51;i<arr51.Items.Count;i++){ if(SValuesEqual(item51, arr51.Items[(int)i])){ idx51=i; break; } }
                }
                Sp-=nargs;
                PushNumber(idx51);
                break;
            }
            case 52: // shuffle
            {
                var arrSv = GetArg(0);
                var arr = arrSv.U.Array ?? arrSv.Value.Array;
                var outArr = new SValueArray();
                if(arr!=null) {
                    var list = new List<SValue>(arr.Items);
                    var rnd = new Random();
                    for(int i=list.Count-1;i>0;i--) { int j = rnd.Next(i+1); var tmp = list[i]; list[i] = list[j]; list[j] = tmp; }
                    foreach(var it in list) outArr.Items.Add(it);
                }
                Sp-=nargs;
                PushArray(outArr);
                break;
            }
            case 53: // unique_array
            {
                var arrSv = GetArg(0);
                var arr = arrSv.U.Array ?? arrSv.Value.Array;
                var outArr = new SValueArray();
                if(arr!=null) {
                    var seen = new List<SValueS>();
                    foreach(var it in arr.Items) {
                        bool dup = false;
                        foreach(var s in seen) if(SValuesEqual(it, s)) { dup = true; break; }
                        if(!dup) { seen.Add(it); outArr.Items.Add(it); }
                    }
                }
                Sp-=nargs;
                PushArray(outArr);
                break;
            }
            case 54: // copy
            {
                var v = GetArg(0);
                var arr = v.U.Array ?? v.Value.Array;
                var map = v.U.Mapping ?? v.Value.Mapping;
                Sp-=nargs;
                if(arr!=null) {
                    var na = new SValueArray();
                    foreach(var it in arr.Items) na.Items.Add(it);
                    PushArray(na);
                } else if(map!=null) {
                    var nm = new SValueMapping();
                    foreach(var kv in map.Map) nm.Map[kv.Key] = kv.Value;
                    PushMapping(nm);
                } else {
                    Push(v);
                }
                break;
            }

            
            case 40: // call_out(func, delay, args...)
            {
                string func = GetArgString(0);
                long delaySec = GetArgNumber(1);
                var coArgs = new List<SValueS>();
                for(int i=2;i<nargs;i++) coArgs.Add(GetArg(i));
                int handle = 0;
                if(!string.IsNullOrEmpty(func) && CurrentObject != null) {
                    handle = CallOutManager.Current.CallOut(CurrentObject, func, (int)(delaySec * 1000), coArgs.ToArray());
                }
                Sp-=nargs;
                PushNumber(handle);
                break;
            }
            case 41: // remove_call_out(handle)
            {
                long handle = GetArgNumber(0);
                bool ok = CallOutManager.Current.RemoveCallOut((int)handle);
                Sp-=nargs;
                PushNumber(ok ? 1 : 0);
                break;
            }
            case 42: // find_call_out(func)
            {
                string func = GetArgString(0);
                int handle = -1;
                if(!string.IsNullOrEmpty(func) && CurrentObject != null) {
                    handle = CallOutManager.Current.FindCallOut(CurrentObject, func);
                }
                Sp-=nargs;
                PushNumber(handle);
                break;
            }
            case 43: // set_heart_beat(flag)
            {
                long flag = GetArgNumber(0);
                if(CurrentObject != null) HeartBeatManager.Current.SetHeartBeat(CurrentObject, flag != 0);
                Sp-=nargs;
                PushNumber(flag != 0 ? 1 : 0);
                break;
            }
            case 14: // query_heart_beat([ob])
            {
                LpcObject ob = CurrentObject;
                if(nargs >= 1) {
                    var o = GetArg(0);
                    if(o.U.Object is LpcObject lo) ob = lo;
                    else if(o.Value.Object is LpcObject lo2) ob = lo2;
                    else {
                        string p = o.U.String ?? o.Value.String ?? "";
                        if(!string.IsNullOrEmpty(p) && p.StartsWith("/")) {
                            try { ob = ObjectManager.Current?.FindObject(p); } catch {}
                        }
                    }
                }
                bool on = HeartBeatManager.Current.HasHeartBeat(ob);
                Sp-=nargs;
                PushNumber(on ? 1 : 0);
                break;
            }
            case 25: // destruct([ob])
            {
                LpcObject ob = CurrentObject;
                if(nargs >= 1) {
                    var o = GetArg(0);
                    if(o.U.Object is LpcObject lo) ob = lo;
                    else if(o.Value.Object is LpcObject lo2) ob = lo2;
                    else {
                        string p = o.U.String ?? o.Value.String ?? "";
                        if(!string.IsNullOrEmpty(p) && p.StartsWith("/")) {
                            try { ob = ObjectManager.Current?.FindObject(p); } catch {}
                        }
                    }
                }
                bool ok = false;
                if(ob != null && ObjectManager.Current != null) {
                    try { ok = ObjectManager.Current.Destruct(ob); } catch {}
                }
                Sp-=nargs;
                PushNumber(ok ? 1 : 0);
                break;
            }

            case 47: // crypt(str, salt)
            {
                string str = GetArgString(0);
                string salt = "";
                if(nargs >= 2) {
                    var sv = GetArg(1);
                    if(sv.Value.Type==SValueType.T_STRING) salt = sv.Value.String ?? "";
                    else if(sv.U!=null && sv.U.String!=null) salt = sv.U.String;
                    else if(sv.Value.Type==SValueType.T_NUMBER) salt = sv.Value.Number == 0 ? "" : sv.Value.Number.ToString();
                }
                // salt: 若是現有 hash，取前 2 字元；若為空，隨機產生 2 字元
                string useSalt;
                if(!string.IsNullOrEmpty(salt) && salt.Length >= 2) useSalt = salt.Substring(0, 2);
                else if(!string.IsNullOrEmpty(salt)) useSalt = salt.PadRight(2, 'A').Substring(0, 2);
                else {
                    const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789./";
                    var rnd = new Random();
                    useSalt = "" + chars[rnd.Next(chars.Length)] + chars[rnd.Next(chars.Length)];
                }
                // SHA256(salt + password)，取前 11 碼 hex，組成 13 字元（相容 DES crypt 長度）
                string hash;
                using(var sha = System.Security.Cryptography.SHA256.Create()) {
                    var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(useSalt + str));
                    hash = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant().Substring(0, 11);
                }
                Sp-=nargs;
                PushString(useSalt + hash);
                break;
            }
            case 16: // regexp(str, pattern) | regexp(array, pattern [, flag])
            {
                string pattern = GetArgString(1);
                var arg0 = GetArg(0);
                var arr = arg0.U.Array ?? arg0.Value.Array;
                int flag = nargs > 2 ? (int)GetArgNumber(2) : 0;
                string strArg = GetArgString(0);
                Sp-=nargs;
                if (arr != null) {
                    bool returnPairs = (flag & 1) != 0;
                    bool returnNonMatches = (flag & 2) != 0;
                    var result = new SValueArray();
                    for (int i = 0; i < arr.Items.Count; i++) {
                        var item = arr.Items[i];
                        string s = item.String ?? "";
                        bool matches = false;
                        try { matches = System.Text.RegularExpressions.Regex.IsMatch(s, pattern); } catch { matches = false; }
                        bool include = returnNonMatches ? !matches : matches;
                        if (include) {
                            if (returnPairs) {
                                result.Items.Add(SValue.NewNumber(i));
                                result.Items.Add(item);
                            } else {
                                result.Items.Add(item);
                            }
                        }
                    }
                    PushArray(result);
                } else {
                    string s = strArg;
                    bool matches = false;
                    try { matches = System.Text.RegularExpressions.Regex.IsMatch(s, pattern); } catch { matches = false; }
                    PushNumber(matches ? 1 : 0);
                }
                break;
            }
            case 71: // reg_assoc(str, patterns, tokens [, def])
            {
                string str = GetArgString(0);
                var patSv = GetArg(1);
                var tokSv = nargs > 2 ? GetArg(2) : SValueS.Invalid;
                var defSv = nargs > 3 ? GetArg(3) : SValueS.Invalid;
                var patArr = patSv.U.Array ?? patSv.Value.Array;
                var tokArr = tokSv.U.Array ?? tokSv.Value.Array;
                var segments = new SValueArray();
                var tokensFound = new SValueArray();
                SValue defVal = defSv.Value;
                if (defVal.Type == SValueType.T_INVALID && defSv.U != null) {
                    if (defSv.U.String != null) defVal = SValue.NewString(defSv.U.String);
                    else defVal = SValue.NewNumber(defSv.U.Number);
                }
                if (defVal.Type == SValueType.T_INVALID) defVal = SValue.NewNumber(0);
                var patterns = new List<string>();
                if (patArr != null) foreach (var pp in patArr.Items) patterns.Add(pp.String ?? "");
                string current = "";
                int pos = 0;
                while (pos < str.Length) {
                    bool matched = false;
                    for (int pi = 0; pi < patterns.Count; pi++) {
                        string pat = patterns[pi];
                        if (string.IsNullOrEmpty(pat)) continue;
                        System.Text.RegularExpressions.Match m = null;
                        try { m = System.Text.RegularExpressions.Regex.Match(str.Substring(pos), "^(?:" + pat + ")"); } catch { m = null; }
                        if (m != null && m.Success && m.Length > 0) {
                            if (current.Length > 0) {
                                segments.Items.Add(SValue.NewString(current));
                                tokensFound.Items.Add(defVal);
                                current = "";
                            }
                            segments.Items.Add(SValue.NewString(m.Value));
                            SValue tokVal = defVal;
                            if (tokArr != null && pi < tokArr.Items.Count) tokVal = tokArr.Items[pi];
                            tokensFound.Items.Add(tokVal);
                            pos += m.Length;
                            matched = true;
                            break;
                        }
                    }
                    if (!matched) {
                        current += str[pos];
                        pos++;
                    }
                }
                if (current.Length > 0) {
                    segments.Items.Add(SValue.NewString(current));
                    tokensFound.Items.Add(defVal);
                }
                var result = new SValueArray();
                result.Items.Add(SValue.NewArray(segments));
                result.Items.Add(SValue.NewArray(tokensFound));
                Sp-=nargs;
                PushArray(result);
                break;
            }
            case 124: // crc32(str | buffer)
            {
                var arg0 = GetArg(0);
                byte[] bytes;
                if (arg0.Value.Type == SValueType.T_BUFFER && arg0.Value.Object is byte[] bb1) bytes = bb1;
                else if (arg0.U.Object is byte[] bb2) bytes = bb2;
                else bytes = System.Text.Encoding.UTF8.GetBytes(GetArgString(0));
                uint crc = 0xFFFFFFFF;
                foreach (byte b in bytes) {
                    crc ^= b;
                    for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
                }
                crc ^= 0xFFFFFFFF;
                Sp-=nargs;
                PushNumber((long)crc);
                break;
            }
            case 126: // c_str(str) - truncate at first NUL
            {
                string s = GetArgString(0);
                int idx = s.IndexOf('\0');
                if (idx >= 0) s = s.Substring(0, idx);
                Sp-=nargs;
                PushString(s);
                break;
            }
            case 128: // envsubst(str) - expand $VAR and ${VAR}
            {
                string s = GetArgString(0);
                string result = System.Text.RegularExpressions.Regex.Replace(s, @"\$(\{([A-Za-z_][A-Za-z0-9_]*)\}|([A-Za-z_][A-Za-z0-9_]*))", m => {
                    string varName = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
                    return System.Environment.GetEnvironmentVariable(varName) ?? "";
                });
                Sp-=nargs;
                PushString(result);
                break;
            }
            case 129: // oldcrypt(str, salt) - same SHA256-based scheme as crypt
            {
                string str = GetArgString(0);
                string salt = "";
                if (nargs >= 2) {
                    var sv = GetArg(1);
                    if (sv.Value.Type == SValueType.T_STRING) salt = sv.Value.String ?? "";
                    else if (sv.U != null && sv.U.String != null) salt = sv.U.String;
                    else if (sv.Value.Type == SValueType.T_NUMBER) salt = sv.Value.Number == 0 ? "" : sv.Value.Number.ToString();
                }
                string useSalt;
                if (!string.IsNullOrEmpty(salt) && salt.Length >= 2) useSalt = salt.Substring(0, 2);
                else if (!string.IsNullOrEmpty(salt)) useSalt = salt.PadRight(2, 'A').Substring(0, 2);
                else {
                    const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789./";
                    var rnd = new Random();
                    useSalt = "" + chars[rnd.Next(chars.Length)] + chars[rnd.Next(chars.Length)];
                }
                string hash;
                using (var sha = System.Security.Cryptography.SHA256.Create()) {
                    var bts = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(useSalt + str));
                    hash = BitConverter.ToString(bts).Replace("-", "").ToLowerInvariant().Substring(0, 11);
                }
                Sp-=nargs;
                PushString(useSalt + hash);
                break;
            }
            case 130: // strwrap(str [, width [, indent]]) - Neolith: not implemented, return str
            {
                string s = GetArgString(0);
                Sp-=nargs;
                PushString(s);
                break;
            }
            case 131: // match_path(mapping, path)
            {
                var mapSv = GetArg(0);
                string path = GetArgString(1);
                var map = mapSv.U.Mapping ?? mapSv.Value.Mapping;
                Sp-=nargs;
                if (map == null) { PushNumber(0); break; }
                string[] parts = path.Split(new char[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
                SValue cur = SValue.NewMapping(map);
                bool found = true;
                foreach (string part in parts) {
                    var curMap = cur.Mapping;
                    if (curMap == null) { found = false; break; }
                    SValue key = SValue.NewString(part);
                    if (!curMap.Map.TryGetValue(key, out SValue next)) { found = false; break; }
                    cur = next;
                }
                if (found) Push((SValueS)cur);
                else PushNumber(0);
                break;
            }
            case 38: // error(msg)
            {
                string msg = GetArgString(0);
                Sp-=nargs;
                throw new Exception(msg);
            }
            case 39: // throw(msg)
            {
                string msg = GetArgString(0);
                Sp-=nargs;
                throw new Exception(msg);
            }
            case 34: // rm(path)
            {
                string path = GetArgString(0);
                bool ok = false;
                try {
                    string full = ResolveLpcPath(path);
                    if(!string.IsNullOrEmpty(full) && System.IO.File.Exists(full)) {
                        System.IO.File.Delete(full);
                        ok = true;
                    }
                } catch {}
                Sp-=nargs;
                PushNumber(ok ? 1 : 0);
                break;
            }
            case 35: // mkdir(path)
            {
                string path = GetArgString(0);
                bool ok = false;
                try {
                    string full = ResolveLpcPath(path);
                    if(!string.IsNullOrEmpty(full)) {
                        System.IO.Directory.CreateDirectory(full);
                        ok = true;
                    }
                } catch {}
                Sp-=nargs;
                PushNumber(ok ? 1 : 0);
                break;
            }
            case 36: // cp(src, dst)
            {
                string src = GetArgString(0);
                string dst = GetArgString(1);
                bool ok = false;
                try {
                    string fullSrc = ResolveLpcPath(src);
                    string fullDst = ResolveLpcPath(dst);
                    if(!string.IsNullOrEmpty(fullSrc) && !string.IsNullOrEmpty(fullDst) && System.IO.File.Exists(fullSrc)) {
                        System.IO.File.Copy(fullSrc, fullDst, true);
                        ok = true;
                    }
                } catch {}
                Sp-=nargs;
                PushNumber(ok ? 1 : 0);
                break;
            }
            case 37: // rename(src, dst)
            {
                string src = GetArgString(0);
                string dst = GetArgString(1);
                bool ok = false;
                try {
                    string fullSrc = ResolveLpcPath(src);
                    string fullDst = ResolveLpcPath(dst);
                    if(!string.IsNullOrEmpty(fullSrc) && !string.IsNullOrEmpty(fullDst) && System.IO.File.Exists(fullSrc)) {
                        System.IO.File.Move(fullSrc, fullDst, true);
                        ok = true;
                    }
                } catch {}
                Sp-=nargs;
                PushNumber(ok ? 1 : 0);
                break;
            }

            case 4: // random(n)
            {
                long n = GetArgNumber(0);
                long r = 0;
                if(n > 0) r = Random.Shared.NextInt64(n);
                else if(n < 0) r = -Random.Shared.NextInt64(-n);
                Sp-=nargs;
                PushNumber(r);
                break;
            }
            case 44: // capitalize(str)
            {
                string s = GetArgString(0);
                if(s.Length > 0) s = char.ToUpperInvariant(s[0]) + (s.Length > 1 ? s.Substring(1) : "");
                Sp-=nargs;
                PushString(s);
                break;
            }
            case 31: // undefinedp(x)
            {
                var v = GetArg(0);
                int isUndef = (v.Value.Type == SValueType.T_INVALID) ? 1 : 0;
                Sp-=nargs;
                PushNumber(isUndef);
                break;
            }
            case 60: // allocate_mapping(n)
            {
                var map = new SValueMapping();
                Sp-=nargs;
                PushMapping(map);
                break;
            }
            case 104: // nullp(x) — LPC 裡 0 即 null
            {
                var v = GetArg(0);
                int isNull = 0;
                if(v.Value.Type == SValueType.T_INVALID) isNull = 1;
                else if(v.Value.Type == SValueType.T_NUMBER && v.Value.Number == 0) isNull = 1;
                Sp-=nargs;
                PushNumber(isNull);
                break;
            }
            case 112: // sqrt(n)
            {
                long n = GetArgNumber(0);
                long r = n >= 0 ? (long)Math.Sqrt(n) : 0;
                Sp-=nargs;
                PushNumber(r);
                break;
            }
            case 132: // acos(n) - int 截斷, 超範圍回 0, 照 sqrt 模式
            {
                long n = GetArgNumber(0);
                long r = 0; if (n >= -1 && n <= 1) r = (long)Math.Acos((double)n);
                Sp-=nargs; PushNumber(r); break;
            }
            case 133: // asin(n)
            {
                long n = GetArgNumber(0);
                long r = 0; if (n >= -1 && n <= 1) r = (long)Math.Asin((double)n);
                Sp-=nargs; PushNumber(r); break;
            }
            case 134: // atan(n)
            {
                long n = GetArgNumber(0);
                long r = (long)Math.Atan((double)n);
                Sp-=nargs; PushNumber(r); break;
            }
            case 135: // cos(n)
            {
                long n = GetArgNumber(0);
                long r = (long)Math.Cos((double)n);
                Sp-=nargs; PushNumber(r); break;
            }
            case 136: // sin(n)
            {
                long n = GetArgNumber(0);
                long r = (long)Math.Sin((double)n);
                Sp-=nargs; PushNumber(r); break;
            }
            case 137: // tan(n)
            {
                long n = GetArgNumber(0);
                long r = (long)Math.Tan((double)n);
                Sp-=nargs; PushNumber(r); break;
            }
            case 138: // exp(n)
            {
                long n = GetArgNumber(0);
                long r = 0; if (n < 20) r = (long)Math.Exp((double)n);
                Sp-=nargs; PushNumber(r); break;
            }
            case 140: // pow(x, y)
            {
                long x = GetArgNumber(0); long y = GetArgNumber(1);
                long r = 0; if (y >= 0 && y < 30) r = (long)Math.Pow((double)x, (double)y);
                Sp-=nargs; PushNumber(r); break;
            }
            case 141: // ceil(n) - T_REAL 未支援前 int 恆等
            {
                long n = GetArgNumber(0);
                Sp-=nargs; PushNumber(n); break;
            }
            case 142: // floor(n) - 同上
            {
                long n = GetArgNumber(0);
                Sp-=nargs; PushNumber(n); break;
            }
            case 143: // set_bit(str, bit) - 每字元 6 bits, 字元 = 32 + bits, 不改原字串
            {
                string s = GetArgString(0); long bit = GetArgNumber(1);
                string out_ = s;
                if (bit >= 0) { int idx2 = (int)(bit / 6); int off = (int)(bit % 6); if (s.Length <= idx2) s = s + new string(' ', idx2 + 1 - s.Length); char[] ch = s.ToCharArray(); int v = ch[idx2] - 32; if (v < 0 || v > 63) v = 0; v |= (1 << off); ch[idx2] = (char)(32 + v); out_ = new string(ch); }
                Sp-=nargs; PushString(out_); break;
            }
            case 144: // clear_bit(str, bit)
            {
                string s = GetArgString(0); long bit = GetArgNumber(1);
                string out_ = s;
                if (bit >= 0) { int idx2 = (int)(bit / 6); int off = (int)(bit % 6); if (s.Length <= idx2) s = s + new string(' ', idx2 + 1 - s.Length); char[] ch = s.ToCharArray(); int v = ch[idx2] - 32; if (v < 0 || v > 63) v = 0; v &= ~(1 << off); ch[idx2] = (char)(32 + v); out_ = new string(ch); }
                Sp-=nargs; PushString(out_); break;
            }
            case 145: // test_bit(str, bit)
            {
                string s = GetArgString(0); long bit = GetArgNumber(1);
                int r = 0; if (bit >= 0) { int idx2 = (int)(bit / 6); int off = (int)(bit % 6); if (idx2 < s.Length) { int v = s[idx2] - 32; if (v >= 0 && v <= 63) r = (v >> off) & 1; } }
                Sp-=nargs; PushNumber(r); break;
            }
            case 101: // repeat_string(str, n)
            {
                string s = GetArgString(0);
                long n = GetArgNumber(1);
                string out_ = "";
                if(n > 0 && n < 10000 && s.Length > 0) {
                    var sb = new System.Text.StringBuilder();
                    for(long i=0;i<n;i++) sb.Append(s);
                    out_ = sb.ToString();
                }
                Sp-=nargs;
                PushString(out_);
                break;
            }
            case 95: // typeof(x) — 回傳型別名稱字串
            {
                var v = GetArg(0);
                string tn = "int";
                switch(v.Value.Type) {
                    case SValueType.T_NUMBER: tn = "int"; break;
                    case SValueType.T_STRING: tn = "string"; break;
                    case SValueType.T_ARRAY: tn = "array"; break;
                    case SValueType.T_MAPPING: tn = "mapping"; break;
                    case SValueType.T_OBJECT: tn = "object"; break;
                    case SValueType.T_FUNCTION: tn = "function"; break;
                    case SValueType.T_BUFFER: tn = "buffer"; break;
                    case SValueType.T_CLASS: tn = "class"; break;
                    default: tn = "int"; break;
                }
                Sp-=nargs;
                PushString(tn);
                break;
            }

            case 23: // wizardp(ob)
            {
                LpcObject ob = CurrentObject;
                if(nargs >= 1) {
                    var o = GetArg(0);
                    if(o.U.Object is LpcObject lo) ob = lo;
                    else if(o.Value.Object is LpcObject lo2) ob = lo2;
                }
                int isWiz = 0;
                if(ob != null) {
                    if(WizardObjects.Contains(ob)) isWiz = 1;
                    else {
                        try {
                            var r = CallApplyInObject(ob, "query", new SValueS[]{ new SValueS("wizard") });
                            if(r.Value.Type == SValueType.T_NUMBER && r.Value.Number != 0) isWiz = 1;
                            else if(r.U != null && r.U.Number != 0) isWiz = 1;
                        } catch {}
                    }
                }
                Sp-=nargs;
                PushNumber(isWiz);
                break;
            }
            case 28: // living(ob)
            {
                LpcObject ob = CurrentObject;
                if(nargs >= 1) {
                    var o = GetArg(0);
                    if(o.U.Object is LpcObject lo) ob = lo;
                    else if(o.Value.Object is LpcObject lo2) ob = lo2;
                }
                int isLiving = (ob != null && LivingObjects.Contains(ob)) ? 1 : 0;
                Sp-=nargs;
                PushNumber(isLiving);
                break;
            }
            case 29: // userp(ob)
            {
                LpcObject ob = CurrentObject;
                if(nargs >= 1) {
                    var o = GetArg(0);
                    if(o.U.Object is LpcObject lo) ob = lo;
                    else if(o.Value.Object is LpcObject lo2) ob = lo2;
                }
                int isUser = (ob != null && UserObjects.Contains(ob)) ? 1 : 0;
                Sp-=nargs;
                PushNumber(isUser);
                break;
            }
            case 45: // function_exists(func, ob)
            {
                string func = GetArgString(0);
                LpcObject ob = CurrentObject;
                if(nargs >= 2) {
                    var o = GetArg(1);
                    if(o.U.Object is LpcObject lo) ob = lo;
                    else if(o.Value.Object is LpcObject lo2) ob = lo2;
                }
                int exists = 0;
                if(ob != null && !string.IsNullOrEmpty(func)) {
                    try { exists = ob.TryFindFunction(func, out _, out _) ? 1 : 0; } catch {}
                }
                Sp-=nargs;
                PushNumber(exists);
                break;
            }
            case 46: // objects()
            {
                var arr = new SValueArray();
                if(ObjectManager.Current != null) {
                    foreach(var key in ObjectManager.Current.AllObjects()) {
                        var ob = ObjectManager.Current.FindObject(key);
                        if(ob != null) {
                            arr.Items.Add(SValue.NewObject(ob));
                        }
                    }
                }
                Sp-=nargs;
                PushArray(arr);
                break;
            }
            case 57: // find_player(name)
            {
                string name = GetArgString(0);
                LpcObject found = null;
                if(ObjectManager.Current != null && !string.IsNullOrEmpty(name)) {
                    foreach(var key in ObjectManager.Current.AllObjects()) {
                        var ob = ObjectManager.Current.FindObject(key);
                        if(ob == null) continue;
                        if(!UserObjects.Contains(ob)) continue;
                        if(ob.ProgramName != null && ob.ProgramName.Contains(name, StringComparison.OrdinalIgnoreCase)) { found = ob; break; }
                    }
                }
                Sp-=nargs;
                if(found != null) {
                    var sv = new SValueS(); sv.Value = SValue.NewObject(found); sv.U.Object = found; Push(sv);
                } else PushNumber(0);
                break;
            }
            case 125: // children(file)
            {
                string file = GetArgString(0);
                var arr = new SValueArray();
                if(ObjectManager.Current != null) {
                    string norm = file.Trim().Replace(".c", "");
                    if(!norm.StartsWith("/")) norm = "/" + norm;
                    foreach(var key in ObjectManager.Current.AllObjects()) {
                        if(key.StartsWith(norm + "#")) {
                            var ob = ObjectManager.Current.FindObject(key);
                            if(ob != null) arr.Items.Add(SValue.NewObject(ob));
                        }
                    }
                }
                Sp-=nargs;
                PushArray(arr);
                break;
            }
            case 96: // inherit_list(ob)
            {
                LpcObject ob = CurrentObject;
                if(nargs >= 1) {
                    var o = GetArg(0);
                    if(o.U.Object is LpcObject lo) ob = lo;
                    else if(o.Value.Object is LpcObject lo2) ob = lo2;
                }
                var arr = new SValueArray();
                if(ob != null && ob.CurrentProgram != null) {
                    var prog = ob.CurrentProgram;
                    var inhProp = prog.GetType().GetProperty("Inherits");
                    if(inhProp != null) {
                        var inh = inhProp.GetValue(prog) as System.Collections.Generic.IEnumerable<string>;
                        if(inh != null) foreach(var s in inh) arr.Items.Add(SValue.NewString(s));
                    }
                }
                Sp-=nargs;
                PushArray(arr);
                break;
            }
            case 114: // deep_inherit_list(ob) — 先回 inherit_list（淺層）
            {
                LpcObject ob = CurrentObject;
                if(nargs >= 1) {
                    var o = GetArg(0);
                    if(o.U.Object is LpcObject lo) ob = lo;
                    else if(o.Value.Object is LpcObject lo2) ob = lo2;
                }
                var arr = new SValueArray();
                if(ob != null && ob.CurrentProgram != null) {
                    var prog = ob.CurrentProgram;
                    var inhProp = prog.GetType().GetProperty("Inherits");
                    if(inhProp != null) {
                        var inh = inhProp.GetValue(prog) as System.Collections.Generic.IEnumerable<string>;
                        if(inh != null) foreach(var s in inh) arr.Items.Add(SValue.NewString(s));
                    }
                }
                Sp-=nargs;
                PushArray(arr);
                break;
            }
            case 99: // call_out_info() - ({ ({ob, func, delay_sec}), ... })
            {
                var arr99 = new SValueArray();
                try {
                    foreach(var e in CallOutManager.Current.ListAll()) {
                        var inner = new SValueArray();
                        var obSv = new SValueS(); obSv.Value = SValue.NewObject(e.Obj); obSv.U.Object = e.Obj;
                        inner.Items.Add(obSv.Value);
                        inner.Items.Add(SValue.NewString(e.Func));
                        inner.Items.Add(SValue.NewNumber(e.DelayMs / 1000));
                        arr99.Items.Add(SValue.NewArray(inner));
                    }
                } catch {}
                Sp-=nargs;
                PushArray(arr99);
                break;
            }
            case 100: // livings()
            {
                var arr = new SValueArray();
                foreach(var ob in LivingObjects) {
                    arr.Items.Add(SValue.NewObject(ob));
                }
                Sp-=nargs;
                PushArray(arr);
                break;
            }
            case 97: // set_living_name(name)
            {
                string name = GetArgString(0);
                if(CurrentObject != null && !string.IsNullOrEmpty(name)) LivingObjects.Add(CurrentObject);
                Sp-=nargs;
                PushNumber(0);
                break;
            }
            case 103: // enable_commands()
            {
                if(CurrentObject != null) LivingObjects.Add(CurrentObject);
                Sp-=nargs;
                PushNumber(0);
                break;
            }

            case 22: // printf(fmt, ...) — sprintf + write，回傳字元數
            {
                string fmt = GetArgString(0);
                var fargs = new System.Collections.Generic.List<SValueS>();
                for(int i=1;i<nargs;i++) fargs.Add(GetArg(i));
                string s = SprintfFormat(fmt, fargs);
                System.Console.Write(s);
                Sp-=nargs;
                PushNumber(s.Length);
                break;
            }
            case 5: // notify_fail(msg) — 存到目前物件，回傳 0（Neolith 語意）
            {
                string msg = GetArgString(0);
                if(CurrentObject != null) CurrentObject.FailMessage = msg;
                Sp-=nargs;
                PushNumber(0);
                break;
            }
            case 7: // add_action(func, verb) — 登記到目前物件的動作表
            {
                string func = GetArgString(0);
                string verb = GetArgString(1);
                int ok = 0;
                if(CurrentObject != null && !string.IsNullOrEmpty(func) && !string.IsNullOrEmpty(verb)) {
                    CurrentObject.Actions.RemoveAll(a => a.Item1 == verb);
                    CurrentObject.Actions.Add((verb, func));
                    ok = 1;
                }
                Sp-=nargs;
                PushNumber(ok);
                break;
            }
            case 76: // origin() — 回傳目前呼叫來源
            {
                // harness 下一律 "driver"；call_out / heart_beat 進來時由那兩邊設 CurrentOrigin
                string org = CurrentOrigin ?? "driver";
                Sp-=nargs;
                PushString(org);
                break;
            }

            case 32: // environment(ob?) — Neolith: destructed → 0
            {
                LpcObject ob = CurrentObject;
                if(nargs >= 1) { var o = GetArg(0); if(o.U.Object is LpcObject lo) ob = lo; }
                Sp-=nargs;
                if(ob == null || ob.Destructed || ob.Environment == null) PushNumber(0);
                else PushObject(ob.Environment);
                break;
            }
            case 33: // all_inventory(ob?) — Neolith: destructed → 0（數字，不是空陣列）
            {
                LpcObject ob = CurrentObject;
                if(nargs >= 1) { var o = GetArg(0); if(o.U.Object is LpcObject lo) ob = lo; }
                Sp-=nargs;
                if(ob == null || ob.Destructed) { PushNumber(0); break; }
                var arr = new SValueArray();
                foreach(var child in ob.Inventory) { if(child!=null && !child.Destructed) arr.Items.Add(SValue.NewObject(child)); }
                PushArray(arr);
                break;
            }
            case 6: // present(str|ob [, env]) — Neolith object_present 1:1
            {
                // Neolith f_present: 第 2 參數非 object → ob=0 → object_present 退回 non-specific (current_object)
                LpcObject env = null; bool specific = false;
                if(nargs >= 2) { var a1 = GetArg(1); if(a1.U.Object is LpcObject lo2) { env = lo2; specific = true; } }
                LpcObject found = null;
                if(nargs >= 1) {
                    var a0 = GetArg(0);
                    if(a0.U.Object is LpcObject target) {
                        // object 參數：Neolith object_present
                        if(!target.Destructed) {
                            if(specific) {
                                if(target.Environment == env) found = target;
                            } else {
                                // non-specific：回傳 super（容器），不是 target 本身
                                if(target.Environment == CurrentObject) found = CurrentObject;
                                else if(CurrentObject?.Environment != null && target.Environment == CurrentObject.Environment) found = CurrentObject.Environment;
                            }
                        }
                    } else {
                        string id = GetArgString(0);
                        // Neolith object_present2 解析 "name N"：尾巴是數字且前面有空格 → N（1-based 轉 0-based count）
                        string baseId = id; int count = 0;
                        if(id.Length > 0) {
                            int scan = id.Length - 1;
                            if(char.IsDigit(id[scan])) {
                                while(scan > 0 && char.IsDigit(id[scan-1])) scan--;
                                if(scan > 0 && id[scan-1] == ' ' && int.TryParse(id.Substring(scan), out int parsed)) { count = parsed - 1; baseId = id.Substring(0, scan-1); }
                            }
                        }
                        if(specific) {
                            found = Present2(baseId, count, env);
                        } else {
                            // 先搜自己 inventory
                            found = Present2(baseId, count, CurrentObject);
                            if(found == null && CurrentObject?.Environment != null) {
                                // id() in environment：符合 → 回 environment
                                var r = CallApplyInObject(CurrentObject.Environment, "id", new SValueS[]{ new SValueS(baseId) });
                                if(CurrentObject.Environment.Destructed) { /* Neolith: return 0 */ }
                                else if(IsTrue(r)) found = CurrentObject.Environment;
                                else found = Present2(baseId, count, CurrentObject.Environment);
                            }
                        }
                    }
                }
                Sp-=nargs;
                if(found != null) PushObject(found); else PushNumber(0);
                break;
            }
            case 62: // tell_room(room, msg) — 傳給 room 內所有 living 物件
            {
                LpcObject room = null;
                if(nargs >= 1) { var o = GetArg(0); if(o.U.Object is LpcObject lo) room = lo; }
                string msg = nargs >= 2 ? GetArgString(1) : "";
                int n = 0;
                if(room != null) {
                    foreach(var child in room.Inventory) {
                        if(LivingObjects.Contains(child)) {
                            CallApplyInObject(child, "receive_message", new SValueS[]{ new SValueS(msg) });
                            n++;
                        }
                    }
                    if(n == 0) System.Console.Write(msg); // harness：無 living 時直接印出，方便測試
                }
                Sp-=nargs;
                PushNumber(n);
                break;
            }
            case 70: // say(msg [, exclude]) — 傳給目前物件所在環境的所有 living，除了自己與 exclude
            {
                string msg = GetArgString(0);
                LpcObject exclude = null;
                if(nargs >= 2) { var o = GetArg(1); if(o.U.Object is LpcObject lo) exclude = lo; }
                int n = 0;
                var env = CurrentObject?.Environment;
                if(env != null) {
                    foreach(var child in env.Inventory) {
                        if(child == CurrentObject || child == exclude) continue;
                        if(LivingObjects.Contains(child)) {
                            CallApplyInObject(child, "receive_message", new SValueS[]{ new SValueS(msg) });
                            n++;
                        }
                    }
                    if(n == 0) System.Console.Write(msg);
                }
                Sp-=nargs;
                PushNumber(n);
                break;
            }
            case 21: // command(str) — 在目前物件與其環境的動作表找 verb，呼叫對應函數
            {
                string cmd = GetArgString(0).Trim();
                int ok = 0;
                if(!string.IsNullOrEmpty(cmd)) {
                    string verb = cmd; string rest = "";
                    int sp3 = cmd.IndexOf(' ');
                    if(sp3 > 0) { verb = cmd.Substring(0, sp3); rest = cmd.Substring(sp3+1); }
                    // 1) 自己的動作表  2) 環境內其他物件的動作表（Neolith 命令搜尋順序）
                    CurrentVerb = verb;
                    System.Collections.Generic.List<LpcObject> search = new();
                    if(CurrentObject != null) search.Add(CurrentObject);
                    if(CurrentObject?.Environment != null) search.AddRange(CurrentObject.Environment.Inventory);
                    foreach(var ob in search) {
                        foreach(var act in ob.Actions) {
                            if(act.Item1 == verb) {
                                var r = CallApplyInObject(ob, act.Item2, new SValueS[]{ new SValueS(rest) });
                                if(IsTrue(r)) ok = 1;
                                else if(CurrentObject != null && !string.IsNullOrEmpty(CurrentObject.FailMessage)) {
                                    System.Console.Write(CurrentObject.FailMessage);
                                    CurrentObject.FailMessage = "";
                                }
                                goto done_cmd;
                            }
                        }
                    }
                    if(ok == 0 && CurrentObject != null && !string.IsNullOrEmpty(CurrentObject.FailMessage)) {
                        System.Console.Write(CurrentObject.FailMessage);
                        CurrentObject.FailMessage = "";
                    }
                    done_cmd:;
                }
                Sp-=nargs;
                PushNumber(ok);
                break;
            }
            default:
            {
                Sp-=nargs;
                PushNumber(0);
                break;
            }
        }
    }

    public long RunFromMain()
    {
        Sp=-1;
        for(int i=0;i<STACK_SIZE;i++) Stack[i]=SValueS.Invalid;
        for(int i=0;i<256;i++) Locals[i]=SValueS.Invalid;
        CallStack.Clear();
        CurrentObject = new LpcObject("<main>", CurrentProgram);
        // Run __global_init first so global variables are initialized (same as ObjectManager)
        if (CurrentProgram?.FunctionTable != null)
        {
            foreach (var f in CurrentProgram.FunctionTable)
            {
                if (f.Name == "__global_init")
                {
                    CallApply("__global_init", System.Array.Empty<SValueS>(), CurrentObject);
                    // reset stack for main function
                    Sp=-1;
                    for(int i=0;i<STACK_SIZE;i++) Stack[i]=SValueS.Invalid;
                    for(int i=0;i<256;i++) Locals[i]=SValueS.Invalid;
                    CallStack.Clear();
                    break;
                }
            }
        }
        int mainAddr=-1;
        // Entry point is create() by name, NOT FuncAddr[0]: FunctionTable is in
        // definition order, so a helper defined before create() is FuncAddr[0].
        if (CurrentProgram?.FunctionTable != null)
        {
            foreach (var f in CurrentProgram.FunctionTable)
            {
                if (f.Name == "create") { mainAddr = f.Address; break; }
            }
        }
        if(mainAddr<0 && FuncAddr.ContainsKey(0)) mainAddr=FuncAddr[0];
        else if(mainAddr<0 && FuncAddr.Count>=1)
        {
            int minKey=int.MaxValue;
            foreach(var kv in FuncAddr) if(kv.Key<minKey){ minKey=kv.Key; mainAddr=kv.Value; }
        }
        Pc = mainAddr>=0? mainAddr : 0;
        return RunLoop();
    }

    public long RunFromAddress(int addr, LpcObject obj=null)
    {
        Sp=-1;
        for(int i=0;i<STACK_SIZE;i++) Stack[i]=SValueS.Invalid;
        for(int i=0;i<256;i++) Locals[i]=SValueS.Invalid;
        CallStack.Clear();
        CurrentObject=obj ?? new LpcObject("<call>", CurrentProgram);
        Pc=addr;
        return RunLoop();
    }

    public SValueS CallApply(string funcName, SValueS[] args, LpcObject obj=null)
    {
        if(obj!=null && obj.CurrentProgram!=null) SetProgram(obj.CurrentProgram);
        if(CurrentProgram==null) return SValueS.Invalid;
        if(obj!=null) CurrentObject=obj;
        int target=-1;
        if(CurrentProgram.FunctionTable!=null)
        {
            for(int i=0;i<CurrentProgram.FunctionTable.Length;i++)
                if(CurrentProgram.FunctionTable[i].Name==funcName){ target=CurrentProgram.FunctionTable[i].Address; break; }
        }
        if(target<0) return SValueS.Invalid;
        var savedSp3=Sp; var savedStack3=new SValueS[STACK_SIZE]; Array.Copy(Stack, savedStack3, STACK_SIZE);
        Sp=-1;
        for(int i=0;i<256;i++) Locals[i]=SValueS.Invalid;
        CallStack.Clear();
        // push args as locals? For simplicity, push onto stack and set locals 0..n-1
        for(int i=0;i<args.Length && i<256;i++) Locals[i]=args[i];
        Pc=target;
        long ret=RunLoop();
        SValueS res3 = Sp>=0? Stack[Sp] : SValueS.FromNumber(ret);
        Array.Copy(savedStack3, Stack, STACK_SIZE); Sp=savedSp3;
        return res3;
    }

    long RunLoop()
    {
        while(Pc < Prog.Length)
        {
            byte instr = Prog[Pc++];
            switch(instr)
            {
                case F_CONST0: PushNumber(0); break;
                case F_CONST1: PushNumber(1); break;
                case F_NUMBER:
                {
                    int v=LoadInt(Prog, ref Pc);
                    PushNumber(v);
                    break;
                }
                case F_LONG:
                {
                    long v=LoadLong(Prog, ref Pc);
                    PushNumber(v);
                    break;
                }
                case F_REAL:
                {
                    // TODO: T_REAL not in SValueType yet; truncate to Number so float literals don't crash.
                    double v=LoadDouble(Prog, ref Pc);
                    PushNumber((long)v);
                    break;
                }
                case F_BYTE:
                {
                    byte b=Prog[Pc++];
                    PushNumber(b);
                    break;
                }
                case F_NBYTE:
                {
                    sbyte b=(sbyte)Prog[Pc++];
                    PushNumber(b);
                    break;
                }
                case F_STRING:
                {
                    short idx=LoadShort(Prog, ref Pc);
                    string s = (idx>=0 && idx<Strings.Length)? Strings[idx] : "";
                    PushString(s);
                    break;
                }
                case F_LOCAL:
                {
                    byte idx=Prog[Pc++];
                    var lv=Locals[idx];
                    Push(lv);
                    break;
                }
                case F_LOCAL_LVALUE:
                {
                    byte idx=Prog[Pc++];
                    var lvd=new LValueDesc{ Kind=LValueKind.Local, LocalIdx=idx };
                    var sv=new SValueS();
                    sv.Value=SValue.NewLValue(lvd);
                    sv.U.LValue=lvd;
                    Push(sv);
                    break;
                }
                case F_GLOBAL:
                {
                    short idx=LoadShort(Prog, ref Pc);
                    Push(GetGlobal(idx));
                    break;
                }
                case F_GLOBAL_LVALUE:
                {
                    short idx=LoadShort(Prog, ref Pc);
                    var lvd=new LValueDesc{ Kind=LValueKind.Global, GlobalIdx=idx };
                    var sv=new SValueS();
                    sv.Value=SValue.NewLValue(lvd);
                    sv.U.LValue=lvd;
                    Push(sv);
                    break;
                }
                case F_RINDEX:
                {
                    var idxSv=Pop();
                    var baseSv=Pop();
                    SValue result=SValue.Invalid;
                    var arrR=baseSv.U.Array ?? baseSv.Value.Array;
                    var strR=baseSv.U.String ?? baseSv.Value.String;
                    long nR=(idxSv.Value.Type==SValueType.T_NUMBER? idxSv.Value.Number : idxSv.U.Number);
                    if(arrR!=null){ int ii=(int)(arrR.Items.Count-1-nR); if(ii>=0 && ii<arrR.Items.Count) result=arrR.Items[ii]; }
                    else if(strR!=null){ int ii=(int)(strR.Length-1-nR); if(ii>=0 && ii<strR.Length) result=SValue.NewString(strR[ii].ToString()); }
                    var outR=new SValueS(); outR.Value=result; outR.U.Number=result.Number;
                    if(result.Array!=null) outR.U.Array=result.Array;
                    if(result.Mapping!=null) outR.U.Mapping=result.Mapping;
                    if(result.String!=null) outR.U.String=result.String;
                    Push(outR);
                    break;
                }
                case F_RINDEX_LVALUE:
                {
                    var idxSv=Pop();
                    var baseLvalSv=Pop();
                    var lvd=baseLvalSv.U.LValue;
                    if(lvd==null){ var newLvd0=new LValueDesc{ Kind=LValueKind.RIndex, Index=idxSv, Parent=null }; var sv0=new SValueS(); sv0.Value=SValue.NewLValue(newLvd0); sv0.U.LValue=newLvd0; Push(sv0); break; }
                    var newDescR=new LValueDesc{ Kind=LValueKind.RIndex, Index=idxSv, Parent=lvd };
                    var outR2=new SValueS(); outR2.Value=SValue.NewLValue(newDescR); outR2.U.LValue=newDescR; Push(outR2);
                    break;
                }
                case F_NN_RANGE:
                case F_RN_RANGE:
                case F_RR_RANGE:
                case F_NR_RANGE:
                case F_NE_RANGE:
                case F_RE_RANGE:
                {
                    var endSv=Pop();
                    var startSv=Pop();
                    var baseSv=Pop();
                    bool sRev=(instr==F_RN_RANGE || instr==F_RR_RANGE || instr==F_RE_RANGE);
                    bool eRev=(instr==F_RR_RANGE || instr==F_NR_RANGE || instr==F_NE_RANGE || instr==F_RE_RANGE);
                    bool excl=(instr==F_NE_RANGE || instr==F_RE_RANGE);
                    long rawS=(startSv.Value.Type==SValueType.T_NUMBER? startSv.Value.Number : startSv.U.Number);
                    long rawE=(endSv.Value.Type==SValueType.T_NUMBER? endSv.Value.Number : endSv.U.Number);
                    var arrQ=baseSv.U.Array ?? baseSv.Value.Array;
                    var strQ=baseSv.U.String ?? baseSv.Value.String;
                    int lenQ=arrQ!=null? arrQ.Items.Count : (strQ!=null? strQ.Length : 0);
                    if(lenQ==0){ if(arrQ!=null) PushArray(new SValueArray()); else PushString(""); break; }
                    int sIdx; int eIdx;
                    if(rawS==255) sIdx=0; else if(sRev) sIdx=lenQ-1-(int)rawS; else sIdx=(int)rawS;
                    if(rawE==255) eIdx=lenQ-1; else if(eRev) eIdx=lenQ-1-(int)rawE; else eIdx=(int)rawE;
                    if(excl) eIdx--;
                    if(sIdx<0) sIdx=0; if(eIdx>=lenQ) eIdx=lenQ-1;
                    if(sIdx>eIdx || sIdx>=lenQ || eIdx<0){ if(arrQ!=null) PushArray(new SValueArray()); else PushString(""); break; }
                    if(arrQ!=null){ var narr=new SValueArray(); for(int ii=sIdx; ii<=eIdx; ii++) narr.Items.Add(arrQ.Items[ii]); PushArray(narr); }
                    else if(strQ!=null){ PushString(strQ.Substring(sIdx, eIdx-sIdx+1)); }
                    else PushNumber(0);
                    break;
                }
                case F_NN_RANGE_LVALUE:
                case F_RN_RANGE_LVALUE:
                case F_RR_RANGE_LVALUE:
                case F_NR_RANGE_LVALUE:
                {
                    var endSv=Pop();
                    var startSv=Pop();
                    var baseLvalSv=Pop();
                    var lvd=baseLvalSv.U.LValue;
                    bool sRevL=(instr==F_RN_RANGE_LVALUE || instr==F_RR_RANGE_LVALUE);
                    bool eRevL=(instr==F_RR_RANGE_LVALUE || instr==F_NR_RANGE_LVALUE);
                    if(lvd==null){ var newLvd0=new LValueDesc{ Kind=LValueKind.Range, Start=startSv, End=endSv, SRev=sRevL, ERev=eRevL, Parent=null }; var sv0=new SValueS(); sv0.Value=SValue.NewLValue(newLvd0); sv0.U.LValue=newLvd0; Push(sv0); break; }
                    var newDescL=new LValueDesc{ Kind=LValueKind.Range, Start=startSv, End=endSv, SRev=sRevL, ERev=eRevL, Parent=lvd };
                    var outL2=new SValueS(); outL2.Value=SValue.NewLValue(newDescL); outL2.U.LValue=newDescL; Push(outL2);
                    break;
                }
                case F_INDEX:
                {
                    var idxSv=Pop();
                    var baseSv=Pop();
                    SValue result=SValue.Invalid;
                    var arr=baseSv.U.Array ?? baseSv.Value.Array;
                    var map=baseSv.U.Mapping ?? baseSv.Value.Mapping;
                    var str=baseSv.U.String ?? baseSv.Value.String;
                    if(arr!=null)
                    {
                        int i = (int)(idxSv.Value.Type==SValueType.T_NUMBER? idxSv.Value.Number : idxSv.U.Number);
                        if(i>=0 && i<arr.Items.Count) result=arr.Items[i];
                    }
                    else if(map!=null)
                    {
                        SValue key=idxSv.Value;
                        if(key.Type==SValueType.T_INVALID)
                        {
                            if(idxSv.U.String!=null) key=SValue.NewString(idxSv.U.String);
                            else key=SValue.NewNumber(idxSv.U.Number);
                        }
                        if(map.Map.TryGetValue(key, out SValue v)) result=v;
                    }
                    else if(str!=null)
                    {
                        int i=(int)(idxSv.Value.Type==SValueType.T_NUMBER? idxSv.Value.Number : idxSv.U.Number);
                        if(i>=0 && i<str.Length) result=SValue.NewString(str[i].ToString());
                    }
                    var outSv=new SValueS(); outSv.Value=result;
                    outSv.U.Number=result.Number;
                    if(result.Array!=null) outSv.U.Array=result.Array;
                    if(result.Mapping!=null) outSv.U.Mapping=result.Mapping;
                    if(result.String!=null) outSv.U.String=result.String;
                    else outSv.U.String=result.Type==SValueType.T_STRING? result.String : null;
                    Push(outSv);
                    break;
                }
                case F_INDEX_LVALUE:
                {
                    var idxSv=Pop();
                    var baseLvalSv=Pop();
                    var lvd=baseLvalSv.U.LValue;
                    if(lvd==null)
                    {
                        // base is not lvalue? treat as value then create lvalue chain for temp?
                        var newLvd=new LValueDesc{ Kind=LValueKind.Index, Index=idxSv, Parent=null };
                        // try find local from base? not trivial, push invalid
                        var sv=new SValueS(); sv.Value=SValue.NewLValue(newLvd); sv.U.LValue=newLvd;
                        Push(sv);
                        break;
                    }
                    var newDesc=new LValueDesc{ Kind=LValueKind.Index, Index=idxSv, Parent=lvd };
                    var outSv2=new SValueS(); outSv2.Value=SValue.NewLValue(newDesc); outSv2.U.LValue=newDesc;
                    Push(outSv2);
                    break;
                }
                case F_ADD:
                {
                    var b=Pop(); var a=Pop();
                    var aArr = a.U.Array ?? a.Value.Array;
                    var bArr = b.U.Array ?? b.Value.Array;
                    bool aIsStr = (a.Value.Type==SValueType.T_STRING) || a.U.String!=null || a.Value.String!=null;
                    bool bIsStr = (b.Value.Type==SValueType.T_STRING) || b.U.String!=null || b.Value.String!=null;
                    if(aIsStr || bIsStr)
                    {
                        string sa = a.U.String ?? a.Value.String ?? (a.Value.Type==SValueType.T_NUMBER? a.Value.Number.ToString() : a.U.Number.ToString());
                        string sb = b.U.String ?? b.Value.String ?? (b.Value.Type==SValueType.T_NUMBER? b.Value.Number.ToString() : b.U.Number.ToString());
                        PushString(sa+sb);
                    }
                    else if(aArr!=null && bArr!=null)
                    {
                        var narr=new SValueArray();
                        foreach(var it in aArr.Items) narr.Items.Add(it);
                        foreach(var it in bArr.Items) narr.Items.Add(it);
                        PushArray(narr);
                    }
                    else if(aArr!=null && b.Value.Type==SValueType.T_NUMBER && b.Value.Number==0 && b.U.Array==null)
                    {
                        // adding empty? but handle
                        PushArray(aArr);
                    }
                    else if(a.Value.Type==SValueType.T_NUMBER && b.Value.Type==SValueType.T_NUMBER)
                        PushNumber(a.Value.Number + b.Value.Number);
                    else
                        PushNumber((a.Value.Type==SValueType.T_NUMBER? a.Value.Number : a.U.Number) + (b.Value.Type==SValueType.T_NUMBER? b.Value.Number : b.U.Number));
                    break;
                }
                case F_SUBTRACT:
                {
                    var b=Pop(); var a=Pop();
                    long av=a.Value.Type==SValueType.T_NUMBER? a.Value.Number : a.U.Number;
                    long bv=b.Value.Type==SValueType.T_NUMBER? b.Value.Number : b.U.Number;
                    PushNumber(av-bv);
                    break;
                }
                case F_MULTIPLY:
                {
                    var b=Pop(); var a=Pop();
                    long av=a.Value.Type==SValueType.T_NUMBER? a.Value.Number : a.U.Number;
                    long bv=b.Value.Type==SValueType.T_NUMBER? b.Value.Number : b.U.Number;
                    PushNumber(av*bv);
                    break;
                }
                case F_DIVIDE:
                {
                    var b=Pop(); var a=Pop();
                    long av=a.Value.Type==SValueType.T_NUMBER? a.Value.Number : a.U.Number;
                    long bv=b.Value.Type==SValueType.T_NUMBER? b.Value.Number : b.U.Number;
                    if(bv==0) PushNumber(0); else PushNumber(av/bv);
                    break;
                }
                case F_MOD:
                {
                    var b=Pop(); var a=Pop();
                    long av=a.Value.Type==SValueType.T_NUMBER? a.Value.Number : a.U.Number;
                    long bv=b.Value.Type==SValueType.T_NUMBER? b.Value.Number : b.U.Number;
                    if(bv==0) PushNumber(0); else PushNumber(av%bv);
                    break;
                }
                case F_AND: { var b=Pop(); var a=Pop(); long av=a.Value.Type==SValueType.T_NUMBER? a.Value.Number : a.U.Number; long bv=b.Value.Type==SValueType.T_NUMBER? b.Value.Number : b.U.Number; PushNumber(av & bv); break; }
                case F_OR: { var b=Pop(); var a=Pop(); long av=a.Value.Type==SValueType.T_NUMBER? a.Value.Number : a.U.Number; long bv=b.Value.Type==SValueType.T_NUMBER? b.Value.Number : b.U.Number; PushNumber(av | bv); break; }
                case F_XOR: { var b=Pop(); var a=Pop(); long av=a.Value.Type==SValueType.T_NUMBER? a.Value.Number : a.U.Number; long bv=b.Value.Type==SValueType.T_NUMBER? b.Value.Number : b.U.Number; PushNumber(av ^ bv); break; }
                case F_LSH: { var b=Pop(); var a=Pop(); long av=a.Value.Type==SValueType.T_NUMBER? a.Value.Number : a.U.Number; long bv=b.Value.Type==SValueType.T_NUMBER? b.Value.Number : b.U.Number; PushNumber(av << (int)bv); break; }
                case F_RSH: { var b=Pop(); var a=Pop(); long av=a.Value.Type==SValueType.T_NUMBER? a.Value.Number : a.U.Number; long bv=b.Value.Type==SValueType.T_NUMBER? b.Value.Number : b.U.Number; PushNumber(av >> (int)bv); break; }
                case F_EQ: { var b=Pop(); var a=Pop(); PushNumber(SValuesEqual(a,b)?1:0); break; }
                case F_NE: { var b=Pop(); var a=Pop(); PushNumber(SValuesEqual(a,b)?0:1); break; }
                case F_LT: { var b=Pop(); var a=Pop(); PushNumber(TrySValueCompare(a,b,out var c) && c<0?1:0); break; }
                case F_GT: { var b=Pop(); var a=Pop(); PushNumber(TrySValueCompare(a,b,out var c) && c>0?1:0); break; }
                case F_LE: { var b=Pop(); var a=Pop(); PushNumber(TrySValueCompare(a,b,out var c) && c<=0?1:0); break; }
                case F_GE: { var b=Pop(); var a=Pop(); PushNumber(TrySValueCompare(a,b,out var c) && c>=0?1:0); break; }
                case F_NOT: { var a=Pop(); PushNumber(IsTrue(a)?0:1); break; }
                case F_NEGATE: { var a=Pop(); long av=a.Value.Type==SValueType.T_NUMBER? a.Value.Number : a.U.Number; PushNumber(-av); break; }
                case F_POP_VALUE: { Pop(); break; }
                case F_DUP: { Push(Peek()); break; }
                case F_PUSH_MAPPING:
                {
                    var map=new SValueMapping();
                    PushMapping(map);
                    break;
                }
                case F_PUSH_ARRAY:
                {
                    var szSv=Pop();
                    int sz=(int)(szSv.Value.Type==SValueType.T_NUMBER? szSv.Value.Number : szSv.U.Number);
                    if(sz<0) sz=0;
                    var arr=new SValueArray();
                    for(int i=0;i<sz;i++) arr.Items.Add(SValue.NewNumber(0));
                    PushArray(arr);
                    break;
                }
                case F_AGGREGATE:
                {
                    short count=LoadShort(Prog, ref Pc);
                    var arr=new SValueArray();
                    var tmp=new List<SValue>();
                    for(int i=0;i<count;i++){ var v=Pop();  tmp.Add(v.Value); }
                    tmp.Reverse();
                    foreach(var v in tmp) {  arr.Items.Add(v); }
                    PushArray(arr);
                    
                    break;
                }
                case F_AGGREGATE_ASSOC:
                {
                    short count=LoadShort(Prog, ref Pc);
                    var map=new SValueMapping();
                    var tmp=new List<(SValue k,SValue v)>();
                    for(int i=0;i<count;i++){ var v=Pop(); var k=Pop(); tmp.Add((k.Value, v.Value)); }
                    tmp.Reverse();
                    foreach(var kv in tmp) map.Map[kv.k]=kv.v;
                    PushMapping(map);
                    break;
                }
                case F_MAP_ADD:
                {
                    var valSv=Pop();
                    var keySv=Pop();
                    var mapSv=Pop();
                    var map=mapSv.U.Mapping ?? mapSv.Value.Mapping;
                    if(map!=null)
                    {
                        SValue key=keySv.Value;
                        if(key.Type==SValueType.T_INVALID)
                        {
                            if(keySv.U.String!=null) key=SValue.NewString(keySv.U.String);
                            else key=SValue.NewNumber(keySv.U.Number);
                        }
                        map.Map[key]=valSv.Value;
                        PushMapping(map);
                    }
                    else
                    {
                        // if not mapping, push 0
                        PushNumber(0);
                    }
                    break;
                }
                case F_PUSH_CLOSURE_CAPTURED:
                {
                    short fIdx=LoadShort(Prog, ref Pc);
                    byte capN=Prog[Pc++];
                    var caps=new SValueS[capN];
                    for(int i=(int)capN-1;i>=0;i--) caps[i]=Pop();
                    PushClosureCaptured(fIdx, caps);
                    break;
                }
                case F_CAPTURED:
                {
                    byte cIdx=Prog[Pc++];
                    if(cIdx < CurrentCaptured.Length) Push(CurrentCaptured[cIdx]);
                    else PushNumber(0);
                    break;
                }
                case F_FUNCTION_CONSTRUCTOR:
                {
                    short fIdx=LoadShort(Prog, ref Pc);
                    PushClosure(fIdx);
                    break;
                }
                case F_EVALUATE:
                {
                    byte nargs=Prog[Pc++];

                    // dump stack
                    for(int si=0; si<=Sp && si<10; si++){
                        var sv=Stack[si];
                    }
                    var all=new List<SValueS>();
                    for(int i=0;i<nargs;i++) all.Add(Stack[Sp - nargs +1 + i]);
                    Sp-=nargs;
                    if(all.Count==0){ PushNumber(0); break; }
                    var closureSv=all[0];
                    int funcIdx = closureSv.Value.Type==SValueType.T_FUNCTION? closureSv.Value.FunctionIndex : (int)closureSv.U.Number;
                    if(closureSv.Value.FunctionIndex!=0) funcIdx=closureSv.Value.FunctionIndex;
                    else if(closureSv.U.Number!=0) funcIdx=(int)closureSv.U.Number;
                    else funcIdx=(int)closureSv.Value.Number;
                    // 找出 ClosureInstance（U 或 Value）
                    ClosureInstance ciE = null;
                    if(closureSv.U.Object is ClosureInstance c1) ciE = c1;
                    else if(closureSv.Value.Object is ClosureInstance c2) ciE = c2;
                    // 跨物件 closure：切到 OwnerProgram，否則 FuncIdx 在目前 Prog 會對到錯誤函數
                    Program ownerProg = ciE?.OwnerProgram;
                    var frame=new CallFrame(Pc, Sp+1, CurrentObject);
                    for(int i=0;i<256;i++) frame.Locals[i]=Locals[i];
                    frame.CallerCaptured=CurrentCaptured;
                    frame.SavedProg=Prog; frame.SavedStrings=Strings; frame.SavedFuncAddr=new Dictionary<int,int>(FuncAddr); frame.SavedProgram=CurrentProgram;
                    if(ciE!=null) CurrentCaptured=ciE.Captured ?? System.Array.Empty<SValueS>();
                    else CurrentCaptured=System.Array.Empty<SValueS>();
                    int target;
                    if(ownerProg!=null && ownerProg!=CurrentProgram) {
                        var dict=new Dictionary<int,int>();
                        for(int i=0;i<ownerProg.FunctionTable.Length;i++) dict[i]=ownerProg.FunctionTable[i].Address;
                        target = dict.ContainsKey(funcIdx)? dict[funcIdx] : -1;
                        if(target<0){ PushNumber(0); break; }
                        Prog=ownerProg.Code ?? System.Array.Empty<byte>(); Strings=ownerProg.Strings ?? System.Array.Empty<string>(); FuncAddr=dict; CurrentProgram=ownerProg;
                        if(ciE?.Owner!=null) CurrentObject=ciE.Owner;
                    } else {
                        target = FuncAddr.ContainsKey(funcIdx)? FuncAddr[funcIdx] : -1;
                        if(target<0){ PushNumber(0); break; }
                    }
                    CallStack.Push(frame);
                    var callArgs=new SValueS[all.Count-1];
                    for(int i=1;i<all.Count;i++) callArgs[i-1]=all[i];
                    for(int i=0;i<256;i++) Locals[i]=SValueS.Invalid;
                    for(int i=0;i<callArgs.Length && i<256;i++) Locals[i]=callArgs[i];

                    Pc=target;
                    break;
                }
                case F_BRANCH:
                {
                    short off=LoadShort(Prog, ref Pc);
                    Pc+=off;
                    break;
                }
                case F_BBRANCH:
                {
                    short off=LoadShort(Prog, ref Pc);
                    Pc+=off; // off is negative for backward
                    break;
                }
                case F_BRANCH_WHEN_ZERO:
                {
                    short off=LoadShort(Prog, ref Pc);
                    var v=Pop();
                    if(!IsTrue(v)) Pc+=off;
                    break;
                }
                case F_BRANCH_WHEN_NON_ZERO:
                {
                    short off=LoadShort(Prog, ref Pc);
                    var v=Pop();
                    if(IsTrue(v)) Pc+=off;
                    break;
                }
                case F_BBRANCH_WHEN_ZERO:
                {
                    short off=LoadShort(Prog, ref Pc);
                    var v=Pop();
                    if(!IsTrue(v)) Pc+=off;
                    break;
                }
                case F_BBRANCH_WHEN_NON_ZERO:
                {
                    short off=LoadShort(Prog, ref Pc);
                    var v=Pop();
                    if(IsTrue(v)) Pc+=off;
                    break;
                }
                case F_LOR:
                case F_LAND:
                {
                    // FluffOS semantics: return operand, not 1/0, so `x || default` / `x && y` work as default-value idiom
                    // e.g. "PROMPT" || "DEFAULT" -> "PROMPT", 0 || 22 -> 22, 0 && 1 -> 0, 1 && "hi" -> "hi"
                    var b=Pop(); var a=Pop();
                    bool at=IsTrue(a);
                    if(instr==F_LOR) Push(at ? a : b); else Push(at ? b : a);
                    break;
                }
                case F_ASSIGN:
                case F_VOID_ASSIGN:
                case F_ASSIGN_LVALUE:
                {
                    var valSv=Pop();
                    var lvalSv=Pop();
                    if(lvalSv.U.LValue!=null) AssignLValueChain(lvalSv.U.LValue, valSv);
                    if(instr!=F_VOID_ASSIGN) Push(valSv);
                    break;
                }
                case F_SIMUL_EFUN:
                {
                    short strIdx=LoadShort(Prog, ref Pc);
                    byte nargs=Prog[Pc++];
                    string fname=(strIdx>=0 && strIdx<Strings.Length)? Strings[strIdx] : "";
                    var sargs=new SValueS[nargs];
                    for(int i=nargs-1;i>=0;i--) sargs[i]=Pop();
                    var seObj=ObjectManager.Current?.SimulEfunObject;
                    if(seObj==null && ObjectManager.Current!=null){ try{ seObj=ObjectManager.Current.LoadObject("/adm/obj/simul_efun"); }catch{} }
                    if(seObj==null || seObj.CurrentProgram==null || !seObj.TryFindFunction(fname, out _, out _)){ PushNumber(0); break; }
                    var res=CallApplySimulEfun(seObj, fname, sargs);
                    Push(res);
                    break;
                }
                case F_CALL_FUNCTION_BY_ADDRESS:
                {
                    short fIdx=LoadShort(Prog, ref Pc);
                    byte nargs=Prog[Pc++];
                    int target=FuncAddr.ContainsKey(fIdx)? FuncAddr[fIdx] : -1;
                    var frame=new CallFrame(Pc, Sp - nargs +1, CurrentObject);
                    for(int i=0;i<256;i++) frame.Locals[i]=Locals[i];
                    frame.CallerCaptured=CurrentCaptured; frame.SavedProg=Prog; frame.SavedStrings=Strings; frame.SavedFuncAddr=new Dictionary<int,int>(FuncAddr); frame.SavedProgram=CurrentProgram; CurrentCaptured=System.Array.Empty<SValueS>();
                    CallStack.Push(frame);
                    // move args into new locals 0..nargs-1
                    SValueS[] args=new SValueS[nargs];
                    for(int i=nargs-1;i>=0;i--) args[i]=Pop();
                    for(int i=0;i<256;i++) Locals[i]=SValueS.Invalid;
                    for(int i=0;i<nargs && i<256;i++) Locals[i]=args[i];
                    Pc=target;
                    break;
                }
                case F_RETURN:
                case F_RETURN_ZERO:
                {
                    SValueS retVal;
                    if(instr==F_RETURN_ZERO) retVal=SValueS.FromNumber(0);
                    else
                    {
                        if(Sp>=0) retVal=Pop(); else retVal=SValueS.FromNumber(0);
                    }
                    if(CallStack.Count==0)
                    {
                        long num=0;
                        if(retVal.Value.Type==SValueType.T_NUMBER) num=retVal.Value.Number;
                        else if(retVal.U.Number!=0) num=retVal.U.Number;
                        else if(retVal.U.Mapping!=null) num=retVal.U.Mapping.Map.Count;
                        else if(retVal.Value.Mapping!=null) num=retVal.Value.Mapping.Map.Count;
                        else if(retVal.U.Array!=null) num=retVal.U.Array.Items.Count;
                        else if(retVal.Value.Array!=null) num=retVal.Value.Array.Items.Count;
                        else if(retVal.U.String!=null) num=retVal.U.String.Length;
                        else if(retVal.Value.String!=null) num=retVal.Value.String.Length;
                        Push(retVal);
                        return num;
                    }
                    else
                    {
                        var frame=CallStack.Pop();
                        for(int i=0;i<256;i++) Locals[i]=frame.Locals[i];
                        CurrentObject=frame.CurrentObject;
                        CurrentCaptured=frame.CallerCaptured ?? System.Array.Empty<SValueS>();
                        if(frame.SavedProg!=null){ Prog=frame.SavedProg; Strings=frame.SavedStrings; FuncAddr=frame.SavedFuncAddr; CurrentProgram=frame.SavedProgram; }
                        Sp=frame.SpBase-1;
                        Push(retVal);
                        Pc=frame.ReturnPc;
                        break;
                    }
                }
                case F_SSCANF:
                {
                    byte nargs=Prog[Pc++];
                    // stack: input, fmt, lvalue1, lvalue2...
                    if(nargs<2){ PushNumber(0); break; }
                    var args=new List<SValueS>();
                    for(int i=0;i<nargs;i++) args.Add(Stack[Sp - nargs +1 + i]);
                    Sp-=nargs;
                    string input=args[0].U.String ?? args[0].Value.String ?? "";
                    string fmt=args[1].U.String ?? args[1].Value.String ?? "";
                    var lvals=new List<SValueS>();
                    for(int i=2;i<args.Count;i++) lvals.Add(args[i]);
                    int matched=InterSscanf(input, fmt, lvals);
                    PushNumber(matched);
                    break;
                }
                case F_EFUN0:
                case F_EFUN1:
                case F_EFUN2:
                case F_EFUN3:
                case F_EFUNV:
                {
                    byte efunIdx=Prog[Pc++];
                    byte nargs=0;
                    if(instr==F_EFUN0) nargs=0;
                    else if(instr==F_EFUN1) nargs=1;
                    else if(instr==F_EFUN2) nargs=2;
                    else if(instr==F_EFUN3) nargs=3;
                    else if(instr==F_EFUNV){ nargs=Prog[Pc++]; }
                    else nargs=Prog[Pc++];
                    HandleEfun(efunIdx, nargs);
                    break;
                }
                default:
                {
                    return Sp>=0? Stack[Sp].U.Number : 0;
                }
            }
        }
        return Sp>=0? Stack[Sp].U.Number : 0;
    }

    SValueS CallApplySimulEfun(LpcObject seObj, string funcName, SValueS[] args)
    {
        if(seObj.CurrentProgram==null) return SValueS.FromNumber(0);
        int target=-1;
        for(int i=0;i<seObj.CurrentProgram.FunctionTable.Length;i++)
            if(seObj.CurrentProgram.FunctionTable[i].Name==funcName){ target=seObj.CurrentProgram.FunctionTable[i].Address; break; }
        if(target<0) return SValueS.FromNumber(0);
        var savedProg=Prog;
        var savedStrings=Strings;
        var savedFuncAddr=new Dictionary<int,int>(FuncAddr);
        var savedProgObj=CurrentProgram;
        var savedPc=Pc;
        var savedSp=Sp;
        var savedLocals=new SValueS[256];
        Array.Copy(Locals, savedLocals, 64);
        var savedCaptured=CurrentCaptured;
        var savedCallStack=new Stack<CallFrame>(CallStack);
        var savedStack=new SValueS[STACK_SIZE]; Array.Copy(Stack, savedStack, STACK_SIZE);
        CurrentProgram=seObj.CurrentProgram;
        Prog=seObj.CurrentProgram.Code;
        Strings=seObj.CurrentProgram.Strings;
        FuncAddr.Clear();
        for(int i=0;i<seObj.CurrentProgram.FunctionTable.Length;i++) FuncAddr[i]=seObj.CurrentProgram.FunctionTable[i].Address;
        CurrentCaptured=System.Array.Empty<SValueS>();
        Sp=-1;
        for(int i=0;i<256;i++) Locals[i]=SValueS.Invalid;
        for(int i=0;i<args.Length && i<256;i++) Locals[i]=args[i];
        Pc=target;
        CallStack.Clear();
        long ret=RunLoop();
        SValueS result=Sp>=0? Stack[Sp] : SValueS.FromNumber(ret);
        Array.Copy(savedStack, Stack, STACK_SIZE);
        Prog=savedProg;
        Strings=savedStrings;
        FuncAddr=savedFuncAddr;
        CurrentProgram=savedProgObj;
        Pc=savedPc;
        Sp=savedSp;
        Array.Copy(savedLocals, Locals, 64);
        CurrentCaptured=savedCaptured;
        CallStack=new Stack<CallFrame>(savedCallStack);
        return result;
    }

    SValueS CallApplyInObject(LpcObject obj, string funcName, SValueS[] args)
    {
        // shadow chain: 最外層優先；caller 就是 shadower 本人時 bypass（讓 shadow 能呼叫原物件）
        if(obj!=null && obj.ShadowedBy!=null) {
            LpcObject caller = CurrentObject;
            var chain = new System.Collections.Generic.List<LpcObject>(); chain.Add(obj);
            var cur = obj;
            while(cur.ShadowedBy!=null && cur.ShadowedBy!=caller && !cur.ShadowedBy.Destructed && !chain.Contains(cur.ShadowedBy)) { chain.Add(cur.ShadowedBy); cur = cur.ShadowedBy; }
            for(int ci=chain.Count-1; ci>=0; ci--) {
                var cand = chain[ci];
                if(cand.CurrentProgram!=null && cand.TryFindFunction(funcName, out int fIdx, out int tgt) && tgt>=0) { obj = cand; break; }
            }
        }
        if(obj.CurrentProgram==null) return SValueS.FromNumber(0);
        int target=-1;
        for(int i=0;i<obj.CurrentProgram.FunctionTable.Length;i++)
            if(obj.CurrentProgram.FunctionTable[i].Name==funcName){ target=obj.CurrentProgram.FunctionTable[i].Address; break; }
        if(target<0) return SValueS.FromNumber(0);
        // save current state
        var savedProg=Prog;
        var savedStrings=Strings;
        var savedFuncAddr=new Dictionary<int,int>(FuncAddr);
        var savedProgObj=CurrentProgram;
        var savedPc=Pc;
        var savedSp=Sp;
        var savedLocals=new SValueS[256];
        Array.Copy(Locals, savedLocals, 64);
        var savedObj=CurrentObject;
        var savedCaptured=CurrentCaptured;
        var savedCallStack=new Stack<CallFrame>(CallStack);
        var savedStack2=new SValueS[STACK_SIZE]; Array.Copy(Stack, savedStack2, STACK_SIZE);

        CurrentProgram=obj.CurrentProgram;
        Prog=obj.CurrentProgram.Code;
        Strings=obj.CurrentProgram.Strings;
        FuncAddr.Clear();
        for(int i=0;i<obj.CurrentProgram.FunctionTable.Length;i++) FuncAddr[i]=obj.CurrentProgram.FunctionTable[i].Address;
        CurrentObject=obj;
        CurrentCaptured=System.Array.Empty<SValueS>();
        Sp=-1;
        for(int i=0;i<256;i++) Locals[i]=SValueS.Invalid;
        for(int i=0;i<args.Length && i<256;i++) Locals[i]=args[i];
        Pc=target;
        CallStack.Clear();
        long ret=RunLoop();
        SValueS result=Sp>=0? Stack[Sp] : SValueS.FromNumber(ret);
        Array.Copy(savedStack2, Stack, STACK_SIZE);

        // restore
        Prog=savedProg;
        Strings=savedStrings;
        FuncAddr=savedFuncAddr;
        CurrentProgram=savedProgObj;
        Pc=savedPc;
        Sp=savedSp;
        Array.Copy(savedLocals, Locals, 64);
        CurrentObject=savedObj;
        CurrentCaptured=savedCaptured;
        CallStack=new Stack<CallFrame>(savedCallStack);
        return result;
    }

    public void EvalInstruction(byte[] prog)
    {
        Prog=prog;
        Strings=Array.Empty<string>();
        FuncAddr.Clear();
        FuncAddr[0]=0;
        RunFromAddress(0);
    }
}

public sealed class InterpreterP33
{
    public InterpreterP9 Inner=new();
    public void SetProgram(Program p)=>Inner.SetProgram(p);
    public long RunFromMain()=>Inner.RunFromMain();
    public long RunFromAddress(int addr, LpcObject obj=null)=>Inner.RunFromAddress(addr,obj);
    public SValueS CallApply(string name, SValueS[] args, LpcObject obj=null)=>Inner.CallApply(name,args,obj);
    public void EvalInstruction(byte[] prog)=>Inner.EvalInstruction(prog);
}
public sealed class InterpreterP27_final
{
    public InterpreterP9 Inner=new();
    public void SetProgram(Program p)=>Inner.SetProgram(p);
    public long RunFromMain()=>Inner.RunFromMain();
    public long RunFromAddress(int addr, LpcObject obj=null)=>Inner.RunFromAddress(addr,obj);
    public SValueS CallApply(string name, SValueS[] args, LpcObject obj=null)=>Inner.CallApply(name,args,obj);
    public void EvalInstruction(byte[] prog)=>Inner.EvalInstruction(prog);
}
public class InterpreterV4
{
    public InterpreterP9 Inner=new();
    public void EvalInstruction(byte[] prog)=>Inner.EvalInstruction(prog);
    public void SetProgram(Program p)=>Inner.SetProgram(p);
    public long RunFromMain()=>Inner.RunFromMain();
    public long RunFromAddress(int addr, LpcObject obj=null)=>Inner.RunFromAddress(addr,obj);
    public SValueS CallApply(string name, SValueS[] args, LpcObject obj=null)=>Inner.CallApply(name,args,obj);
}
