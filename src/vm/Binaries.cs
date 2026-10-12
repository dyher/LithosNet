
using System;
using System.IO;
using System.Text;

namespace LithosNet.VM;

public sealed class BinaryCorruptException : Exception
{
    public BinaryCorruptException(string msg) : base(msg) { }
}

public static class Binaries
{
    public const uint DRIVER_ID = 0x20260602u;
    public const string MAGIC = "NEOL";
    private const int MAX_STRINGS = 65535;
    private const int MAX_CODE = 65535;
    private const int HEADER_SIZE = 4 + 4 + 2 + 2; // magic + driverId + totalSize + offsetLineInfo

    public static bool TryLoadBinary(string path, out Program prog, out string error)
    {
        prog = null;
        error = null;
        try
        {
            if (!File.Exists(path))
            {
                error = "File not found: " + path;
                return false;
            }
            byte[] data = File.ReadAllBytes(path);
            if (data.Length < HEADER_SIZE)
            {
                error = "File too short";
                return false;
            }
            int pc = 0;
            string magic = Encoding.ASCII.GetString(data, pc, 4);
            pc += 4;
            if (magic != MAGIC)
            {
                error = "Bad magic: " + magic;
                return false;
            }
            uint driverId = BitConverter.ToUInt32(data, pc);
            pc += 4;
            if (driverId != DRIVER_ID && driverId != Program.DRIVER_ID)
            {
                error = "Driver ID mismatch: file 0x" + driverId.ToString("X8") + " != current 0x" + DRIVER_ID.ToString("X8") + " - fallback to recompile";
                return false;
            }
            ushort totalSize = BitConverter.ToUInt16(data, pc);
            pc += 2;
            ushort offsetLineInfo = BitConverter.ToUInt16(data, pc);
            pc += 2;

            if (data.Length < totalSize)
            {
                error = "Truncated file: expected " + totalSize + " got " + data.Length;
                return false;
            }
            if (offsetLineInfo > data.Length)
            {
                error = "Invalid offsetLineInfo";
                return false;
            }

            prog = new Program();
            prog.Name = Path.GetFileNameWithoutExtension(path);
            // Simplified format: after header, we store counts then blobs
            // Real format: [file_info_blob][line_info_blob][strings][functions][inherits][code]
            // For P15b baseline we implement minimal compatible loader
            using (var ms = new MemoryStream(data, HEADER_SIZE, data.Length - HEADER_SIZE))
            using (var br = new BinaryReader(ms, Encoding.UTF8, true))
            {
                // file info
                ushort fileInfoCount = br.ReadUInt16();
                if (fileInfoCount > 0)
                {
                    prog.FileInfo = new ushort[fileInfoCount];
                    for (int i = 0; i < fileInfoCount; i++) prog.FileInfo[i] = br.ReadUInt16();
                }
                // line info
                int lineInfoLen = br.ReadInt32();
                if (lineInfoLen < 0 || lineInfoLen > 1000000) throw new BinaryCorruptException("Bad lineInfo len");
                if (lineInfoLen > 0)
                {
                    prog.LineInfo = br.ReadBytes(lineInfoLen);
                }
                // strings - ByteSpan safe, len + raw bytes, no null terminator
                ushort numStrings = br.ReadUInt16();
                if (numStrings > MAX_STRINGS) throw new BinaryCorruptException("Too many strings");
                prog.Strings = new string[numStrings];
                prog.NumStrings = numStrings;
                for (int i = 0; i < numStrings; i++)
                {
                    ushort len = br.ReadUInt16();
                    if (len > 0)
                    {
                        byte[] raw = br.ReadBytes(len);
                        // Preserve embedded nulls - use UTF8 decoding with nulls intact
                        prog.Strings[i] = Encoding.UTF8.GetString(raw);
                    }
                    else
                    {
                        prog.Strings[i] = "";
                    }
                }
                // functions
                ushort numFuncs = br.ReadUInt16();
                prog.FunctionTable = new CompilerFunction[numFuncs];
                for (int i = 0; i < numFuncs; i++)
                {
                    var f = new CompilerFunction();
                    ushort nameLen = br.ReadUInt16();
                    f.Name = nameLen > 0 ? Encoding.UTF8.GetString(br.ReadBytes(nameLen)) : "";
                    f.Type = br.ReadUInt16();
                    f.RuntimeIndex = br.ReadUInt16();
                    f.Address = br.ReadUInt16();
                    prog.FunctionTable[i] = f;
                }
                // code
                int codeLen = br.ReadInt32();
                if (codeLen < 0 || codeLen > MAX_CODE) throw new BinaryCorruptException("Bad code len");
                prog.Code = br.ReadBytes(codeLen);
                prog.ProgramSize = (ushort)codeLen;
                prog.TotalSize = data.Length;
            }
            return true;
        }
        catch (Exception ex)
        {
            error = "Load failed: " + ex.Message;
            prog = null;
            return false;
        }
    }

    public static void SaveBinary(Program prog, string path)
    {
        if (prog == null) throw new ArgumentNullException(nameof(prog));
        if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms, Encoding.UTF8, true))
        {
            // header placeholder
            bw.Write(Encoding.ASCII.GetBytes(MAGIC));
            bw.Write(DRIVER_ID);
            bw.Write((ushort)0); // totalSize placeholder
            bw.Write((ushort)0); // offsetLineInfo placeholder - we will fill later if needed

            long headerEnd = ms.Position;

            // file info
            if (prog.FileInfo != null && prog.FileInfo.Length > 0)
            {
                bw.Write((ushort)prog.FileInfo.Length);
                foreach (var v in prog.FileInfo) bw.Write(v);
            }
            else
            {
                bw.Write((ushort)0);
            }

            // line info - single blob per lpc-program.md epilog format
            if (prog.LineInfo != null && prog.LineInfo.Length > 0)
            {
                bw.Write(prog.LineInfo.Length);
                bw.Write(prog.LineInfo);
            }
            else
            {
                bw.Write(0);
            }

            // strings - ByteSpan: 16-bit len + raw bytes, no null terminator, O(1) SHARED_STRLEN
            ushort numStrings = (ushort)(prog.Strings?.Length ?? 0);
            bw.Write(numStrings);
            if (prog.Strings != null)
            {
                foreach (var s in prog.Strings)
                {
                    if (s == null)
                    {
                        bw.Write((ushort)0);
                    }
                    else
                    {
                        byte[] raw = Encoding.UTF8.GetBytes(s);
                        if (raw.Length > ushort.MaxValue) throw new InvalidOperationException("String too long");
                        bw.Write((ushort)raw.Length);
                        bw.Write(raw);
                    }
                }
            }

            // functions
            ushort numFuncs = (ushort)(prog.FunctionTable?.Length ?? 0);
            bw.Write(numFuncs);
            if (prog.FunctionTable != null)
            {
                foreach (var f in prog.FunctionTable)
                {
                    byte[] nameBytes = Encoding.UTF8.GetBytes(f.Name ?? "");
                    bw.Write((ushort)nameBytes.Length);
                    if (nameBytes.Length > 0) bw.Write(nameBytes);
                    bw.Write(f.Type);
                    bw.Write(f.RuntimeIndex);
                    bw.Write(f.Address);
                }
            }

            // code - max 65535 per A_PROGRAM
            int codeLen = prog.Code?.Length ?? 0;
            if (codeLen > MAX_CODE) throw new InvalidOperationException("Code too large");
            bw.Write(codeLen);
            if (codeLen > 0) bw.Write(prog.Code);

            // finalize header
            long total = ms.Position;
            ms.Position = 4 + 4; // after magic + driverId
            bw.Write((ushort)total); // totalSize
            bw.Write((ushort)0); // offsetLineInfo - for P15b we keep 0, real impl would point to line_info_blob

            File.WriteAllBytes(path, ms.ToArray());
        }
    }

    public static bool ValidateDriverId(string path)
    {
        try
        {
            byte[] data = File.ReadAllBytes(path);
            if (data.Length < 8) return false;
            string magic = Encoding.ASCII.GetString(data, 0, 4);
            if (magic != MAGIC) return false;
            uint did = BitConverter.ToUInt32(data, 4);
            return did == DRIVER_ID;
        }
        catch { return false; }
    }
}
