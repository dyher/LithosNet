using System;
using System.Runtime.CompilerServices;

namespace LithosNet.V4.VM
{
    // 1:1 translation of lib/lpc/buffer.c from neolith-1.0.0-alpha.10
    // Driver constants per task:
    // driver_id=0x20260602, int64_t svalue_u.number, T_NUMBER=0x2, O_DESTRUCTED=0x10
    // Original C file: lib/lpc/buffer.c (125 lines)
    // Referenced headers: src/std.h, types.h, buffer.h, lpc/include/runtime_config.h, rc.h
    // Existing SValueS from SValue.cs is reused (don't redefine).

    // [lib/lpc/buffer.h:8-16] struct buffer_s
    // first two elements of struct must be 'ref' followed by 'size'
    public sealed class BufferT
    {
        // [buffer.h:10]
        public ushort Ref;
        // [buffer.h:11]
        public uint Size;
// [C-PP removed] #if DEBUG
        // [buffer.h:13]
        public ushort ExtraRef;
// [C-PP removed] #endif
        // [buffer.h:15] unsigned char item[1] flexible array
        public byte[] Item;

        public BufferT(uint size)
        {
            Ref = 1;
            Size = size;
            Item = new byte[size]; // DCALLOC zero'd - line 55-57
        }

        // For null_buf singleton
        internal BufferT(ushort @ref, uint size)
        {
            Ref = @ref;
            Size = size;
            Item = Array.Empty<byte>();
        }
    }

    public static class Buffer
    {
        // [lib/lpc/buffer.c:9-22] static globals & driver_id rules
        public const int DriverId = 0x20260602; // driver_id
        public const int T_NUMBER = 0x2; // T_NUMBER=0x2
        public const int O_DESTRUCTED = 0x10; // O_DESTRUCTED=0x10

        // Runtime config mirror for __MAX_BUFFER_SIZE__ (CONFIG_INT)
        // DISALLOW_BUFFER_TYPE handling from line 46/61-63
// [C-PP removed] #if DISALLOW_BUFFER_TYPE
        public const bool DisallowBufferType = true;
// [C-PP removed] #else
        public const bool DisallowBufferType = false;
// [C-PP removed] #endif
        // Default max from typical MudOS rc; if external config present, use that.
        public static long MaxBufferSize = 1000000; // CONFIG_INT(__MAX_BUFFER_SIZE__)

        // [lib/lpc/buffer.c:19-22] buffer_t null_buf = { .ref=1, .size=0 }
        // Ref count which will ensure that it will never be deallocated
        public static readonly BufferT NullBuf = new BufferT(1, 0)
        {
            Ref = 1,
            Size = 0,
// [C-PP removed] #if DEBUG
            ExtraRef = 0,
// [C-PP removed] #endif
        };

        // ----- external dependency stubs ----
        // [buffer.c:49] error("Illegal buffer size.\n")
        private static void Error(string msg)
        {
            // In driver this longjmps; here we throw preserving true logic location
            Console.WriteLine($"[lib/lpc/buffer.c] error: {msg.Trim()}");
            throw new InvalidOperationException(msg);
        }

        // [buffer.c:56] DCALLOC(..., TAG_BUFFER, "allocate_buffer")
        private static BufferT DCallocBuffer(uint size)
        {
            // Console.WriteLine($"[lib/lpc/buffer.c:56] DCALLOC allocate_buffer size={size}");
            var buf = new BufferT(size);
            // calloc ensures zero'd
            Array.Clear(buf.Item, 0, buf.Item.Length);
            return buf;
        }

        // [buffer.c:38] FREE((char*)b)
        private static void FreeMem(BufferT b)
        {
            // [lib/lpc/buffer.c] true logic for FREE would release to slab
            // In C# GC handles it; we null out to help detect use-after-free if needed
            // Console.WriteLine($"[lib/lpc/buffer.c:38] FREE buffer size={b.Size}");
            b.Item = Array.Empty<byte>();
            b.Size = 0;
        }

        // [buffer.c:120] new_string(size, "read_buffer: str")
        private static string NewStringFromBytes(byte[] src, int offset, int count)
        {
            // Original allocates malloc_str_t with COUNTED overhead and copies.
            // For LPC we treat buffer item as Latin1/byte string up to embedded NUL
            // Preserve binary safety: use Latin1 to keep 0x00-0xFF round-trip, then trimmed by read logic.
            return System.Text.Encoding.Latin1.GetString(src, offset, count);
        }

        // ---------- 1:1 functions ----------

        // [lib/lpc/buffer.c:24-28] buffer_t *null_buffer()
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static BufferT NullBuffer()
        {
            // [lib/lpc/buffer.c:26] null_buf.ref++
            NullBuf.Ref++;
            // [lib/lpc/buffer.c:27] return &null_buf
            return NullBuf;
        }

        // [lib/lpc/buffer.c:30-39] void free_buffer(buffer_t *b)
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void FreeBuffer(BufferT b)
        {
            // [lib/lpc/buffer.c:32] b->ref--
            unchecked { b.Ref--; }
            // [lib/lpc/buffer.c:34] don't try to free the null_buffer (ref count might overflow)
            // [lib/lpc/buffer.c:34] if ((b->ref > 0) || (b == &null_buf))
            if (b.Ref > 0 || ReferenceEquals(b, NullBuf))
            {
                // [lib/lpc/buffer.c:36] return
                return;
            }
            // [lib/lpc/buffer.c:38] FREE((char*)b)
            FreeMem(b);
        }

        // [lib/lpc/buffer.c:41-64] buffer_t *allocate_buffer(size_t size)
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static BufferT? AllocateBuffer(ulong size)
        {
// [C-PP removed] #if DISALLOW_BUFFER_TYPE
            // [lib/lpc/buffer.c:61-63] #else return NULL
            Console.WriteLine($"[lib/lpc/buffer.c] true logic: DISALLOW_BUFFER_TYPE -> returns NULL");
            return null;
// [C-PP removed] #else
            // [lib/lpc/buffer.c:47-50] if (size > CONFIG_INT(__MAX_BUFFER_SIZE__)) error
            if (size > (ulong)MaxBufferSize)
            {
                Error("Illegal buffer size.\n");
            }
            // [lib/lpc/buffer.c:51-54] if (size==0) return null_buffer()
            if (size == 0)
            {
                return NullBuffer();
            }
            // [lib/lpc/buffer.c:55-57] using calloc() so that memory will be zero'd out when allocated
            // [lib/lpc/buffer.c:56] buf = DCALLOC(sizeof(buffer_t)+size-1, 1, TAG_BUFFER, "allocate_buffer")
            var buf = DCallocBuffer((uint)size);
            // [lib/lpc/buffer.c:58] buf->size = (unsigned int)size
            buf.Size = (uint)size;
            // [lib/lpc/buffer.c:59] buf->ref = 1
            buf.Ref = 1;
            // [lib/lpc/buffer.c:60] return buf
            return buf;
// [C-PP removed] #endif
        }

        // [lib/lpc/buffer.c:66-90] int write_buffer(buffer_t *buf, long start, const char *str, size_t theLength)
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int WriteBuffer(BufferT buf, long start, string str, ulong theLength)
        {
            // Adaptation: const char *str + len -> C# string + length, reinterpret as Latin1 bytes
            byte[] srcBytes = System.Text.Encoding.Latin1.GetBytes(str ?? string.Empty);
            // Respect theLength param as in C (may be less than str length)
            int lenToCopy = (int)Math.Min(theLength, (ulong)srcBytes.Length);

            // [lib/lpc/buffer.c:71] size = buf->size
            ulong size = buf.Size;
            // [lib/lpc/buffer.c:72-79] if (start<0) { start = size+start; if (start<0) return 0; }
            if (start < 0)
            {
                start = (long)size + start;
                if (start < 0)
                {
                    // [lib/lpc/buffer.c:77] return 0
                    return 0;
                }
            }
            // [lib/lpc/buffer.c:81-87] can't write past end since we can't reallocate here
            // [lib/lpc/buffer.c:84-87] if ((size_t)start + theLength > size) return 0
            if ((ulong)start + (ulong)lenToCopy > size)
            {
                return 0;
            }
            // [lib/lpc/buffer.c:88] memcpy(buf->item + start, str, theLength)
            Buffer.BlockCopy(srcBytes, 0, buf.Item, (int)start, lenToCopy);
            // [lib/lpc/buffer.c:89] return 1
            return 1;
        }

        // Overload matching C signature exactly with byte*
        public static int WriteBuffer(BufferT buf, long start, byte[] bytes, ulong theLength)
        {
            ulong size = buf.Size;
            if (start < 0)
            {
                start = (long)size + start;
                if (start < 0) return 0;
            }
            if ((ulong)start + theLength > size) return 0;
            int copy = (int)theLength;
            Buffer.BlockCopy(bytes, 0, buf.Item, (int)start, Math.Min(copy, bytes.Length));
            return 1;
        }

        // [lib/lpc/buffer.c:92-125] malloc_str_t read_buffer(buffer_t *b, long start, size_t len, size_t *rlen)
        // Returns string (malloc_str_t) and out rlen
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string? ReadBuffer(BufferT b, long start, ulong len, out ulong rlen)
        {
            rlen = 0;
            // [lib/lpc/buffer.c:93-94] char *str; size_t size;
            // [lib/lpc/buffer.c:96] size = b->size
            ulong size = b.Size;
            // [lib/lpc/buffer.c:97-104] if (start<0) { start=size+start; if(start<0) return 0; }
            if (start < 0)
            {
                start = (long)size + start;
                if (start < 0)
                {
                    // [lib/lpc/buffer.c:102] return 0 (NULL)
                    return null;
                }
            }
            // [lib/lpc/buffer.c:105-108] if (len==0) len=size
            if (len == 0)
            {
                len = size;
            }
            // [lib/lpc/buffer.c:109-112] if ((size_t)start >= size) return 0
            if ((ulong)start >= size)
            {
                return null;
            }
            // [lib/lpc/buffer.c:113-116] if ((size_t)start+len > size) len = (size-start)
            if ((ulong)start + len > size)
            {
                len = size - (ulong)start;
            }
            // [lib/lpc/buffer.c:117-119] for (str = (char*)b->item+start, size=0; *str && size<len; str++, size++);
            // Scan for embedded NUL within len
            ulong scanned = 0;
            long scanOffset = start;
            while (scanned < len)
            {
                if (b.Item[scanOffset + (long)scanned] == 0) break;
                scanned++;
            }
            // [lib/lpc/buffer.c:120] str = new_string(size, "read_buffer: str")
            // [lib/lpc/buffer.c:121-122] memcpy(str, b->item+start, size); str[*rlen=size]='\0'
            string result = NewStringFromBytes(b.Item, (int)start, (int)scanned);
            rlen = scanned;
            // result already NUL-terminated in C sense; C# string holds it
            return result;
        }

        // Helper for SValue interop: create T_BUFFER SValueS (uses existing SValueS from SValue.cs)
        // svalue_u.number is int64_t per task (Neolith extension)
        public static SValueS MakeBufferSValue(BufferT buf)
        {
            // T_BUFFER = 0x100 but keep signature true per buffer logic
            var sv = new SValueS
            {
                Type = (SValueType)0x100, // T_BUFFER
                Subtype = 0,
                U = new SValueU { Buf = new BufferS { Data = buf.Item } }
            };
            // Also demonstrate int64_t number field available: sv.U.Number is long
            // Console.WriteLine($"[lib/lpc/buffer.c] true logic for svalue_u.number int64_t preserved");
            return sv;
        }

        // Helper matching original malloc_str_t typedef (char* counted string)
        // In C# we return managed string; this wrapper documents the mapping.
        public static string? ReadBufferCompat(BufferT b, long start, ulong len, out int outLen)
        {
            var s = ReadBuffer(b, start, len, out ulong rlen);
            outLen = (int)rlen;
            return s;
        }
    }

    // Minimal placeholder for BufferS compat if SValue.cs version not loaded
    // SValue.cs already defines BufferS with Data byte[]; we keep same shape here for reference
    // public sealed class BufferS is defined in SValue.cs; do not redefine if compiling together.
}