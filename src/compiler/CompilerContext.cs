using System;
using System.Collections.Generic;
using LithosNet.VM;

namespace LithosNet.Compiler
{
    public sealed class CompilerContext
    {
        public readonly CodeGenContext InnerContext;
        public CodeBlock CodeBlock => InnerContext.CodeBlock;
        public CompilerContext() { InnerContext = new CodeGenContext(); }
        public int AddString(string s) => InnerContext.AddString(s);
        public ushort AddFunction(string name, ushort addr) => InnerContext.AddFunction(name, addr);
        public void PatchFunctionAddress(ushort idx, ushort addr) => InnerContext.PatchFunctionAddress(idx, addr);
        public ushort FindFunction(string name) => InnerContext.FindFunction(name);
    }

    public sealed class CodeGenContext
    {
        public CodeBlock CodeBlock { get; } = new CodeBlock();
        private readonly List<string> _strings = new();
        private readonly Dictionary<string, int> _stringMap = new();
        private readonly List<CompilerFunction> _funcs = new();
        private readonly Dictionary<string, ushort> _funcMap = new();

        public IReadOnlyList<string> Strings => _strings;
        public IReadOnlyList<CompilerFunction> Functions => _funcs;

        public int AddString(string s)
        {
            if (_stringMap.TryGetValue(s, out var idx)) return idx;
            idx = _strings.Count;
            _strings.Add(s);
            _stringMap[s] = idx;
            return idx;
        }
        public ushort AddFunction(string name, ushort addr)
        {
            if (_funcMap.TryGetValue(name, out var existing)) return existing;
            ushort idx = (ushort)_funcs.Count;
            _funcs.Add(new CompilerFunction { Name = name, Address = addr });
            _funcMap[name] = idx;
            return idx;
        }
        public void PatchFunctionAddress(ushort idx, ushort addr)
        {
            if (idx < _funcs.Count) _funcs[idx].Address = addr;
        }
        public ushort FindFunction(string name)
        {
            if (_funcMap.TryGetValue(name, out var idx)) return idx;
            return ushort.MaxValue;
        }
    }

    public sealed class CodeBlock
    {
        private byte[] _buf = new byte[65536];
        private int _pos = 0;
        public int Position => _pos;
        public void InsByte(byte b) => _buf[_pos++] = b;
        public void InsShort(short s) { _buf[_pos++] = (byte)(s & 0xFF); _buf[_pos++] = (byte)(s >> 8); }
        public void InsShort(ushort s) { _buf[_pos++] = (byte)(s & 0xFF); _buf[_pos++] = (byte)(s >> 8); }
        public void InsInt(int i) { var b = BitConverter.GetBytes(i); Buffer.BlockCopy(b, 0, _buf, _pos, 4); _pos += 4; }
        public void InsLong(long l) { var b = BitConverter.GetBytes(l); Buffer.BlockCopy(b, 0, _buf, _pos, 8); _pos += 8; }
        public byte[] ToArray() { var r = new byte[_pos]; Buffer.BlockCopy(_buf, 0, r, 0, _pos); return r; }
    }
}
