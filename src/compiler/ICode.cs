
using System;
using System.Buffers;
using System.Text;
using LithosNet.VM;

namespace LithosNet.Compiler;

// 1:1 from lib/lpc/program/icode.h + icode.c + generate.h
// C# 特化: ArrayPool + Span + hardening, 語義保留 A_PROGRAM=65535 max

public static class AProgramLimits
{
    public const int A_PROGRAM_MAX_SIZE = 65535; // from program.h
    public const int MAX_STRINGS = 65535;
    public const int MAX_FUNCTIONS = 65535;
    public const int MAX_VARIABLES = 65535;
}

public sealed class MemoryBlock : IDisposable
{
    private byte[] _rented;
    private int _pos;
    private bool _disposed;

    public MemoryBlock(int initialSize = 1024)
    {
        int size = Math.Max(initialSize, 256);
        size = Math.Min(size, AProgramLimits.A_PROGRAM_MAX_SIZE);
        _rented = ArrayPool<byte>.Shared.Rent(size);
        _pos = 0;
    }

    public int Position => _pos;
    public int Capacity => _rented.Length;

    public void EnsureCapacity(int additional)
    {
        if (_pos + additional <= _rented.Length) return;
        if (_pos + additional > AProgramLimits.A_PROGRAM_MAX_SIZE)
            throw new InvalidOperationException($"A_PROGRAM overflow: {_pos}+{additional} > {AProgramLimits.A_PROGRAM_MAX_SIZE}");

        int newSize = Math.Max(_rented.Length * 2, _pos + additional);
        newSize = Math.Min(newSize, AProgramLimits.A_PROGRAM_MAX_SIZE);
        var newBuf = ArrayPool<byte>.Shared.Rent(newSize);
        _rented.AsSpan(0, _pos).CopyTo(newBuf);
        ArrayPool<byte>.Shared.Return(_rented);
        _rented = newBuf;
    }

    // 1:1 ins_byte
    public void InsByte(byte b)
    {
        EnsureCapacity(1);
        _rented[_pos++] = b;
    }

    // 1:1 ins_short - little endian per neolith byte_code.h LOAD_SHORT
    public void InsShort(short s)
    {
        EnsureCapacity(2);
        _rented[_pos++] = (byte)(s & 0xFF);
        _rented[_pos++] = (byte)((s >> 8) & 0xFF);
    }

    public void InsUShort(ushort s)
    {
        EnsureCapacity(2);
        _rented[_pos++] = (byte)(s & 0xFF);
        _rented[_pos++] = (byte)((s >> 8) & 0xFF);
    }

    // 1:1 ins_number - int32 per original but we now have int64 path via F_LONG
    public void InsInt(int v)
    {
        EnsureCapacity(4);
        _rented[_pos++] = (byte)(v & 0xFF);
        _rented[_pos++] = (byte)((v >> 8) & 0xFF);
        _rented[_pos++] = (byte)((v >> 16) & 0xFF);
        _rented[_pos++] = (byte)((v >> 24) & 0xFF);
    }

    // 1:1 ins_long - int64_t per int64-design.md
    public void InsLong(long v)
    {
        EnsureCapacity(8);
        ulong uv = (ulong)v;
        for (int i = 0; i < 8; i++)
        {
            _rented[_pos++] = (byte)((uv >> (i * 8)) & 0xFF);
        }
    }

    // 1:1 ins_string index
    public void InsStringIndex(ushort idx)
    {
        InsUShort(idx);
    }

    public byte[] ToArray()
    {
        var result = new byte[_pos];
        _rented.AsSpan(0, _pos).CopyTo(result);
        return result;
    }

    public ReadOnlySpan<byte> AsSpan() => _rented.AsSpan(0, _pos);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_rented != null)
        {
            ArrayPool<byte>.Shared.Return(_rented);
            _rented = null;
        }
    }
}

// 1:1 from generate.h - function and variable tables with Inherit offsets
public sealed class CodeGenContext
{
    public MemoryBlock CodeBlock { get; } = new MemoryBlock(1024);
    public List<string> Strings { get; } = new();
    public List<CompilerFunction> Functions { get; } = new();
    public List<string> Variables { get; } = new();
    public List<Inherit> Inherits { get; } = new();
    public List<ushort> FileInfo { get; } = new();
    public List<byte> LineInfo { get; } = new();

    // string deduplication per SHARED_STRING / ByteSpan
    private readonly Dictionary<string, ushort> _stringMap = new(StringComparer.Ordinal);

    public ushort AddString(string s)
    {
        if (s == null) s = "";
        if (_stringMap.TryGetValue(s, out ushort idx)) return idx;
        if (Strings.Count >= AProgramLimits.MAX_STRINGS)
            throw new InvalidOperationException("Too many strings");
        idx = (ushort)Strings.Count;
        Strings.Add(s);
        _stringMap[s] = idx;
        return idx;
    }

    public ushort AddFunction(string name, ushort address)
    {
        if (Functions.Count >= AProgramLimits.MAX_FUNCTIONS)
            throw new InvalidOperationException("Too many functions");
        var f = new CompilerFunction
        {
            Name = name ?? "",
            Address = address,
            RuntimeIndex = (ushort)Functions.Count,
            Type = 0
        };
        Functions.Add(f);
        return f.RuntimeIndex;
    }

    public void PatchFunctionAddress(ushort runtimeIndex, ushort address)
    {
        if (runtimeIndex < Functions.Count)
        {
            Functions[runtimeIndex].Address = address;
        }
    }

    public byte[] GetCode() => CodeBlock.ToArray();

    public void Reset()
    {
        // for reuse per file compile
    }
}
