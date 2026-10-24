using System;
using System.Collections.Generic;

namespace LithosNet.VM
{
    // Neolith 1:1 Program structure with A_* areas from compiler.h
    public sealed class Program
    {
        public string Name { get; set; } = "";
        public byte[] Code { get; set; } = Array.Empty<byte>();
        public List<FunctionInfo> Functions { get; } = new();
        public List<string> Strings { get; } = new();
        public List<string> VariableNames { get; } = new();
        public byte[] LineInfo { get; set; } = Array.Empty<byte>();
        public List<InheritInfo> Inherits { get; } = new();
        public List<ClassDef> Classes { get; } = new();
        
        // A_* areas tracking (from compiler.h)
        public const int A_PROGRAM = 0;
        public const int A_RUNTIME_FUNCTIONS = 1;
        public const int A_COMPILER_FUNCTIONS = 2;
        public const int A_RUNTIME_COMPRESSED = 3;
        public const int A_FUNCTION_FLAGS = 4;
        public const int A_STRINGS = 5;
        public const int A_VAR_NAME = 6;
        public const int A_VAR_TYPE = 7;
        public const int A_LINENUMBERS = 8;
        public const int A_FILE_INFO = 9;
        public const int A_INHERITS = 10;
        public const int A_CLASS_DEF = 11;
        public const int A_CLASS_MEMBER = 12;
        public const int A_ARGUMENT_TYPES = 13;
        public const int A_ARGUMENT_INDEX = 14;
        public const int NUMPAREAS = 15;
        public const int NUMAREAS_FULL = 24;

        public int ProgramSize { get; set; }
        public int NumFunctions => Functions.Count;
        public int NumStrings => Strings.Count;
        public int HeartBeat { get; set; } = -1;
        public ulong ConfigId { get; set; }

        public sealed class FunctionInfo
        {
            public string Name { get; set; } = "";
            public int Address { get; set; }
            public byte NumArgs { get; set; }
            public byte NumLocals { get; set; }
            public int RuntimeIndex { get; set; }
            public ushort Flags { get; set; }
        }

        public sealed class InheritInfo
        {
            public Program Prog { get; set; }
            public int FunctionIndexOffset { get; set; }
            public int VariableIndexOffset { get; set; }
        }

        public sealed class ClassDef
        {
            public string Name { get; set; } = "";
            public int Size { get; set; }
            public int Index { get; set; }
        }

        public bool TryFindFunction(string name, out FunctionInfo info)
        {
            foreach (var f in Functions)
            {
                if (f.Name == name) { info = f; return true; }
            }
            info = null;
            return false;
        }

        public FunctionInfo FindMain()
        {
            if (TryFindFunction("main", out var main)) return main;
            // Fallback: last non-closure function
            for (int i = Functions.Count-1; i>=0; i--)
            {
                var f = Functions[i];
                if (!f.Name.StartsWith("__closure")) return f;
            }
            return Functions.Count>0 ? Functions[0] : null;
        }
    }
}
