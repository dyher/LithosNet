
namespace LithosNet.V4.VM;
// 1:1 from taedlar/neolith src/interpret.h L7-L150

public static class PushFlags {
    public const int PUSH_STRING = (0 << 6);
    public const int PUSH_NUMBER = (1 << 6);
    public const int PUSH_GLOBAL = (2 << 6);
    public const int PUSH_LOCAL = (3 << 6);
    public const int PUSH_WHAT = (3 << 6);
    public const int PUSH_MASK = (0xff ^ PUSH_WHAT);
}

public enum FrameKind {
    FRAME_FUNCTION = 0,
    FRAME_FUNP = 1,
    FRAME_CATCH = 2,
    FRAME_FAKE = 3,
    FRAME_MASK = 3,
    FRAME_OB_CHANGE = 4,
    FRAME_EXTERNAL = 8
}

// control_stack_s L34-L49
public sealed class ControlStackS {
    public int FrameKind;
    public int TableIndex; // union with funp
    public ObjectS? Ob;
    public ObjectS? PrevOb;
    public ProgramS? Prog;
    public int NumLocalVariables;
    public int Pc; // program counter offset, char *pc in C
    public int Fp; // frame pointer index into stack
    public int FunctionIndexOffset;
    public int VariableIndexOffset;
    public int CallerType;
}

public sealed class FunctionToCallS {
    public ObjectS? Ob;
    public string? Str; // or funptr
    public int NArg;
    public SValueS[]? Args;
}

public static class ErrorState {
    public const int ES_STACK_FULL = 1 << 0;
    public const int ES_MAX_EVAL_COST = 1 << 1;
}

// interpreter stack from interpret.h L65-L82
public sealed class Interpreter {
    public const int STACK_SIZE = 4096;
    public SValueS[] Stack = new SValueS[STACK_SIZE];
    public int Sp = -1; // stack pointer, svalue_t *sp
    public ControlStackS[] ControlStack = new ControlStackS[256];
    public int Csp = -1;
    public ProgramS? CurrentProg;
    public int CallerType;
    public SValueS Const0 = SValueS.FromNumber(0);
    public SValueS Const1 = SValueS.FromNumber(1);

    public Interpreter(){
        for(int i=0;i<STACK_SIZE;i++) Stack[i]=SValueS.Invalid;
    }

    public void PushNumber(long n){
        Stack[++Sp] = SValueS.FromNumber(n);
    }
    public void PushObject(ObjectS ob){
        if((ob.Flags & ObjectFlags.O_DESTRUCTED)!=0) PushNumber(0);
        else Stack[++Sp] = SValueS.FromObject(ob);
    }
    public SValueS Pop(){ return Stack[Sp--]; }

    // eval_instruction(const char *p) from interpret.h L85
    public void EvalInstruction(ProgramS prog, int pc){
        // TODO Phase2: bytecode dispatch loop per src/interpret.c F_NUMBER/F_LONG/F_BRANCH etc.
    }

    public void CallFunction(ProgramS progp, int runtimeIndex, int numArgs, SValueS ret){
        // call_function from interpret.h L87
        var entry = progp.FindFuncEntry(runtimeIndex);
        // TODO
    }
}
