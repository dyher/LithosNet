
using System;
using System.Collections.Generic;

namespace LithosNet.VM;

public sealed class LpcObject
{
    public string ProgramName { get; set; }
    public Dictionary<string, int> Functions { get; } = new();
    public SValueS[] Variables { get; set; }
    public Program CurrentProgram { get; set; }
    public LpcObject(string programName = "<obj>", Program prog = null) { ProgramName = programName; CurrentProgram = prog; }
    public bool TryFindFunction(string name, out int fIdx, out int target)
    {
        fIdx = -1; target = -1;
        if (CurrentProgram?.FunctionTable != null)
        {
            for (int i=0;i<CurrentProgram.FunctionTable.Length;i++)
            {
                if (CurrentProgram.FunctionTable[i].Name == name)
                {
                    fIdx = i; target = CurrentProgram.FunctionTable[i].Address; return true;
                }
            }
        }
        if (Functions != null && Functions.TryGetValue(name, out int idx))
        {
            fIdx = idx;
            if (CurrentProgram?.FunctionTable != null && idx >=0 && idx < CurrentProgram.FunctionTable.Length)
                target = CurrentProgram.FunctionTable[idx].Address;
            return target >=0;
        }
        return false;
    }
}

public sealed class CompilerFunction
{
    public string Name;
    public ushort Address;
}

public sealed class Program
{
    public string Name;
    public byte[] Code;
    public CompilerFunction[] FunctionTable;
    public string[] Strings;
    public ushort ProgramSize { get; set; }
    public ushort NumStrings { get; set; }
    public ushort NumFunctionsDefined { get; set; }
    public ushort NumFunctionsTotal { get; set; }
}

