using System;
using System.Collections.Generic;
namespace LithosNet.VM;

public sealed class CallFrame
{
    public int ReturnPc;
    public SValueS[] Locals = new SValueS[64];
    public int SpBase;
    public LpcObject CurrentObject;
    public CallFrame(int retPc, int spBase, LpcObject curObj = null)
    {
        ReturnPc = retPc;
        SpBase = spBase;
        CurrentObject = curObj;
        for (int i = 0; i < 64; i++) Locals[i] = SValueS.Invalid;
    }
}

public class InterpreterP9 {
    public const int STACK_SIZE = 2000;
    public SValueS[] Stack = new SValueS[STACK_SIZE];
    public int Sp = -1;
    public byte[] Prog = Array.Empty<byte>();
    public int Pc = 0;
    public string[] Strings = Array.Empty<string>();
    public Program CurrentProgram;

                                                                                                                            const byte F_SSCANF = 109;
const byte F_FUNCTION_CONSTRUCTOR = 107;
const byte F_RSH = 103;
const byte F_LSH = 102;
const byte F_DIVIDE = 97;
const byte F_MULTIPLY = 96;
const byte F_SUBTRACT = 95;
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
const byte F_CALL_FUNCTION_BY_ADDRESS = 43;
const byte F_AGGREGATE_ASSOC = 17;
const byte F_AGGREGATE = 16;
const byte F_EFUNV = 6;
const byte F_EFUN3 = 5;
const byte F_EFUN2 = 4;
const byte F_EFUN1 = 3;
const byte F_EFUN0 = 2;
const byte F_PUSH = 1;
const byte F_NUMBER = 7; // Neolith // Neolith // Neolith
    const byte F_LONG = 9; // Neolith // Neolith // Neolith
    const byte F_BYTE = 10; // Neolith // Neolith // Neolith
    const byte F_NBYTE = 11; // Neolith // Neolith // Neolith
    const byte F_BRANCH = 20; // Neolith // Neolith // Neolith
    const byte F_BRANCH_WHEN_ZERO = 18; // Neolith // Neolith // Neolith
    const byte F_BRANCH_WHEN_NON_ZERO = 19; // Neolith // Neolith // Neolith
    const byte F_AND = 99; // Neolith // Neolith // Neolith
    const byte F_OR = 100; // Neolith // Neolith // Neolith
    const byte F_XOR = 101; // Neolith // Neolith // Neolith
    const byte F_ADD = 94; // Neolith // Neolith // Neolith
    const byte F_SUB = 95; // Neolith // Neolith // Neolith
    const byte F_MUL = 96; // Neolith // Neolith // Neolith
    const byte F_DIV = 97; // Neolith // Neolith // Neolith
    const byte F_MOD = 98; // Neolith // Neolith // Neolith
    const byte F_SHL = 102; // Neolith // Neolith // Neolith
    const byte F_SHR = 103; // Neolith // Neolith // Neolith
    const byte F_LT = 50; // Neolith // Neolith // Neolith
    const byte F_GT = 52; // Neolith // Neolith // Neolith
    const byte F_LE = 49; // Neolith // Neolith // Neolith
    const byte F_GE = 51; // Neolith // Neolith // Neolith
    const byte F_EQ = 47; // Neolith // Neolith // Neolith
    const byte F_NE = 48; // Neolith // Neolith // Neolith
    const byte F_LOCAL = 60; // Neolith // Neolith // Neolith
    const byte F_STORE_LOCAL = 61; // Neolith // Neolith
    const byte F_CONST0 = 14; // Neolith
    const byte F_CONST1 = 15; // Neolith
    const byte F_STRING = 12; // Neolith // Neolith // Neolith
    const byte F_CALL = 43; // Neolith // Neolith // Neolith
    const byte F_CALL_OTHER = 114; // Neolith // Neolith // Neolith
    const byte F_CLONE_OBJECT = 122; // Neolith // Neolith
    const byte F_RETURN = 45; // Neolith // Neolith // Neolith
    const byte F_POP = 0; // Neolith // Neolith // Neolith
    const byte F_PUSH_MAPPING = 17; // Neolith // Neolith
    const byte F_MAP_ADD = 204; // Neolith
    const byte F_INDEX = 66; // Neolith // Neolith
    const byte F_INDEX_ASSIGN = 67; // Neolith // Neolith
    const byte F_PUSH_ARRAY = 16; // Neolith // Neolith
    const byte F_SIZEOF = 200; // Neolith
    const byte F_KEYS = 201; // Neolith
    const byte F_VALUES = 202; // Neolith
    const byte F_ASSIGN_LVALUE = 80;
    const byte F_M_DELETE = 203;
    const byte F_PUSH_CLOSURE = 107; // Neolith
    const byte F_EVALUATE = 115; // Neolith

    SValueS[] Locals = new SValueS[64];
    Stack<CallFrame> CallStack = new();
    Dictionary<int,int> FuncAddr = new();
    LpcObject CurrentObject = null;

    public InterpreterP9(){
        for(int i=0;i<STACK_SIZE;i++) Stack[i]=SValueS.Invalid;
        for(int i=0;i<64;i++) Locals[i]=SValueS.Invalid;
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
        foreach (var kv in FuncAddr) Console.WriteLine($"  func idx {kv.Key} @ {kv.Value}");
    }

    static int LoadInt(byte[] prog, ref int pc){
        int v = prog[pc] | (prog[pc+1]<<8) | (prog[pc+2]<<16) | (prog[pc+3]<<24);
        pc+=4; return v;
    }
    static long LoadLong(byte[] prog, ref int pc){
        long lo = (uint)(prog[pc] | (prog[pc+1]<<8) | (prog[pc+2]<<16) | (prog[pc+3]<<24));
        long hi = (uint)(prog[pc+4] | (prog[pc+5]<<8) | (prog[pc+6]<<16) | (prog[pc+7]<<24));
        pc+=8; return lo | (hi<<32);
    }
    static short LoadShort(byte[] prog, ref int pc){
        short v = (short)(prog[pc] | (prog[pc+1]<<8));
        pc+=2; return v;
    }

    void Push(SValueS v){
        if(Sp+1 >= STACK_SIZE) throw new Exception("*Stack overflow");
        Stack[++Sp]=v;
    }
    void PushNumber(long n){ Push(SValueS.FromNumber(n)); }
    void PushString(string s){ Push(new SValueS(s)); }
    void PushMapping(SValueMapping map){ var sv = SValue.NewMapping(map); var s = new SValueS(); s.Value = sv; s.U.Mapping = map; Push(s); }
    void PushArray(SValueArray arr){ var sv = SValue.NewArray(arr); var s = new SValueS(); s.Value = sv; s.U.Array = arr; Push(s); }
    void PushClosure(int funcIdx){ var sv = SValue.NewFunction(funcIdx); var s = new SValueS(); s.Value = sv; s.U.Number = funcIdx; Push(s); }
    void PushObject(LpcObject ob){ var sv = SValue.NewObject(ob); var s = new SValueS(); s.Value = sv; s.U.Object = ob; Push(s); }

    bool IsNil(SValueS sv){ return sv.U.Number==255 || sv.U.Number==-1; }
    int GetLength(SValueS sv){ if (sv.U.Array!=null) return sv.U.Array.Items.Count; if (sv.Value.Array!=null) return sv.Value.Array.Items.Count; if (sv.U.String!=null) return sv.U.String.Length; if (sv.Value.String!=null) return sv.Value.String.Length; return 0; }
    int FindLocalIndex(SValueS sv){ for (int i=0;i<Locals.Length;i++){ if (Locals[i].U.Array!=null && sv.U.Array!=null && Locals[i].U.Array==sv.U.Array) return i; if (Locals[i].U.String!=null && sv.U.String!=null && Locals[i].U.String==sv.U.String) return i; } return -1; }
    void AssignLValueChain(LValueDesc lvd, SValueS val){
        if (lvd==null) return;
        var stack = new System.Collections.Generic.List<LValueDesc>(); var cur=lvd; while (cur!=null){ stack.Add(cur); cur=cur.Parent; } stack.Reverse();
        if (stack.Count==0 || stack[0].Kind!=LValueKind.Local) return;
        int localIdx = stack[0].LocalIdx;
        if (localIdx<0 || localIdx>=Locals.Length) return;
        var rootSv = Locals[localIdx];
        SValueArray parentArr = null; int parentIdx=-1;
        SValueMapping parentMap = null; SValue parentMapKey = default;
        bool parentIsMap = false;
        var containerSv = rootSv;
        for (int i=1;i<stack.Count;i++){
            var d=stack[i];
            if (d.Kind==LValueKind.Index){
                int idxNum = 0;
                bool isNumIdx = false;
                if (d.Index!=null){
                    if (d.Index.Value.Type==SValueType.T_NUMBER) { idxNum=(int)d.Index.Value.Number; isNumIdx=true; }
                    else if (d.Index.U.Mapping==null && d.Index.U.Array==null && d.Index.U.String==null) { idxNum=(int)d.Index.U.Number; isNumIdx = d.Index.U.Number!=255 && d.Index.U.Number!=-1 || d.Index.Value.Type==SValueType.T_INVALID; }
                    else { isNumIdx=false; }
                }
                // 先試 array
                if (containerSv.U.Array!=null){
                    var arr=containerSv.U.Array;
                    int idx = idxNum;
                    if (isNumIdx){
                        if (idx>=0 && idx<arr.Items.Count){
                            if (i==stack.Count-1){ arr.Items[idx]=val.Value; return; }
                            else { var nextVal = arr.Items[idx]; var nextSv = new SValueS(); nextSv.Value=nextVal; if (nextVal.Array!=null) nextSv.U.Array=nextVal.Array; if (nextVal.Mapping!=null) nextSv.U.Mapping=nextVal.Mapping; if (nextVal.String!=null) nextSv.U.String=nextVal.String; parentArr=arr; parentIdx=idx; parentIsMap=false; containerSv=nextSv; continue; }
                        } else if (idx>=0 && i==stack.Count-1){
                            while (arr.Items.Count<=idx) arr.Items.Add(SValue.NewNumber(0));
                            arr.Items[idx]=val.Value; return;
                        }
                    }
                    // 若不是數字索引或超出，嘗試 mapping 鍵? array 不支援，跳過
                }
                if (containerSv.Value.Array!=null){
                    var arr=containerSv.Value.Array;
                    int idx = idxNum;
                    if (isNumIdx && idx>=0 && idx<arr.Items.Count){
                        if (i==stack.Count-1){ arr.Items[idx]=val.Value; var newSv = new SValueS(); newSv.Value=SValue.NewArray(arr); newSv.U.Array=arr; Locals[localIdx]=newSv; return; }
                        else { var nextVal = arr.Items[idx]; var nextSv = new SValueS(); nextSv.Value=nextVal; if (nextVal.Array!=null) nextSv.U.Array=nextVal.Array; if (nextVal.Mapping!=null) nextSv.U.Mapping=nextVal.Mapping; if (nextVal.String!=null) nextSv.U.String=nextVal.String; parentArr=arr; parentIdx=idx; parentIsMap=false; containerSv=nextSv; continue; }
                    }
                }
                // mapping 分支
                var map = containerSv.U.Mapping ?? containerSv.Value.Mapping;
                if (map!=null){
                    SValue key = ToSValue(d.Index);
                    if (i==stack.Count-1){ map.Map[key]=ToSValue(val); return; }
                    else {
                        if (map.Map.TryGetValue(key, out var nextVal)){
                            var nextSv = new SValueS(); nextSv.Value=nextVal; nextSv.U.Array=nextVal.Array; nextSv.U.Mapping=nextVal.Mapping; nextSv.U.String=nextVal.String; nextSv.U.Number=nextVal.Number;
                            parentMap=map; parentMapKey=key; parentIsMap=true; containerSv=nextSv; continue;
                        } else {
                            // 自動創建中間 mapping? 對於 a["b"]["c"]=1，如果中間不存在，先創空 mapping
                            var newMap = new SValueMapping();
                            map.Map[key]=SValue.NewMapping(newMap);
                            var nextSv = new SValueS(); nextSv.Value=SValue.NewMapping(newMap); nextSv.U.Mapping=newMap;
                            parentMap=map; parentMapKey=key; parentIsMap=true; containerSv=nextSv; continue;
                        }
                    }
                }
            } else if (d.Kind==LValueKind.Range){
                int len=0;
                if (containerSv.U.Array!=null) len=containerSv.U.Array.Items.Count;
                else if (containerSv.U.String!=null) len=containerSv.U.String.Length;
                else if (containerSv.Value.String!=null) len=containerSv.Value.String.Length;
                bool sNil = d.Start!=null && IsNil(d.Start);
                bool eNil = d.End!=null && IsNil(d.End);
                int s = sNil ? 0 : (d.Start!=null ? (int)d.Start.U.Number : 0);
                int e = eNil ? len-1 : (d.End!=null ? (int)d.End.U.Number : len-1);
                if (sNil) s=0;
                if (eNil) e=len-1;
                if (d.SRev) s = len - s;
                if (d.ERev) e = len - e;
                if (d.EExcl) e = e - 1;
                if (s<0) s=0; if (e>=len) e=len-1;
                string str = containerSv.U.String ?? containerSv.Value.String ?? "";
                string newStr = val.U.String ?? val.Value.String ?? "";
                string before = s>0 ? str.Substring(0,s) : "";
                string after = e+1 < str.Length ? str.Substring(e+1) : "";
                string result = before + newStr + after;
                var newSv = new SValueS(result);
                if (parentArr!=null && parentIdx>=0){ parentArr.Items[parentIdx]=newSv.Value; }
                else { Locals[localIdx]=newSv; }
                return;
            }
        }
        if (stack.Count==1) Locals[localIdx]=val;
    }
    void HandleEfun(byte efun, int nargs, SValueS a0=default, SValueS a1=default, SValueS a2=default){
        switch(efun){
            case 23:
                {
                    string s = a0.U.String ?? a0.Value.String ?? "";
                    if (s=="" && a0.U.Array!=null) s = FormatArray(a0.U.Array);
                    else if (s=="" && a0.Value.Array!=null) s = FormatArray(a0.Value.Array);
                    else if (s=="") s = a0.U.Number.ToString();
                    Console.Write(s);
                    PushNumber(0);
                    break;
                }
            default: PushNumber(0); break;
        }
    }
    void HandleEfunV(byte efun, SValueS[] args){ if (args.Length>0) HandleEfun(efun, args.Length, args[0], args.Length>1?args[1]:default, args.Length>2?args[2]:default); else HandleEfun(efun,0); }
    string FormatArray(SValueArray arr){
        if (arr==null) return "({})";
        var parts = new System.Collections.Generic.List<string>();
        foreach (var it in arr.Items){
            if (it.Type==SValueType.T_NUMBER) parts.Add(it.Number.ToString());
            else if (it.Type==SValueType.T_STRING) parts.Add(it.String);
            else if (it.Array!=null) parts.Add(FormatArray(it.Array));
            else parts.Add(it.ToString());
        }
        return "({"+string.Join(",", parts)+"})";
    }


    SValueS Pop(){ if(Sp<0) throw new Exception($"Pop empty at Pc={Pc-1}"); var v=Stack[Sp]; Stack[Sp]=SValueS.Invalid; Sp--; return v; }

    SValue ToSValue(SValueS ss)
    {
        if (ss.Value.Type != SValueType.T_INVALID) return ss.Value;
        if (ss.U.String != null) return SValue.NewString(ss.U.String);
        if (ss.U.Mapping != null) return SValue.NewMapping(ss.U.Mapping);
        if (ss.U.Array != null) return SValue.NewArray(ss.U.Array);
        if (ss.U.Object != null) return SValue.NewObject(ss.U.Object);
        return SValue.NewNumber(ss.U.Number);
    }

    public long RunFromMain()
    {
        Sp = -1;
        for(int i=0;i<STACK_SIZE;i++) Stack[i]=SValueS.Invalid;
        for(int i=0;i<64;i++) Locals[i]=SValueS.Invalid;
        CallStack.Clear();
        CurrentObject = new LpcObject("<main>", CurrentProgram);
        int mainAddr = -1;
        if (CurrentProgram?.FunctionTable != null)
        {
            for (int i=0;i<CurrentProgram.FunctionTable.Length;i++)
            {
                if (CurrentProgram.FunctionTable[i].Name == "main")
                {
                    mainAddr = CurrentProgram.FunctionTable[i].Address;
                    break;
                }
            }
            if (mainAddr <0)
            {
                // fallback: last non-closure function
                for (int i=CurrentProgram.FunctionTable.Length-1;i>=0;i--)
                {
                    var name = CurrentProgram.FunctionTable[i].Name;
                    if (!name.StartsWith("__closure") && !name.StartsWith("__filter") && !name.StartsWith("__map"))
                    {
                        mainAddr = CurrentProgram.FunctionTable[i].Address;
                        break;
                    }
                }
            }
        }
        if (mainAddr <0 && FuncAddr.Count >= 1) mainAddr = FuncAddr[FuncAddr.Count-1];
        Pc = mainAddr >=0 ? mainAddr : 0;


        while (Pc < Prog.Length)
        {
            byte instr = Prog[Pc++];
            switch(instr){
                case F_CONST0:{ PushNumber(0); break; }
                case F_CONST1:{ PushNumber(1); break; }
                case F_NUMBER:{ int i = LoadInt(Prog, ref Pc); PushNumber(i); break; }
                case F_LONG:{ long lv = LoadLong(Prog, ref Pc); PushNumber(lv); break; }
                case F_BYTE:{ byte b = Prog[Pc++]; PushNumber((sbyte)b); break; }
                case F_NBYTE:{ byte b = Prog[Pc++]; PushNumber(-(int)b); break; }
                case F_STRING:{ short sIdx = LoadShort(Prog, ref Pc); string s = (sIdx>=0 && sIdx < Strings.Length) ? Strings[sIdx] : ""; PushString(s); break; }
                case F_PUSH_MAPPING:{ var map = new SValueMapping(); PushMapping(map); break; }
                case F_PUSH_ARRAY:{
                    var szSv = Pop();
                    int sz = (int)szSv.U.Number;
                    if (szSv.Value.Type == SValueType.T_NUMBER) sz = (int)szSv.Value.Number;
                    if (sz <0) sz =0; if (sz>10000) sz=10000;
                    var arr = new SValueArray();
                    for (int i=0;i<sz;i++) arr.Items.Add(SValue.NewNumber(0));
                    PushArray(arr);
                    break;
                }
                case F_PUSH_CLOSURE:{ short fIdx = LoadShort(Prog, ref Pc); PushClosure(fIdx); break; }
                case F_CLONE_OBJECT:{
                    var pathSv = Pop();
                    string path = pathSv.U.String ?? pathSv.Value.String ?? "<clone>";
                    var ob = new LpcObject(path, CurrentProgram);
                    PushObject(ob);
                    break;
                }
                case F_EVALUATE:{
                    byte nargs = Prog[Pc++];
                    if (nargs <1) { PushNumber(0); break; }
                    int closurePos = Sp - nargs + 1;
                    if (closurePos <0) { PushNumber(0); break; }
                    var closureSv = Stack[closurePos];
                    int funcIdx = (int)closureSv.U.Number;
                    if (closureSv.Value.Type == SValueType.T_FUNCTION) funcIdx = closureSv.Value.FunctionIndex;
                    var args = new List<SValueS>();
                    for (int i=1;i<nargs;i++) args.Add(Stack[closurePos+i]);
                    for (int i=0;i<nargs;i++) Pop();
                    if (!FuncAddr.TryGetValue(funcIdx, out int target)) { Console.WriteLine($"[F_EVALUATE] unknown func idx {funcIdx}"); PushNumber(0); break; }
                    var frame = new CallFrame(Pc, Sp+1, CurrentObject);
                    for(int i=0;i<64;i++) frame.Locals[i]=Locals[i];
                    CallStack.Push(frame);
                    for(int i=0;i<64;i++) Locals[i]=SValueS.Invalid;
                    for (int i=0;i<args.Count && i<64;i++) Locals[i]=args[i];
                    Pc = target;
                    break;
                }
                case F_CALL_OTHER:{
                    byte nargs = Prog[Pc++];
                    // stack: ... object, methodName, arg0..argN-1  Sp = last arg or method if nargs=0
                    if (Sp < 1) { Console.WriteLine($"[F_CALL_OTHER] underflow Sp={Sp} nargs={nargs}"); PushNumber(0); break; }
                    int methodPos = Sp - nargs; // methodName index
                    int objPos = methodPos - 1;
                    if (objPos < 0 || methodPos <0 || methodPos > Sp)
                    {
                        Console.WriteLine($"[F_CALL_OTHER] bad positions objPos={objPos} methodPos={methodPos} Sp={Sp} nargs={nargs}");
                        PushNumber(0); break;
                    }
                    var objSv = Stack[objPos];
                    var methodSv = Stack[methodPos];
                    string methodName = methodSv.U.String ?? methodSv.Value.String ?? "";
                    // save args
                    var argList = new List<SValueS>();
                    for (int i=0;i<nargs;i++) argList.Add(Stack[methodPos+1+i]);

                    // pop args
                    for (int i=0;i<nargs;i++) Pop();
                    // pop method and object
                    Pop(); // method
                    Pop(); // object
                    // push args back for callee prologue (STORE_LOCAL)
                    for (int i=0;i<argList.Count;i++) Push(argList[i]);

                    LpcObject ob = objSv.U.Object as LpcObject ?? objSv.Value.Object as LpcObject;
                    int target = -1; int fIdx = -1;
                    if (ob != null && ob.TryFindFunction(methodName, out fIdx, out target)) { }
                    else
                    {
                        bool found = false;
                        if (CurrentProgram?.FunctionTable != null)
                        {
                            for (int i=0;i<CurrentProgram.FunctionTable.Length;i++)
                            {
                                if (CurrentProgram.FunctionTable[i].Name == methodName)
                                {
                                    fIdx = i; target = CurrentProgram.FunctionTable[i].Address; found = true; break;
                                }
                            }
                        }
                        if (!found) { Console.WriteLine($"[F_CALL_OTHER] method {methodName} not found"); for (int i=0;i<nargs;i++) if(Sp>=0) Pop(); PushNumber(0); break; }
                    }
                    var frame = new CallFrame(Pc, Sp - nargs, CurrentObject);
                    for(int i=0;i<64;i++) frame.Locals[i]=Locals[i];
                    CallStack.Push(frame);
                    for(int i=0;i<64;i++) Locals[i]=SValueS.Invalid;
                    CurrentObject = ob ?? CurrentObject;
                    Pc = target;
                    break;
                }
                case F_MAP_ADD:{
                    var val = Pop(); var key = Pop(); var mapSv = Pop();
                    var map = mapSv.U.Mapping ?? mapSv.Value.Mapping;
                    if (map == null) { map = new SValueMapping(); }
                    SValue k = ToSValue(key); SValue v = ToSValue(val);
                    map.Map[k] = v;
                    PushMapping(map);
                    break;
                }
                case F_INDEX:{
                    var idxSv = Pop(); var contSv = Pop();
                    SValue idx = ToSValue(idxSv);
                    var arr = contSv.U.Array ?? contSv.Value.Array;
                    if (arr != null)
                    {
                        int off = (int)idxSv.U.Number;
                        if (idxSv.Value.Type == SValueType.T_NUMBER) off = (int)idxSv.Value.Number;
                        if (off <0 || off >= arr.Items.Count) { PushNumber(0); }
                        else {
                            var elem = arr.Items[off];
                            if (elem.Type == SValueType.T_NUMBER) PushNumber(elem.Number);
                            else if (elem.Type == SValueType.T_STRING) PushString(elem.String);
                            else { var s = new SValueS(); s.Value = elem; s.U.Array = elem.Array; s.U.Mapping = elem.Mapping; s.U.String = elem.String; s.U.Number = elem.Number; Push(s); }
                        }
                        break;
                    }
                    var map = contSv.U.Mapping ?? contSv.Value.Mapping;
                    if (map != null)
                    {
                        if (map.Map.TryGetValue(idx, out var v)) {
                            if (v.Type == SValueType.T_NUMBER) PushNumber(v.Number);
                            else if (v.Type == SValueType.T_STRING) PushString(v.String);
                            else { var s = new SValueS(); s.Value = v; s.U.Mapping = v.Mapping; s.U.Array = v.Array; s.U.String = v.String; s.U.Number = v.Number; Push(s); }
                        } else { PushNumber(0); }
                        break;
                    }
                    var str = contSv.U.String ?? contSv.Value.String;
                    if (str != null)
                    {
                        int off = (int)idxSv.U.Number;
                        if (off>=0 && off < str.Length) PushNumber(str[off]);
                        else PushNumber(0);
                        break;
                    }
                    PushNumber(0);
                    break;
                }

                case F_SIZEOF:{
                    var c = Pop();
                    var arr = c.U.Array ?? c.Value.Array;
                    if (arr != null) { PushNumber(arr.Items.Count); break; }
                    var map = c.U.Mapping ?? c.Value.Mapping;
                    if (map != null) { PushNumber(map.Map.Count); break; }
                    var str = c.U.String ?? c.Value.String;
                    if (str != null) { PushNumber(str.Length); break; }
                    PushNumber(0);
                    break;
                }
                case F_KEYS:{
                    var c = Pop();
                    var map = c.U.Mapping ?? c.Value.Mapping;
                    if (map != null) { var arr = new SValueArray(); foreach (var kv in map.Map) arr.Items.Add(kv.Key); PushArray(arr); }
                    else PushNumber(0);
                    break;
                }
                case F_VALUES:{
                    var c = Pop();
                    var map = c.U.Mapping ?? c.Value.Mapping;
                    if (map != null) { var arr = new SValueArray(); foreach (var kv in map.Map) arr.Items.Add(kv.Value); PushArray(arr); }
                    else PushNumber(0);
                    break;
                }
                case F_M_DELETE:{
                    var keySv = Pop(); var mapSv = Pop();
                    var map = mapSv.U.Mapping ?? mapSv.Value.Mapping;
                    if (map != null) { SValue k = ToSValue(keySv); map.Map.Remove(k); PushMapping(map); }
                    else PushNumber(0);
                    break;
                }
                case F_AND:{ var r = Pop(); var l = Pop(); PushNumber(l.U.Number & r.U.Number); break; }
                case F_OR:{ var r = Pop(); var l = Pop(); PushNumber(l.U.Number | r.U.Number); break; }
                case F_XOR:{ var r = Pop(); var l = Pop(); PushNumber(l.U.Number ^ r.U.Number); break; }
                case F_ADD:{
                    var r = Pop(); var l = Pop();
                    if ((l.U.String!=null || l.Value.String!=null) || (r.U.String!=null || r.Value.String!=null))
                    {
                        string ls = l.U.String ?? l.Value.String ?? l.U.Number.ToString();
                        string rs = r.U.String ?? r.Value.String ?? r.U.Number.ToString();
                        if (l.Value.Type == SValueType.T_STRING) ls = l.Value.String;
                        if (r.Value.Type == SValueType.T_STRING) rs = r.Value.String;
                        PushString(ls + rs);
                    }
                    else if ((l.U.Array!=null || l.Value.Array!=null) && (r.U.Array!=null || r.Value.Array!=null))
                    {
                        var la = l.U.Array ?? l.Value.Array;
                        var ra = r.U.Array ?? r.Value.Array;
                        var na = new SValueArray();
                        if (la!=null) foreach (var it in la.Items) na.Items.Add(it);
                        if (ra!=null) foreach (var it in ra.Items) na.Items.Add(it);
                        PushArray(na);
                    }
                    else { PushNumber(l.U.Number + r.U.Number); }
                    break;
                }
                case F_SUB:{ var r = Pop(); var l = Pop(); PushNumber(l.U.Number - r.U.Number); break; }
                case F_MUL:{
                    var r = Pop(); var l = Pop();
                    string ls = l.U.String ?? l.Value.String;
                    string rs = r.U.String ?? r.Value.String;
                    bool lIsStr = ls != null;
                    bool rIsStr = rs != null;
                    long lNum = l.U.Number; long rNum = r.U.Number;
                    if (l.Value.Type == SValueType.T_NUMBER) lNum = l.Value.Number;
                    if (r.Value.Type == SValueType.T_NUMBER) rNum = r.Value.Number;
                    if (lIsStr && !rIsStr)
                    {
                        int times = (int)rNum; if (times<0) times=0; if (times>10000) times=10000;
                        var sb = new System.Text.StringBuilder(ls.Length * times);
                        for (int i=0;i<times;i++) sb.Append(ls);
                        PushString(sb.ToString());
                    }
                    else if (!lIsStr && rIsStr)
                    {
                        int times = (int)lNum; if (times<0) times=0; if (times>10000) times=10000;
                        var sb = new System.Text.StringBuilder(rs.Length * times);
                        for (int i=0;i<times;i++) sb.Append(rs);
                        PushString(sb.ToString());
                    }
                    else { PushNumber(lNum * rNum); }
                    break;
                }
                case F_DIV:{ var r = Pop(); var l = Pop(); PushNumber(r.U.Number!=0? l.U.Number / r.U.Number : 0); break; }
                case F_MOD:{ var r = Pop(); var l = Pop(); PushNumber(r.U.Number!=0? l.U.Number % r.U.Number : 0); break; }
                case F_SHL:{ var r = Pop(); var l = Pop(); PushNumber(l.U.Number << (int)r.U.Number); break; }
                case F_SHR:{ var r = Pop(); var l = Pop(); PushNumber(l.U.Number >> (int)r.U.Number); break; }
                case F_LT:{ var r = Pop(); var l = Pop(); PushNumber(l.U.Number < r.U.Number ? 1 : 0); break; }
                case F_GT:{ var r = Pop(); var l = Pop(); PushNumber(l.U.Number > r.U.Number ? 1 : 0); break; }
                case F_LE:{ var r = Pop(); var l = Pop(); PushNumber(l.U.Number <= r.U.Number ? 1 : 0); break; }
                case F_GE:{ var r = Pop(); var l = Pop(); PushNumber(l.U.Number >= r.U.Number ? 1 : 0); break; }
                case F_EQ:{ var r = Pop(); var l = Pop(); PushNumber(l.U.Number == r.U.Number ? 1 : 0); break; }
                case F_NE:{ var r = Pop(); var l = Pop(); PushNumber(l.U.Number != r.U.Number ? 1 : 0); break; }
                case F_EFUN0:{ byte efun = Prog[Pc++]; HandleEfun(efun, 0); break; }
                case F_EFUN1:{ byte efun = Prog[Pc++]; var a0 = Pop(); HandleEfun(efun, 1, a0); break; }
                case F_EFUN2:{ byte efun = Prog[Pc++]; var a1 = Pop(); var a0 = Pop(); HandleEfun(efun, 2, a0, a1); break; }
                case F_EFUN3:{ byte efun = Prog[Pc++]; var a2 = Pop(); var a1 = Pop(); var a0 = Pop(); HandleEfun(efun, 3, a0, a1, a2); break; }
                case F_EFUNV:{ byte efun = Prog[Pc++]; byte nargs = Prog[Pc++]; var args = new SValueS[nargs]; for (int i=nargs-1;i>=0;i--) args[i]=Pop(); HandleEfunV(efun, args); break; }
                case F_RINDEX:{
                    var idxSv = Pop(); var contSv = Pop();
                    int idx = (int)idxSv.U.Number;
                    int len = GetLength(contSv);
                    int off = len - idx;
                    if (contSv.U.Array!=null){
                        var arr=contSv.U.Array;
                        if (off>=0 && off < arr.Items.Count){
                            Push(new SValueS{ Value=arr.Items[off] });
                        } else PushNumber(0);
                    } else if (contSv.U.String!=null){
                        var str=contSv.U.String;
                        if (off>=0 && off < str.Length) PushNumber(str[off]);
                        else PushNumber(0);
                    } else PushNumber(0);
                    break;
                }
                case F_NN_RANGE:
                case F_RN_RANGE:
                case F_NR_RANGE:
                case F_RR_RANGE:
                case F_NE_RANGE:
                case F_RE_RANGE:{
                    var endSv = Pop(); var startSv = Pop(); var contSv = Pop();
                    bool sRev = (instr==F_RN_RANGE || instr==F_RR_RANGE || instr==F_RE_RANGE);
                    bool eRev = (instr==F_NR_RANGE || instr==F_RR_RANGE || instr==F_NE_RANGE || instr==F_RE_RANGE);
                    bool eExcl = (instr==F_NE_RANGE || instr==F_RE_RANGE);
                    int len = GetLength(contSv);
                    int s = IsNil(startSv) ? 0 : (int)startSv.U.Number;
                    int e = IsNil(endSv) ? len-1 : (int)endSv.U.Number;
                    if (sRev) s = len - s;
                    if (eRev) e = len - e;
                    if (eExcl) e = e - 1;
                    if (s<0) s=0; if (e>=len) e=len-1;
                    if (contSv.U.Array!=null){
                        var arr=contSv.U.Array;
                        var res = new SValueArray();
                        for (int i=s;i<=e && i>=0 && i<arr.Items.Count;i++) res.Items.Add(arr.Items[i]);
                        var sv = new SValueS(); sv.Value=SValue.NewArray(res); sv.U.Array=res;
                        Push(sv);
                    } else if (contSv.U.String!=null){
                        var str=contSv.U.String;
                        if (s<0) s=0; if (e>=str.Length) e=str.Length-1;
                        if (s>e) PushString("");
                        else PushString(str.Substring(s, e-s+1));
                    } else PushNumber(0);
                    break;
                }
                case F_NN_RANGE_LVALUE:
                case F_RN_RANGE_LVALUE:
                case F_RR_RANGE_LVALUE:
                case F_NR_RANGE_LVALUE:{
                    bool isAssign = Pc < Prog.Length && Prog[Pc]==F_POP;
                    if (isAssign){
                        var valSv = Pop(); var endSv = Pop(); var startSv = Pop(); var contSv = Pop();
                        bool sRev = (instr==F_RN_RANGE_LVALUE || instr==F_RR_RANGE_LVALUE);
                        bool eRev = (instr==F_NR_RANGE_LVALUE || instr==F_RR_RANGE_LVALUE);
                        int len = GetLength(contSv);
                        int s = IsNil(startSv) ? 0 : (int)startSv.U.Number;
                        int e = IsNil(endSv) ? len-1 : (int)endSv.U.Number;
                        if (sRev) s = len - s;
                        if (eRev) e = len - e;
                        if (s<0) s=0; if (e>=len) e=len-1;
                        if (contSv.U.Array!=null){
                            var arr=contSv.U.Array;
                            var newArr = new SValueArray();
                            for (int i=0;i<s && i<arr.Items.Count;i++) newArr.Items.Add(arr.Items[i]);
                            if (valSv.U.Array!=null){ foreach (var it in valSv.U.Array.Items) newArr.Items.Add(it); }
                            else newArr.Items.Add(valSv.Value);
                            for (int i=e+1;i<arr.Items.Count;i++) newArr.Items.Add(arr.Items[i]);
                            int localIdx = FindLocalIndex(contSv);
                            if (localIdx>=0){ var newSv = new SValueS(); newSv.Value=SValue.NewArray(newArr); newSv.U.Array=newArr; Locals[localIdx]=newSv; }
                            Push(new SValueS());
                        } else if (contSv.U.String!=null || contSv.Value.String!=null){
                            string str = contSv.U.String ?? contSv.Value.String ?? "";
                            string newStr = valSv.U.String ?? valSv.Value.String ?? "";
                            string before = s>0 ? str.Substring(0, System.Math.Min(s, str.Length)) : "";
                            string after = e+1 < str.Length ? str.Substring(e+1) : "";
                            string result = before + newStr + after;
                            int localIdx = FindLocalIndex(contSv);
                            if (localIdx>=0){ Locals[localIdx]=new SValueS(result); }
                            Push(new SValueS(result));
                        } else Push(valSv);
                    } else {
                        var endSv = Pop(); var startSv = Pop(); var parentSv = Pop();
                        if (parentSv.U.LValue!=null){
                            bool sRev = (instr==F_RN_RANGE_LVALUE || instr==F_RR_RANGE_LVALUE);
                            bool eRev = (instr==F_NR_RANGE_LVALUE || instr==F_RR_RANGE_LVALUE);
                            var lvd = new LValueDesc{ Kind=LValueKind.Range, Parent=parentSv.U.LValue, Start=startSv, End=endSv, SRev=sRev, ERev=eRev, EExcl=false };
                            var sv = new SValueS(); sv.Value=SValue.NewLValue(lvd); sv.U.LValue=lvd;
                            Push(sv);
                        } else Push(parentSv);
                    }
                    break;
                }
                case F_RINDEX_LVALUE:{
                    bool isAssign = Pc < Prog.Length && Prog[Pc]==F_POP;
                    if (isAssign){
                        var valSv = Pop(); var idxSv = Pop(); var contSv = Pop();
                        int idx = (int)idxSv.U.Number;
                        int len = GetLength(contSv);
                        int off = len - idx;
                        int localIdx = FindLocalIndex(contSv);
                        if (localIdx>=0 && Locals[localIdx].U.Array!=null){
                            var arr=Locals[localIdx].U.Array;
                            if (off>=0 && off < arr.Items.Count) arr.Items[off]=valSv.Value;
                            Push(valSv);
                        } else Push(valSv);
                    } else {
                        var idxSv = Pop(); var parentSv = Pop();
                        if (parentSv.U.LValue!=null){
                            var lvd = new LValueDesc{ Kind=LValueKind.RIndex, Parent=parentSv.U.LValue, Index=idxSv };
                            var sv = new SValueS(); sv.Value=SValue.NewLValue(lvd); sv.U.LValue=lvd;
                            Push(sv);
                        } else Push(parentSv);
                    }
                    break;
                }
                case F_INDEX_LVALUE:{
                    bool isAssign = Pc < Prog.Length && Prog[Pc]==F_POP;
                    if (isAssign){
                        var valSv = Pop(); var idxSv = Pop(); var contSv = Pop();
                        if (contSv.U.Array!=null){
                            var arr=contSv.U.Array;
                            int i=(int)idxSv.U.Number;
                            if (idxSv.Value.Type==SValueType.T_NUMBER) i=(int)idxSv.Value.Number;
                            else if (idxSv.U.Number==255 || idxSv.U.Number==-1) i=0;
                            if (i>=0 && i<arr.Items.Count) arr.Items[i]=valSv.Value;
                            else if (i>=0){
                                while (arr.Items.Count<=i) arr.Items.Add(SValue.NewNumber(0));
                                arr.Items[i]=valSv.Value;
                            }
                            Push(valSv);
                        } else if (contSv.U.Mapping!=null || contSv.Value.Mapping!=null){
                            var map = contSv.U.Mapping ?? contSv.Value.Mapping;
                            if (map!=null){ SValue k=ToSValue(idxSv); SValue v=ToSValue(valSv); map.Map[k]=v; }
                            Push(valSv);
                        } else if (contSv.Value.Array!=null){
                            var arr=contSv.Value.Array;
                            int i=(int)idxSv.U.Number;
                            if (i>=0 && i<arr.Items.Count) arr.Items[i]=valSv.Value;
                            Push(valSv);
                        } else Push(valSv);
                    } else {
                        var idxSv = Pop(); var parentSv = Pop();
                        if (parentSv.U.LValue!=null){
                            var lvd = new LValueDesc{ Kind=LValueKind.Index, Parent=parentSv.U.LValue, Index=idxSv };
                            var sv = new SValueS(); sv.Value=SValue.NewLValue(lvd); sv.U.LValue=lvd;
                            Push(sv);
                        } else {
                            // 兼容舊的直接索引 LValue 也要能產生鏈，否則 mapping 字面量會被 Push(parent) 吃掉
                            if (parentSv.U.Mapping!=null || parentSv.Value.Mapping!=null || parentSv.U.Array!=null || parentSv.Value.Array!=null || parentSv.U.String!=null){
                                // 如果 parent 是具體容器但不是 LValue，我們仍需把它包裝成 Local? 實際上這條分支是當 base 是 Local LValue 時才會進，上層已經是 LValue，所以這裡只處理 LValue parent
                                Push(parentSv);
                            } else Push(parentSv);
                        }
                    }
                    break;
                }
                case F_SSCANF:{
                    byte total = Prog[Pc++];
                    var args = new SValueS[total];
                    for (int i=total-1;i>=0;i--) args[i]=Pop();
                    string input = args[0].U.String ?? args[0].Value.String ?? "";
                    int matched=0;
                    var words = input.Split(new[]{' '}, System.StringSplitOptions.RemoveEmptyEntries);
                    for (int i=2;i<total;i++){
                        string valStr;
                        if (i == total-1){
                            int startIdx=0;
                            for (int k=0;k<i-2 && k<words.Length;k++){ int pos=input.IndexOf(words[k], startIdx); if (pos>=0) startIdx=pos+words[k].Length; }
                            valStr = startIdx < input.Length ? input.Substring(startIdx) : "";
                            if (valStr.Length>0 && valStr[0]==' ') valStr = " " + valStr.TrimStart(); else valStr = valStr.TrimStart();
                        } else {
                            if (i-2 < words.Length) valStr = words[i-2];
                            else valStr = "";
                        }
                        if (i-2 < words.Length) matched++;
                        var lval = args[i];
                        if (lval.U.LValue!=null) AssignLValueChain(lval.U.LValue, new SValueS(valStr));
                    }
                    PushNumber(matched);
                    break;
                }
                case F_ASSIGN_LVALUE:{
                    var valSv = Pop();
                    var lvalSv = Pop();
                    if (lvalSv.U.LValue!=null) AssignLValueChain(lvalSv.U.LValue, valSv);
                    Push(valSv);
                    break;
                }
                case F_LOCAL:{ byte idx = Prog[Pc++]; var lv = Locals[idx]; Push(lv); break; }
                case F_STORE_LOCAL:{
                    byte idx = Prog[Pc++];
                    bool isAssign = Pc < Prog.Length && Prog[Pc]==F_POP;
                    if (isAssign){ var v = Pop(); Locals[idx]=v; Push(v); }
                    else { var lvd = new LValueDesc{ Kind=LValueKind.Local, LocalIdx=idx }; var sv = new SValueS(); sv.Value=SValue.NewLValue(lvd); sv.U.LValue=lvd; Push(sv); }
                    break;
                }
                case F_POP:{ Pop(); break; }
                case F_BRANCH:{ short off = LoadShort(Prog, ref Pc); Pc += off; break; }
                case F_BRANCH_WHEN_ZERO:{ short off = LoadShort(Prog, ref Pc); var v = Pop(); if(v.U.Number==0) Pc+=off; break; }
                case F_BRANCH_WHEN_NON_ZERO:{ short off = LoadShort(Prog, ref Pc); var v = Pop(); if(v.U.Number!=0) Pc+=off; break; }
                case F_CALL:{
                    short fIdx = LoadShort(Prog, ref Pc);
                    byte nargs = Prog[Pc++];
                    int target = FuncAddr.ContainsKey(fIdx) ? FuncAddr[fIdx] : -1;
                    if (target < 0) { Console.WriteLine($"[F_CALL] unknown func idx {fIdx}"); PushNumber(0); break; }
                    var frame = new CallFrame(Pc, Sp - nargs, CurrentObject);
                    for(int i=0;i<64;i++) frame.Locals[i]=Locals[i];
                    CallStack.Push(frame);
                    for(int i=0;i<64;i++) Locals[i]=SValueS.Invalid;
                    Pc = target;
                    break;
                }
                case F_RETURN:{
                    var retVal = Sp>=0? Stack[Sp] : SValueS.FromNumber(0);
                    if (Sp>=0) { Pop(); }
                    if (CallStack.Count == 0)
                    {
                        long num = retVal.U.Number;
                        if (retVal.Value.Type == SValueType.T_NUMBER) num = retVal.Value.Number;
                        else if (retVal.U.Mapping != null || retVal.Value.Mapping != null) num = retVal.U.Mapping?.Map.Count ?? retVal.Value.Mapping?.Map.Count ?? 0;
                        else if (retVal.U.Array != null || retVal.Value.Array != null) num = retVal.U.Array?.Items.Count ?? retVal.Value.Array?.Items.Count ?? 0;
                        else if (retVal.U.String != null) num = retVal.U.String.Length;
                        else if (retVal.Value.String != null) num = retVal.Value.String.Length;
                        PushNumber(num);
                        return num;
                    }
                    else
                    {
                        var frame = CallStack.Pop();
                        for(int i=0;i<64;i++) Locals[i]=frame.Locals[i];
                        CurrentObject = frame.CurrentObject;
                        Sp = frame.SpBase;
                        Push(retVal);
                        Pc = frame.ReturnPc;
                        break;
                    }
                }
                default:{
                    if(instr >= 100){ PushNumber(0); }
                    else { Console.WriteLine($"[interp] unknown opcode {instr:X2} at pc {Pc-1}, stopping"); return Sp>=0? Stack[Sp].U.Number : 0; }
                    break;
                }
            }
        }
        return Sp>=0? Stack[Sp].U.Number : 0;
    }

    public void EvalInstruction(byte[] prog){
        Prog = prog;
        Strings = Array.Empty<string>();
        FuncAddr.Clear();
        FuncAddr[0]=0;
        RunFromMain();
    }
}

public sealed class InterpreterP33 {
    public InterpreterP9 Inner = new();
    public void SetProgram(Program p) => Inner.SetProgram(p);
    public long RunFromMain() => Inner.RunFromMain();
    public void EvalInstruction(byte[] prog) => Inner.EvalInstruction(prog);
}
public sealed class InterpreterP27_final {
    public InterpreterP9 Inner = new();
    public void SetProgram(Program p) => Inner.SetProgram(p);
    public long RunFromMain() => Inner.RunFromMain();
    public void EvalInstruction(byte[] prog) => Inner.EvalInstruction(prog);
}
public class InterpreterV4 {
    public InterpreterP9 Inner = new();
    public void EvalInstruction(byte[] prog) => Inner.EvalInstruction(prog);
    public void SetProgram(Program p) => Inner.SetProgram(p);
    public long RunFromMain() => Inner.RunFromMain();
}

