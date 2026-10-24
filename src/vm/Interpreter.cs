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
    const byte F_MAP_ADD = 71; // Neolith
    const byte F_INDEX = 66; // Neolith // Neolith
    const byte F_INDEX_ASSIGN = 67; // Neolith // Neolith
    const byte F_PUSH_ARRAY = 16; // Neolith // Neolith
    const byte F_SIZEOF = 75; // Neolith
    const byte F_KEYS = 76; // Neolith
    const byte F_VALUES = 77; // Neolith
    const byte F_M_DELETE = 78;
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
        Console.WriteLine($"[VM] loaded {Prog.Length} bytes, {FuncAddr.Count} funcs");
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
        Console.WriteLine($"[VM] start Pc={Pc}");

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
                case F_INDEX_ASSIGN:{
                    var valSv = Pop(); var idxSv = Pop(); var contSv = Pop();
                    SValue val = ToSValue(valSv); SValue idx = ToSValue(idxSv);
                    var arr = contSv.U.Array ?? contSv.Value.Array;
                    if (arr != null)
                    {
                        int off = (int)idxSv.U.Number;
                        if (idxSv.Value.Type == SValueType.T_NUMBER) off = (int)idxSv.Value.Number;
                        if (off>=0 && off < arr.Items.Count) arr.Items[off] = val;
                        Push(valSv);
                        break;
                    }
                    var map = contSv.U.Mapping ?? contSv.Value.Mapping;
                    if (map != null) { map.Map[idx] = val; Push(valSv); break; }
                    Push(valSv);
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
                case F_LOCAL:{ byte idx = Prog[Pc++]; var lv = Locals[idx]; Push(lv); break; }
                case F_STORE_LOCAL:{ byte idx = Prog[Pc++]; var v = Pop(); Locals[idx]=v; Push(v); break; }
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
                        Console.WriteLine($"[F_RETURN main] val {num}");
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

