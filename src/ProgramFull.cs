using System;
using System.Collections.Generic;

namespace LithosNet.V4.VM;

// 1:1 translation of neolith-1.0.0-alpha.10 lib/lpc/program.c
// Original C path: neolith-1.0.0-alpha.10/lib/lpc/program.c
// Also covers lib/lpc/program.h constants
// Driver: DRIVER_ID = 0x20260602 per lib/lpc/program/binaries.h
// svalue_u.number is int64_t => long in C#
// T_NUMBER = 0x2 per phase spec, O_DESTRUCTED = 0x10 per object.h
// This file keeps original function names and logic 1:1, with line refs.
//
// External deps from C (free_string, FREE, free_prog of inherit) are stubbed
// where VM integration would be more complex, but signature stays true.

public static class ProgramFull
{
    // From program.h / binaries.h
    public const uint DRIVER_ID = 0x20260602; // 0x20260602
    public const ushort T_NUMBER = 0x2;
    public const ushort O_DESTRUCTED = 0x10;

    // Globals mirroring program.c L8: total_num_prog_blocks, total_prog_block_size
    // [program.c:L8]
    public static ulong TotalNumProgBlocks = 0;
    public static long TotalProgBlockSize = 0;

    // compressed_offset_table_t mirror for COMPRESS_FUNCTION_TABLES path
    // program.h L121-L128
    public sealed class CompressedOffsetTable
    {
        public ushort FirstDefined;
        public ushort FirstOverload;
        public ushort NumCompressed;
        public ushort NumDeleted;
        public byte[] Index = Array.Empty<byte>(); // index[1] variable length in C, 255 = absent
    }

    // Extended Program extensions needed for full translation
    // If Program.Compressed is null, we are in non-compressed mode.
    // We store it in a side dictionary to avoid modifying sealed Program.cs directly,
    // but also we support Program having property via dynamic check.
    private static readonly Dictionary<Program, CompressedOffsetTable> CompressedMap = new();

    public static void SetCompressedTable(Program prog, CompressedOffsetTable tbl)
    {
        CompressedMap[prog] = tbl;
    }

    public static CompressedOffsetTable? GetCompressedTable(Program prog)
    {
        CompressedMap.TryGetValue(prog, out var tbl);
        return tbl;
    }

    // [program.c:L10-L13] reference_prog
    // void reference_prog(program_t *progp, const char *from)
    public static void reference_prog(Program progp, string from)
    {
        // [program.c:L11] (void) from; /* unused */
        // [program.c:L12] progp->ref++;
        if (progp == null) return;
        progp.Ref++;
        // Console.WriteLine($"[src/program.c:L10] reference_prog from={from} ref={progp.Ref}");
    }

    // [program.c:L15-L40] deallocate_program
    // void deallocate_program(program_t *progp)
    public static void deallocate_program(Program progp)
    {
        // [program.c:L17-L19]
        // total_prog_block_size -= progp->total_size;
        // total_num_prog_blocks--;
        if (progp == null) return;
        TotalProgBlockSize -= progp.TotalSize;
        if (TotalNumProgBlocks > 0) TotalNumProgBlocks--;

        // [program.c:L22-L24] Free all function names.
        // for (i = 0; i < num_functions_defined; i++) free_string(function_table[i].name)
        // In C# strings are GC, but keep loop for 1:1 logic + stub
        for (int i = 0; i < progp.NumFunctionsDefined; i++)
        {
            // [program.c:L23] if (progp->function_table[i].name) free_string(...)
            if (i < progp.FunctionTable.Length)
            {
                var fname = progp.FunctionTable[i].Name;
                if (!string.IsNullOrEmpty(fname))
                {
                    // [program.c:L24] free_string(to_shared_str(...))
                    // stub: would free shared string
                    // Console.WriteLine($"[src/program.c:L24] free_string func {fname}");
                }
            }
        }

        // [program.c:L25-L27] Free all strings
        for (int i = 0; i < progp.NumStrings; i++)
        {
            // [program.c:L26-L27]
            if (i < progp.Strings.Length)
            {
                // free_string
            }
        }

        // [program.c:L28-L30] Free all variable names
        for (int i = 0; i < progp.NumVariablesDefined; i++)
        {
            if (i < progp.VariableTable.Length)
            {
                // free_string(variable_table[i])
            }
        }

        // [program.c:L31-L33] Free all inherited objects
        // for (i = 0; i < num_inherited; i++) free_prog(inherit[i].prog, 1);
        for (int i = 0; i < progp.NumInherited; i++)
        {
            if (i < progp.Inherits.Length)
            {
                var inhProg = progp.Inherits[i].Prog;
                if (inhProg != null)
                {
                    // [program.c:L33] recursive free
                    free_prog(inhProg, 1);
                }
            }
        }

        // [program.c:L34] free_string(progp->name)
        // stub

        // [program.c:L36-L37] if (file_info) FREE(file_info)
        if (progp.FileInfo != null)
        {
            progp.FileInfo = null;
        }

        // [program.c:L39] FREE((char*)progp)
        // In C# GC frees Program object; we zero fields for 1:1 intent
        progp.Code = Array.Empty<byte>();
        progp.FunctionTable = Array.Empty<CompilerFunction>();
        progp.FunctionFlags = Array.Empty<ushort>();
        progp.FunctionOffsets = Array.Empty<RuntimeFunction>();
        progp.Strings = Array.Empty<string>();
        progp.VariableTable = Array.Empty<string>();
        progp.Inherits = Array.Empty<Inherit>();
        // Console.WriteLine($"[src/program.c:L15] deallocate_program {progp.Name}");
    }

    // [program.c:L49-L64] free_prog
    // void free_prog(program_t *progp, int free_sub_strings)
    public static void free_prog(Program progp, int free_sub_strings)
    {
        // [program.c:L50] progp->ref--;
        if (progp == null) return;
        if (progp.Ref > 0) progp.Ref--;

        // [program.c:L51-L52] if (ref>0) return
        if (progp.Ref > 0)
        {
            return;
        }

        // [program.c:L53-L54] if (func_ref>0) return
        if (progp.FuncRef > 0)
        {
            return;
        }

        // [program.c:L56-L57] if (free_sub_strings) deallocate_program(progp);
        if (free_sub_strings != 0)
        {
            // [program.c:L57]
            deallocate_program(progp);
        }
        else
        {
            // [program.c:L58-L63] else branch (swap case)
            // total_prog_block_size -= total_size; total_num_prog_blocks--; FREE(progp)
            TotalProgBlockSize -= progp.TotalSize;
            if (TotalNumProgBlocks > 0) TotalNumProgBlocks--;
            // FREE((char*)progp) -> GC path, we clear heavy fields
            Console.WriteLine($"[src/program.c:L58-L63] free_prog swap-free without sub strings: {progp.Name}");
            progp.Code = Array.Empty<byte>();
            // In real VM swap logic would keep string table to reload later; here stub
        }
    }

    // [program.c:L66-L82] variable_name
    // shared_str_t variable_name(const program_t *prog, int idx)
    public static string variable_name(Program prog, int idx)
    {
        // [program.c:L67-L68] int i = num_inherited -1; int first;
        int i = prog.NumInherited - 1;
        int first;

        // [program.c:L70-L75] if (i>-1) compute first, else return variable_table[idx]
        if (i > -1)
        {
            // prog->inherit[i].variable_index_offset + inherit[i].prog->num_variables_total
            var inh = prog.Inherits[i];
            int progTotal = inh.Prog != null ? inh.Prog.NumVariablesTotal : 0;
            first = inh.VariableIndexOffset + progTotal;
        }
        else
        {
            // [program.c:L75] return prog->variable_table[idx];
            if (idx >= 0 && idx < prog.VariableTable.Length)
                return prog.VariableTable[idx];
            return $"<invalid var {idx}>";
        }

        // [program.c:L76-L77] if (idx >= first) return variable_table[idx-first]
        if (idx >= first)
        {
            int localIdx = idx - first;
            if (localIdx >= 0 && localIdx < prog.VariableTable.Length)
                return prog.VariableTable[localIdx];
            return $"<invalid local var {localIdx}>";
        }

        // [program.c:L78-L79] while (idx < inherit[i].variable_index_offset) i--;
        while (i >= 0 && idx < prog.Inherits[i].VariableIndexOffset)
        {
            i--;
        }

        // [program.c:L80-L81] return variable_name(inherit[i].prog, idx - offset)
        if (i >= 0)
        {
            var inhProg = prog.Inherits[i].Prog;
            if (inhProg != null)
            {
                int adj = idx - prog.Inherits[i].VariableIndexOffset;
                // recursive - matches C recursion
                return variable_name(inhProg, adj);
            }
        }

        // fallback
        Console.WriteLine($"[src/program.c:L66] variable_name fallback idx={idx}");
        return $"<var {idx}>";
    }

    // [program.c:L84-L95] function_name
    // shared_str_t function_name(const program_t *prog, int index)
    public static string function_name(Program prog, int index)
    {
        // [program.c:L85] runtime_function_u *func_entry = FIND_FUNC_ENTRY(prog, index)
        var funcEntry = FIND_FUNC_ENTRY(prog, index);
        if (funcEntry == null)
        {
            return $"<invalid func {index}>";
        }

        var curProg = prog;
        var curIndex = index;
        var curEntry = funcEntry;

        // [program.c:L87-L92] while (prog->function_flags[index] & NAME_INHERITED)
        while (true)
        {
            if (curIndex < 0 || curIndex >= curProg.FunctionFlags.Length)
                break;
            ushort flags = curProg.FunctionFlags[curIndex];
            if ((flags & NameFlags.NAME_INHERITED) == 0)
                break;

            // [program.c:L89-L90] prog = inherit[func_entry->inh.offset].prog; index = inh.index
            if (curEntry == null) break;
            int inhOffset = curEntry.Value.Inh.Offset;
            if (inhOffset < 0 || inhOffset >= curProg.Inherits.Length) break;
            var nextProg = curProg.Inherits[inhOffset].Prog;
            if (nextProg == null) break;
            int nextIndex = curEntry.Value.Inh.Index;

            curProg = nextProg;
            curIndex = nextIndex;
            curEntry = FIND_FUNC_ENTRY(curProg, curIndex);
            if (curEntry == null) break;
        }

        // [program.c:L94] return prog->function_table[func_entry->def.f_index].name
        if (curEntry == null) return $"<func {index}>";
        int fIndex = curEntry.Value.Def.FIndex;
        if (fIndex >= 0 && fIndex < curProg.FunctionTable.Length)
        {
            return curProg.FunctionTable[fIndex].Name;
        }
        return $"<func idx {fIndex}>";
    }

    // [program.c:L109-L144] find_func_entry - only when COMPRESS_FUNCTION_TABLES defined
    // runtime_function_u *find_func_entry(const program_t *prog, int index)
    public static RuntimeFunction? find_func_entry(Program prog, int index)
    {
        // This mimics the C static return value pattern [program.c:L111] static runtime_function_u ret;
        // In C# we avoid static mutable return; but keep logic 1:1
        var comp = GetCompressedTable(prog);
        if (comp == null)
        {
            // Fallback to direct lookup when no compressed table - should not happen in compressed mode,
            // but keep true logic for C# translation.
            if (index >= 0 && index < prog.FunctionOffsets.Length)
                return prog.FunctionOffsets[index];
            return null;
        }

        // [program.c:L113-L114]
        // int f_ov = first_overload; int n_ov = first_defined - num_compressed
        int f_ov = comp.FirstOverload;
        int n_ov = comp.FirstDefined - comp.NumCompressed;
        int idx;
        int fidx;

        // [program.c:L123-L125] condition: (index < f_ov) || (idx=index-f_ov)>=n_ov || index[idx]==255
        bool omitted = false;
        if (index < f_ov)
        {
            omitted = true;
        }
        else
        {
            idx = index - f_ov;
            if (idx >= n_ov)
            {
                omitted = true;
            }
            else
            {
                if (idx < comp.Index.Length)
                {
                    fidx = comp.Index[idx];
                    if (fidx == 255)
                        omitted = true;
                    else
                        return prog.FunctionOffsets[fidx]; // [program.c:L142]
                }
                else
                {
                    omitted = true;
                }
            }
        }

        if (omitted)
        {
            // [program.c:L126-L135] binary search inheritance to remake entry
            int first = 0;
            int last = prog.NumInherited - 1;

            // [program.c:L128-L135] while(last>first) { mid = (last+first+1)/2; if(inherit[mid].function_index_offset > index) last=mid-1 else first=mid }
            while (last > first)
            {
                int mid = (last + first + 1) / 2;
                if (mid < 0 || mid >= prog.Inherits.Length) break;
                if (prog.Inherits[mid].FunctionIndexOffset > index)
                    last = mid - 1;
                else
                    first = mid;
            }

            // [program.c:L136-L137] ret.inh.offset = first; ret.inh.index = index - inherit[first].function_index_offset
            var ret = new RuntimeFunction
            {
                IsDefined = false,
                Inh = new RuntimeInherited
                {
                    Offset = (ushort)first,
                    Index = (ushort)(index - (first >= 0 && first < prog.Inherits.Length ? prog.Inherits[first].FunctionIndexOffset : 0))
                }
            };
            // [program.c:L138] return &ret
            Console.WriteLine($"[src/program.c:L126-L138] find_func_entry remade omitted index={index} -> inh offset={ret.Inh.Offset} idx={ret.Inh.Index}");
            return ret;
        }
        else
        {
            // unreachable due to early return above, but keep for 1:1
            return null;
        }
    }

    // Helper macro FIND_FUNC_ENTRY / FUNC_ENTRY from program.h L260-L268
    // #define FUNC_ENTRY(p,i) ((p)->function_offsets + (i))
    // #define FIND_FUNC_ENTRY(p,i) ...compressed check...
    public static RuntimeFunction? FUNC_ENTRY(Program prog, int i)
    {
        // [program.h:L260]
        if (i < 0 || i >= prog.FunctionOffsets.Length) return null;
        return prog.FunctionOffsets[i];
    }

    public static RuntimeFunction? FIND_FUNC_ENTRY(Program prog, int i)
    {
        // [program.h:L263-L268]
        var comp = GetCompressedTable(prog);
        if (comp == null)
        {
            // non-compressed path
            return FUNC_ENTRY(prog, i);
        }

        // if (i < first_defined) find_func_entry else FUNC_ENTRY(i - num_deleted)
        if (i < comp.FirstDefined)
        {
            return find_func_entry(prog, i);
        }
        else
        {
            int adjusted = i - comp.NumDeleted;
            return FUNC_ENTRY(prog, adjusted);
        }
    }

    // [program.c:L147-L174] translate_absolute_line
    // int translate_absolute_line(int abs_line, unsigned short *file_info, size_t block_size, int *ret_file, int *ret_line)
    public static int translate_absolute_line(int abs_line, ushort[] file_info, int block_size, out int ret_file, out int ret_line)
    {
        // [program.c:L148] unsigned short *p1,*p2,*end = file_info + (block_size/sizeof(ushort))
        // In C# arrays are ushort[]; block_size is in bytes, convert to count = block_size/2
        ret_file = -1;
        ret_line = -1;

        if (file_info == null || file_info.Length == 0)
        {
            Console.WriteLine($"[src/program.c:L147] translate_absolute_line true logic - null file_info");
            return -1;
        }

        int count = block_size / sizeof(ushort);
        // clamp to actual array length for safety
        count = Math.Min(count, file_info.Length);
        int end = count; // exclusive

        int line_tmp = abs_line;
        // [program.c:L152-L160] two passes: first find file
        // p1 = file_info; while(line_tmp > *p1) { line_tmp -= *p1; p1+=2; if(p1>=end) return -1 }
        int p1 = 0;
        while (true)
        {
            if (p1 >= end)
            {
                // [program.c:L158-L159] return -1 file info block corrupted
                return -1;
            }
            ushort curLen = file_info[p1];
            // [program.c:L154] while(line_tmp > *p1)
            if (line_tmp <= curLen)
                break;
            line_tmp -= curLen;
            p1 += 2;
            if (p1 >= end)
            {
                return -1;
            }
        }

        // [program.c:L161] file = p1[1]
        if (p1 + 1 >= file_info.Length)
            return -1;
        int file = file_info[p1 + 1];

        // [program.c:L164-L170] now correct line number for that file
        // p2 = file_info; while(p2<p1) { if(p2[1]==file) line_tmp+=*p2; p2+=2; }
        int p2 = 0;
        while (p2 < p1)
        {
            if (p2 + 1 < file_info.Length && file_info[p2 + 1] == file)
            {
                line_tmp += file_info[p2];
            }
            p2 += 2;
        }

        // [program.c:L171-L172] *ret_line = line_tmp; *ret_file = file;
        ret_line = line_tmp;
        ret_file = file;
        return 0;
    }

    // Overload taking Program's FileInfo directly
    public static int translate_absolute_line(int abs_line, Program prog, out int ret_file, out int ret_line)
    {
        if (prog.FileInfo == null)
        {
            ret_file = -1;
            ret_line = -1;
            return -1;
        }
        int blockSize = prog.FileInfo.Length * sizeof(ushort);
        return translate_absolute_line(abs_line, prog.FileInfo, blockSize, out ret_file, out ret_line);
    }
}