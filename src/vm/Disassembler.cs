
using System;
using System.Text;

namespace LithosNet.VM;

public static class OpCode
{
    public const byte F_CONST0 = 0;
    public const byte F_CONST1 = 1;
    public const byte F_CONST_1 = 2;
    public const byte F_NUMBER = 8;
    public const byte F_BYTE = 9;
    public const byte F_LONG = 10;
    public const byte F_NBYTE = 11;
    public const byte F_STRING = 12;
    public const byte F_CALL = 30;
    public const byte F_RETURN = 31;
    public const byte F_ADD = 40;
    public const byte F_SUB = 41;
    public const byte F_AND = 20;
    public const byte F_OR = 21;
    public const byte F_XOR = 22;
}

public sealed class Disassembler
{
    private static short LoadShort(byte[] code, ref int pc)
    {
        if (pc + 1 >= code.Length) return 0;
        short v = (short)(code[pc] | (code[pc + 1] << 8));
        pc += 2;
        return v;
    }

    private static int LoadInt(byte[] code, ref int pc)
    {
        if (pc + 3 >= code.Length) return 0;
        int v = code[pc] | (code[pc + 1] << 8) | (code[pc + 2] << 16) | (code[pc + 3] << 24);
        pc += 4;
        return v;
    }

    private static long LoadLong(byte[] code, ref int pc)
    {
        if (pc + 7 >= code.Length) return 0;
        long v = 0;
        for (int i = 0; i < 8; i++)
        {
            v |= ((long)code[pc + i]) << (i * 8);
        }
        pc += 8;
        return v;
    }

    private static string EscapeString(string s)
    {
        if (s == null) return "\"\"";
        var sb = new StringBuilder();
        sb.Append('"');
        foreach (char ch in s)
        {
            if (ch == '\n') sb.Append("\\n");
            else if (ch == '\r') sb.Append("\\r");
            else if (ch == '\t') sb.Append("\\t");
            else if (ch == '\\') sb.Append("\\\\");
            else if (ch == '"') sb.Append("\\\"");
            else if (ch == '\0') sb.Append("\\0");
            else if (char.IsControl(ch)) sb.Append("\\x").Append(((int)ch).ToString("X2"));
            else sb.Append(ch);
        }
        sb.Append('"');
        return sb.ToString();
    }

    public static string Disassemble(Program prog)
    {
        if (prog == null) return "// null program";
        var sb = new StringBuilder();
        sb.AppendLine("// LithosNet Disassembly - driver_id 0x" + Program.DRIVER_ID.ToString("X8"));
        sb.AppendLine("// Program: " + (prog.Name ?? "<unnamed>"));
        sb.AppendLine("// Magic: " + Program.MAGIC);
        sb.AppendLine("// Code size: " + (prog.Code?.Length ?? 0));
        sb.AppendLine();

        // functions
        sb.AppendLine("Functions: " + prog.FunctionTable.Length);
        for (int i = 0; i < prog.FunctionTable.Length; i++)
        {
            var f = prog.FunctionTable[i];
            sb.AppendLine($"  [{i}] {f.Name} addr={f.Address} type={f.Type} runtimeIdx={f.RuntimeIndex}");
        }
        sb.AppendLine();

        // strings
        sb.AppendLine("Strings: " + prog.Strings.Length);
        for (int i = 0; i < prog.Strings.Length; i++)
        {
            sb.AppendLine($"  [{i}] {EscapeString(prog.Strings[i])} len={prog.Strings[i]?.Length ?? 0}");
        }
        sb.AppendLine();

        // variables
        sb.AppendLine("Variables: " + prog.VariableTable.Length);
        for (int i = 0; i < prog.VariableTable.Length; i++)
        {
            sb.AppendLine($"  [{i}] {prog.VariableTable[i]}");
        }
        sb.AppendLine();

        // inherits
        sb.AppendLine("Inherits: " + prog.Inherits.Length);
        for (int i = 0; i < prog.Inherits.Length; i++)
        {
            var inh = prog.Inherits[i];
            sb.AppendLine($"  [{i}] prog={(inh.Prog?.Name ?? "null")} funcOff={inh.FunctionIndexOffset} varOff={inh.VariableIndexOffset}");
        }
        sb.AppendLine();

        // code listing
        sb.AppendLine("Code:");
        var code = prog.Code;
        if (code == null || code.Length == 0)
        {
            sb.AppendLine("  <empty>");
            return sb.ToString();
        }

        int pc = 0;
        while (pc < code.Length)
        {
            int startPc = pc;
            byte op = code[pc++];
            string line = startPc.ToString("D4") + ": ";
            switch (op)
            {
                case OpCode.F_NUMBER:
                    {
                        int v = LoadInt(code, ref pc);
                        line += "F_NUMBER " + v.ToString();
                        break;
                    }
                case OpCode.F_LONG:
                    {
                        long v = LoadLong(code, ref pc);
                        line += "F_LONG " + v.ToString();
                        break;
                    }
                case OpCode.F_BYTE:
                    {
                        if (pc < code.Length)
                        {
                            sbyte b = (sbyte)code[pc++];
                            line += "F_BYTE " + b.ToString();
                        }
                        else
                        {
                            line += "F_BYTE <truncated>";
                        }
                        break;
                    }
                case OpCode.F_NBYTE:
                    {
                        if (pc < code.Length)
                        {
                            sbyte b = (sbyte)code[pc++];
                            line += "F_NBYTE " + b.ToString();
                        }
                        else
                        {
                            line += "F_NBYTE <truncated>";
                        }
                        break;
                    }
                case OpCode.F_STRING:
                    {
                        short idx = LoadShort(code, ref pc);
                        string str = (idx >= 0 && idx < prog.Strings.Length) ? prog.Strings[idx] : "<invalid>";
                        line += "F_STRING " + idx.ToString() + " " + EscapeString(str);
                        break;
                    }
                case OpCode.F_AND:
                    line += "F_AND";
                    break;
                case OpCode.F_OR:
                    line += "F_OR";
                    break;
                case OpCode.F_XOR:
                    line += "F_XOR";
                    break;
                case OpCode.F_ADD:
                    line += "F_ADD";
                    break;
                case OpCode.F_SUB:
                    line += "F_SUB";
                    break;
                case OpCode.F_CALL:
                    {
                        short funcIdx = LoadShort(code, ref pc);
                        line += "F_CALL " + funcIdx.ToString();
                        break;
                    }
                case OpCode.F_RETURN:
                    line += "F_RETURN";
                    break;
                default:
                    line += "OP_" + op.ToString() + " (0x" + op.ToString("X2") + ")";
                    break;
            }
            sb.AppendLine("  " + line);
        }

        return sb.ToString();
    }

    public static string DisassembleFunction(Program prog, int funcIndex)
    {
        if (prog == null) return "// null";
        if (funcIndex < 0 || funcIndex >= prog.FunctionTable.Length) return "// invalid func index";
        // For simplicity, disassemble whole program and filter by function address range
        // Real neolith disassemble.c tracks function boundaries via line info
        return Disassemble(prog);
    }
}
