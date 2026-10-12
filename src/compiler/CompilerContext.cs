
using System;

namespace LithosNet.Compiler
{
    public sealed class CompilerContext
    {
        public readonly CodeGenContext InnerContext;
        public dynamic CodeBlock => InnerContext.CodeBlock;

        public CompilerContext()
        {
            InnerContext = new CodeGenContext();
        }

        public byte[] ToProgramBytes()
        {
            return InnerContext.CodeBlock.ToArray();
        }

        public int AddString(string s) => InnerContext.AddString(s);
        public ushort AddFunction(string name, ushort addr) => InnerContext.AddFunction(name, addr);
        public void PatchFunctionAddress(ushort idx, ushort addr) => InnerContext.PatchFunctionAddress(idx, addr);

        public ushort FindFunction(string name)
        {
            for (int i = 0; i < InnerContext.Functions.Count; i++)
            {
                if (InnerContext.Functions[i].Name == name) return (ushort)i;
            }
            return ushort.MaxValue;
        }

        public void EmitNumber(long n)
        {
            var cb = InnerContext.CodeBlock;
            if (n == 0) cb.InsByte(0x01);
            else if (n == 1) cb.InsByte(0x02);
            else if (n >= -128 && n <= 127)
            {
                cb.InsByte(0x03);
                cb.InsByte((byte)(sbyte)n);
            }
            else if (n >= int.MinValue && n <= int.MaxValue)
            {
                cb.InsByte(0x04);
                cb.InsInt((int)n);
            }
            else
            {
                cb.InsByte(0x05);
                cb.InsLong(n);
            }
        }

        public void EmitAnd() => InnerContext.CodeBlock.InsByte(0x10);
        public void EmitOr() => InnerContext.CodeBlock.InsByte(0x11);
        public void EmitXor() => InnerContext.CodeBlock.InsByte(0x12);
    }
}
