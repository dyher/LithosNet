using System;
using System.Collections.Generic;
using System.Globalization;

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

public sealed class InterpreterP9
{
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
    const byte F_ASSIGN_LVALUE = 80; // alias for add_eq slot reused? keep for lvalue assign handling

    const byte F_SIZEOF = 200;
    const byte F_KEYS = 201;
    const byte F_VALUES = 202;
    const byte F_M_DELETE = 203;
    const byte F_MAP_ADD = 204;

    SValueS[] Locals = new SValueS[64];
    Stack<CallFrame> CallStack = new();
    Dictionary<int,int> FuncAddr = new();
    LpcObject CurrentObject = null;
    Dictionary<string, SValueS> Globals = new();

    public InterpreterP9()
    {
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
    void PushMapping(SValueMapping map){ var sv = SValue.NewMapping(map); var s = new SValueS(); s.Value = sv; s.U.Mapping = map; Push(s); }
    void PushArray(SValueArray arr){ var sv = SValue.NewArray(arr); var s = new SValueS(); s.Value = sv; s.U.Array = arr; Push(s); }
    void PushClosure(int funcIdx){ var sv = SValue.NewFunction(funcIdx); var s = new SValueS(); s.Value = sv; s.U.Number = funcIdx; Push(s); }
    void PushObject(LpcObject ob){ var sv = SValue.NewObject(ob); var s = new SValueS(); s.Value = sv; s.U.Object = ob as object; Push(s); }
    SValueS Pop(){ if(Sp<0) throw new Exception($"Pop empty at Pc={Pc-1}"); var v=Stack[Sp]; Stack[Sp]=SValueS.Invalid; Sp--; return v; }
    SValueS Peek(int depth=0){ int idx=Sp-depth; if(idx<0) return SValueS.Invalid; return Stack[idx]; }

    bool IsNil(SValueS sv){ return sv.U.Number==255 || sv.U.Number==-1 || sv.Value.Type==SValueType.T_INVALID; }
    bool IsTrue(SValueS sv)
    {
        if(sv.Value.Type==SValueType.T_NUMBER) return sv.Value.Number!=0;
        if(sv.U.Number!=0 && sv.U.Number!=255 && sv.U.Number!=-1) return true;
        if(sv.U.String!=null) return sv.U.String.Length!=0;
        if(sv.Value.String!=null) return sv.Value.String.Length!=0;
        if(sv.U.Array!=null) return sv.U.Array.Items.Count!=0;
        if(sv.Value.Array!=null) return sv.Value.Array.Items.Count!=0;
        if(sv.U.Mapping!=null) return sv.U.Mapping.Map.Count!=0;
        if(sv.Value.Mapping!=null) return sv.Value.Mapping.Map.Count!=0;
        return false;
    }

    void AssignLValueChain(LValueDesc lvd, SValueS val)
    {
        if (lvd==null) return;
        var chain = new List<LValueDesc>();
        var cur=lvd;
        while(cur!=null){ chain.Add(cur); cur=cur.Parent; }
        chain.Reverse();
        if(chain.Count==0 || chain[0].Kind!=LValueKind.Local) return;
        int localIdx = chain[0].LocalIdx;
        if(localIdx<0 || localIdx>=Locals.Length) return;
        var rootSv = Locals[localIdx];

        // if chain is only local => direct assign
        if(chain.Count==1)
        {
            Locals[localIdx]=val;
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
                            Locals[localIdx]=new SValueS(){ Value=SValue.NewArray(newArr), U=new SValueU(){ Array=newArr } };
                            if(!isLast) continue;
                            return;
                        }
                        Locals[localIdx]=new SValueS(){ Value=SValue.NewArray(newArr), U=new SValueU(){ Array=newArr } };
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
                            Locals[localIdx]=new SValueS(){ Value=SValue.NewMapping(newMap), U=new SValueU(){ Mapping=newMap } };
                            continue;
                        }
                        Locals[localIdx]=new SValueS(){ Value=SValue.NewMapping(newMap), U=new SValueU(){ Mapping=newMap } };
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
                    Locals[localIdx]=val;
                    return;
                }
            }
        }
        // fallback
        Locals[localIdx]=val;
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

    void HandleEfun(byte efunIdx, byte nargs)
    {
        switch(efunIdx)
        {
            case 0:
            case 23: // write
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
        for(int i=0;i<64;i++) Locals[i]=SValueS.Invalid;
        CallStack.Clear();
        CurrentObject = new LpcObject("<main>", CurrentProgram);
        int mainAddr=-1;
        if(FuncAddr.ContainsKey(0)) mainAddr=FuncAddr[0];
        else if(FuncAddr.Count>=1)
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
        for(int i=0;i<64;i++) Locals[i]=SValueS.Invalid;
        CallStack.Clear();
        CurrentObject=obj ?? new LpcObject("<call>", CurrentProgram);
        Pc=addr;
        return RunLoop();
    }

    public SValueS CallApply(string funcName, SValueS[] args, LpcObject obj=null)
    {
        if(CurrentProgram==null) return SValueS.Invalid;
        if(obj!=null) CurrentObject=obj;
        int target=-1;
        if(CurrentProgram.FunctionTable!=null)
        {
            for(int i=0;i<CurrentProgram.FunctionTable.Length;i++)
                if(CurrentProgram.FunctionTable[i].Name==funcName){ target=CurrentProgram.FunctionTable[i].Address; break; }
        }
        if(target<0) return SValueS.Invalid;
        Sp=-1;
        for(int i=0;i<64;i++) Locals[i]=SValueS.Invalid;
        CallStack.Clear();
        // push args as locals? For simplicity, push onto stack and set locals 0..n-1
        for(int i=0;i<args.Length && i<64;i++) Locals[i]=args[i];
        Pc=target;
        long ret=RunLoop();
        if(Sp>=0) return Stack[Sp];
        return SValueS.FromNumber(ret);
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
                case F_EQ: { var b=Pop(); var a=Pop(); long av=a.Value.Type==SValueType.T_NUMBER? a.Value.Number : a.U.Number; long bv=b.Value.Type==SValueType.T_NUMBER? b.Value.Number : b.U.Number; if(a.Value.String!=null && b.Value.String!=null) PushNumber(a.Value.String==b.Value.String?1:0); else if(a.U.String!=null && b.U.String!=null) PushNumber(a.U.String==b.U.String?1:0); else PushNumber(av==bv?1:0); break; }
                case F_NE: { var b=Pop(); var a=Pop(); long av=a.Value.Type==SValueType.T_NUMBER? a.Value.Number : a.U.Number; long bv=b.Value.Type==SValueType.T_NUMBER? b.Value.Number : b.U.Number; if(a.Value.String!=null && b.Value.String!=null) PushNumber(a.Value.String!=b.Value.String?1:0); else if(a.U.String!=null && b.U.String!=null) PushNumber(a.U.String!=b.U.String?1:0); else PushNumber(av!=bv?1:0); break; }
                case F_LT: { var b=Pop(); var a=Pop(); long av=a.Value.Type==SValueType.T_NUMBER? a.Value.Number : a.U.Number; long bv=b.Value.Type==SValueType.T_NUMBER? b.Value.Number : b.U.Number; PushNumber(av<bv?1:0); break; }
                case F_GT: { var b=Pop(); var a=Pop(); long av=a.Value.Type==SValueType.T_NUMBER? a.Value.Number : a.U.Number; long bv=b.Value.Type==SValueType.T_NUMBER? b.Value.Number : b.U.Number; PushNumber(av>bv?1:0); break; }
                case F_LE: { var b=Pop(); var a=Pop(); long av=a.Value.Type==SValueType.T_NUMBER? a.Value.Number : a.U.Number; long bv=b.Value.Type==SValueType.T_NUMBER? b.Value.Number : b.U.Number; PushNumber(av<=bv?1:0); break; }
                case F_GE: { var b=Pop(); var a=Pop(); long av=a.Value.Type==SValueType.T_NUMBER? a.Value.Number : a.U.Number; long bv=b.Value.Type==SValueType.T_NUMBER? b.Value.Number : b.U.Number; PushNumber(av>=bv?1:0); break; }
                case F_NOT: { var a=Pop(); PushNumber(IsTrue(a)?0:1); break; }
                case F_NEGATE: { var a=Pop(); long av=a.Value.Type==SValueType.T_NUMBER? a.Value.Number : a.U.Number; PushNumber(-av); break; }
                case F_POP_VALUE: { Pop(); break; }
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
                case F_SIZEOF:
                {
                    var v=Pop();
                    int sz=0;
                    if(v.U.Array!=null) sz=v.U.Array.Items.Count;
                    else if(v.Value.Array!=null) sz=v.Value.Array.Items.Count;
                    else if(v.U.Mapping!=null) sz=v.U.Mapping.Map.Count;
                    else if(v.Value.Mapping!=null) sz=v.Value.Mapping.Map.Count;
                    else if(v.U.String!=null) sz=v.U.String.Length;
                    else if(v.Value.String!=null) sz=v.Value.String.Length;
                    PushNumber(sz);
                    break;
                }
                case F_KEYS:
                {
                    var v=Pop();
                    var map=v.U.Mapping ?? v.Value.Mapping;
                    var arr=new SValueArray();
                    if(map!=null) foreach(var kv in map.Map) arr.Items.Add(kv.Key);
                    PushArray(arr);
                    break;
                }
                case F_VALUES:
                {
                    var v=Pop();
                    var map=v.U.Mapping ?? v.Value.Mapping;
                    var arr=new SValueArray();
                    if(map!=null) foreach(var kv in map.Map) arr.Items.Add(kv.Value);
                    PushArray(arr);
                    break;
                }
                case F_M_DELETE:
                {
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
                        map.Map.Remove(key);
                        PushMapping(map);
                    }
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
                    int target = FuncAddr.ContainsKey(funcIdx)? FuncAddr[funcIdx] : -1;

                    if(target<0){ PushNumber(0); break; }
                    var frame=new CallFrame(Pc, Sp+1, CurrentObject);
                    for(int i=0;i<64;i++) frame.Locals[i]=Locals[i];
                    CallStack.Push(frame);
                    var callArgs=new SValueS[all.Count-1];
                    for(int i=1;i<all.Count;i++) callArgs[i-1]=all[i];
                    for(int i=0;i<64;i++) Locals[i]=SValueS.Invalid;
                    for(int i=0;i<callArgs.Length && i<64;i++) Locals[i]=callArgs[i];

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
                    // logical or/and short-circuit already handled by compiler via branches? For simplicity evaluate
                    var b=Pop(); var a=Pop();
                    bool at=IsTrue(a); bool bt=IsTrue(b);
                    if(instr==F_LOR) PushNumber(at||bt?1:0); else PushNumber(at&&bt?1:0);
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
                case F_CALL_FUNCTION_BY_ADDRESS:
                {
                    short fIdx=LoadShort(Prog, ref Pc);
                    byte nargs=Prog[Pc++];
                    int target=FuncAddr.ContainsKey(fIdx)? FuncAddr[fIdx] : -1;
                    var frame=new CallFrame(Pc, Sp - nargs +1, CurrentObject);
                    for(int i=0;i<64;i++) frame.Locals[i]=Locals[i];
                    CallStack.Push(frame);
                    // move args into new locals 0..nargs-1
                    SValueS[] args=new SValueS[nargs];
                    for(int i=nargs-1;i>=0;i--) args[i]=Pop();
                    for(int i=0;i<64;i++) Locals[i]=SValueS.Invalid;
                    for(int i=0;i<nargs && i<64;i++) Locals[i]=args[i];
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
                        for(int i=0;i<64;i++) Locals[i]=frame.Locals[i];
                        CurrentObject=frame.CurrentObject;
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
                case F_CALL_OTHER:
                {
                    byte nargs=Prog[Pc++];
                    // stack: obj, funcName, args...
                    if(nargs<2){ for(int i=0;i<nargs;i++) Pop(); PushNumber(0); break; }
                    var all=new List<SValueS>();
                    for(int i=0;i<nargs;i++) all.Add(Stack[Sp - nargs +1 + i]);
                    Sp-=nargs;
                    var objSv=all[0];
                    var funcSv=all[1];
                    string funcName=funcSv.U.String ?? funcSv.Value.String ?? "";
                    var callArgs=new List<SValueS>();
                    for(int i=2;i<all.Count;i++) callArgs.Add(all[i]);
                    // try to resolve object: if it's LpcObject, call apply; else push 0
                    LpcObject ob=null;
                    if(objSv.U.Object is LpcObject lo) ob=lo;
                    else if(objSv.Value.Object is LpcObject lo2) ob=lo2;
                    if(ob!=null && ob.CurrentProgram!=null)
                    {
                        var res=CallApplyInObject(ob, funcName, callArgs.ToArray());
                        Push(res);
                    }
                    else
                    {
                        // fallback: if we have current program and function exists, try
                        PushNumber(0);
                    }
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

    SValueS CallApplyInObject(LpcObject obj, string funcName, SValueS[] args)
    {
        if(obj.CurrentProgram==null) return SValueS.FromNumber(0);
        int target=-1;
        for(int i=0;i<obj.CurrentProgram.FunctionTable.Length;i++)
            if(obj.CurrentProgram.FunctionTable[i].Name==funcName){ target=obj.CurrentProgram.FunctionTable[i].Address; break; }
        if(target<0) return SValueS.FromNumber(0);
        // save current state
        var savedProg=Prog;
        var savedStrings=Strings;
        var savedFuncAddr=new Dictionary<int,int>(FuncAddr);
        var savedPc=Pc;
        var savedSp=Sp;
        var savedLocals=new SValueS[64];
        Array.Copy(Locals, savedLocals, 64);
        var savedObj=CurrentObject;
        var savedCallStack=new Stack<CallFrame>(CallStack);

        Prog=obj.CurrentProgram.Code;
        Strings=obj.CurrentProgram.Strings;
        FuncAddr.Clear();
        for(int i=0;i<obj.CurrentProgram.FunctionTable.Length;i++) FuncAddr[i]=obj.CurrentProgram.FunctionTable[i].Address;
        CurrentObject=obj;
        Sp=-1;
        for(int i=0;i<64;i++) Locals[i]=SValueS.Invalid;
        for(int i=0;i<args.Length && i<64;i++) Locals[i]=args[i];
        Pc=target;
        CallStack.Clear();
        long ret=RunLoop();
        SValueS result=Sp>=0? Stack[Sp] : SValueS.FromNumber(ret);

        // restore
        Prog=savedProg;
        Strings=savedStrings;
        FuncAddr=savedFuncAddr;
        Pc=savedPc;
        Sp=savedSp;
        Array.Copy(savedLocals, Locals, 64);
        CurrentObject=savedObj;
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