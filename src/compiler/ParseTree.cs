using System;
using System.Collections.Generic;

namespace LithosNet.Compiler;

public enum NodeKind
{
    NODE_NUMBER,
    NODE_REAL,
    NODE_STRING,
    NODE_IDENTIFIER,
    NODE_CALL,
    NODE_DOT_CALL,
    NODE_ARRAY,
    NODE_MAPPING,
    NODE_IF,
    NODE_LOOP,
    NODE_RETURN,
    NODE_ASSIGN,
    NODE_BINARY_OP,
    NODE_UNARY_OP,
    NODE_FUNCTION_DEF,
    NODE_VAR_DEF,
    NODE_CLASS,
    NODE_INHERIT,
    NODE_INCLUDE,
    NODE_CLOSURE
}

[Flags]
public enum TypeFlags : ushort
{
    TYPE_UNKNOWN = 0,
    TYPE_NUMBER = 0x1,
    TYPE_STRING = 0x2,
    TYPE_OBJECT = 0x4,
    TYPE_MAPPING = 0x8,
    TYPE_FUNCTION = 0x10,
    TYPE_ANY = 0xFF,
    TYPE_MOD_ARRAY = 0x0020,
    TYPE_MOD_CLASS = 0x0040
}

public sealed class ParseNode
{
    public NodeKind Kind;
    public TypeFlags Type;
    public long NumberValue;
    public double RealValue;
    public string StringValue = "";
    public int StringLen;
    public string Name = "";
    public int Line;
    public int Column;
    public List<ParseNode> Children = new();
    public ParseNode Left;
    public ParseNode Right;
    public ParseNode Next;

    public static ParseNode NewNumber(long val, int line = 0)
    {
        return new ParseNode { Kind = NodeKind.NODE_NUMBER, NumberValue = val, Line = line, Type = TypeFlags.TYPE_NUMBER };
    }

    public static ParseNode NewReal(double val, int line = 0)
    {
        return new ParseNode { Kind = NodeKind.NODE_REAL, RealValue = val, Line = line };
    }

    public static ParseNode NewString(string s, int len, int line = 0)
    {
        return new ParseNode { Kind = NodeKind.NODE_STRING, StringValue = s ?? "", StringLen = len, Line = line, Type = TypeFlags.TYPE_STRING };
    }

    public static ParseNode NewIdentifier(string name, int line = 0)
    {
        return new ParseNode { Kind = NodeKind.NODE_IDENTIFIER, Name = name ?? "", StringValue = name ?? "", Line = line };
    }

    public static ParseNode NewDotCall(ParseNode obj, string method, List<ParseNode> args, int line = 0)
    {
        var node = new ParseNode { Kind = NodeKind.NODE_DOT_CALL, Name = method ?? "", Line = line };
        node.Children.Add(obj);
        if (args != null) node.Children.AddRange(args);
        return node;
    }

    public void AddChild(ParseNode child)
    {
        if (child != null) Children.Add(child);
    }

    public override string ToString()
    {
        string firstChildName = Children.Count > 0 ? Children[0].Name : "?";
        if (Kind == NodeKind.NODE_NUMBER) return "NUMBER(" + NumberValue + ")@" + Line;
        if (Kind == NodeKind.NODE_STRING) return "STRING(len=" + StringLen + ")@" + Line;
        if (Kind == NodeKind.NODE_IDENTIFIER) return "ID(" + Name + ")@" + Line;
        if (Kind == NodeKind.NODE_DOT_CALL) return "DOT_CALL(" + firstChildName + "->" + Name + ")@" + Line;
        if (Kind == NodeKind.NODE_CLOSURE) return "CLOSURE@" + Line + " children=" + Children.Count;
        return Kind + "@" + Line + " children=" + Children.Count;
    }
}

public sealed class ParseFunction
{
    public string Name = "";
    public TypeFlags ReturnType;
    public List<ParseNode> Args = new();
    public List<ParseNode> Locals = new();
    public ParseNode Body;
    public ushort Flags;
    public int Line;
    public bool IsVarArgs;
}

public sealed class ParseClass
{
    public string Name = "";
    public List<ParseNode> Members = new();
    public int Line;
}
