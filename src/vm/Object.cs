
namespace LithosNet.V4.VM;
// 1:1 from taedlar/neolith lib/lpc/object.h L20-L74

public static class ObjectFlags {
    public const ushort O_HEART_BEAT = 0x0001;
    public const ushort O_IS_WIZARD = 0x0002;
    public const ushort O_LISTENER = 0x0004; // same as O_ENABLE_COMMANDS
    public const ushort O_ENABLE_COMMANDS = 0x0004;
    public const ushort O_CLONE = 0x0008;
    public const ushort O_DESTRUCTED = 0x0010;
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

// struct sentence_s L42-L49 - carryover args for input_to/add_action per sentence-callback-args.md
public sealed class SentenceS {
    public string Verb = "";
    public SentenceS? Next;
    public ObjectS? Ob;
    public string? FunctionName; // string_or_func_t simplified
    public int Flags;
    public ArrayS? Args; // carryover arguments
}

// struct object_s L53-L74
// Note: variables[1] MUST be last per L72-L73
public sealed class ObjectS {
    public ushort Ref; // reference count
    public ushort Flags;
    public string Name = ""; // char *name, MAX_OBJECT_NAME_SIZE 2048
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
    public UserIdS? Uid;
    public UserIdS? Euid;
    // variables[1] last
    public SValueS[] Variables = new SValueS[0];

    public bool IsDestructed => (Flags & ObjectFlags.O_DESTRUCTED) != 0;
    public bool IsClone => (Flags & ObjectFlags.O_CLONE) != 0;
    public bool HasHeartBeat => (Flags & ObjectFlags.O_HEART_BEAT) != 0;

    public void AddRef(string caller) { Ref++; }
}

public sealed class InteractiveS { }
public sealed class UserIdS { }
