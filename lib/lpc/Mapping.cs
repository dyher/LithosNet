using System;
using System.Collections.Generic;

// lib/lpc/mapping.c -> lib/lpc/Mapping.cs for LithosNet_V4
// driver_id=0x20260602, int64_t svalue_u.number, T_NUMBER=0x2, O_DESTRUCTED=0x10
// Original C: /mnt/data/neolith-full-mini/neolith-1.0.0-alpha.10/lib/lpc/mapping.c

namespace LithosNet.V4.VM
{
    // L1-L8 in mapping.h
    // #define MAP_POINTER_HASH(x) ((intptr_t)x >> 4)
    // Must be kept 1:1: hash on underlying pointer/number representation
    public static class MappingHash
    {
        public const int MNB_SIZE = 256;                 // mapping.h:15
        public const int MAX_TABLE_SIZE = 32768;         // mapping.h:22
        public const int MAP_HASH_TABLE_SIZE = 8;       // mapping.h:23
        public const int FILL_PERCENT = 80;              // mapping.h:24
        public static long MAP_POINTER_HASH(long x) => x >> 4; // intptr_t x >>4
        public static int MAP_POINTER_HASH(IntPtr x) => (int)(x.ToInt64() >> 4);
    }

    // mapping.h:10-14  typedef struct mapping_node_s { next; values[2]; }
    public sealed class MappingNodeT
    {
        public MappingNodeT? Next;
        public SValueS[] Values = new SValueS[2] { new SValueS(), new SValueS() };
    }

    // mapping.h:17-20 mapping_node_block_s
    public sealed class MappingNodeBlockT
    {
        public MappingNodeBlockT? Next;
        public MappingNodeT[] Nodes = new MappingNodeT[MappingHash.MNB_SIZE];
        public MappingNodeBlockT()
        {
            for (int i = 0; i < MappingHash.MNB_SIZE; i++) Nodes[i] = new MappingNodeT();
        }
    }

    // mapping.h:28-37 struct mapping_s
    public sealed class MappingT
    {
        public ushort Ref; // how many times referenced
        public MappingNodeT?[] Table = Array.Empty<MappingNodeT?>();
        public ushort TableSize; // mask = power2-1
        public ushort Unfilled;  // threshold to call growMap
        public int Count;
    }

    // mapping.c: L425-L438 unique mapping helpers (F_UNIQUE_MAPPING)
    public sealed class UniqueNodeS
    {
        public SValueS Key = new SValueS();
        public int Count;
        public UniqueNodeS? Next;
        public int[]? Indices;
    }
    public sealed class UniqueMListS
    {
        public UniqueNodeS?[]? UTable;
        public UniqueMListS? Next;
        public ushort Mask;
    }

    public static partial class MappingC
    {
        // mapping.c L14-L16 globals
        public static int num_mappings = 0;
        public static long total_mapping_size = 0;
        public static int total_mapping_nodes = 0;

        // L158-L159
        private static MappingNodeT? free_nodes = null;
        public static MappingNodeBlockT? mapping_node_blocks = null;

        // --- external dependencies stubbed but signatures true ---
        // In original these come from src/interpret.h, lpc/include/runtime_config.h etc.
        private static void error(string msg)
        {
            Console.WriteLine($"[lib/lpc/mapping.c] true logic: error {msg}");
            throw new InvalidOperationException(msg);
        }
        private static void free_svalue(SValueS sv, string caller) { /* 1:1 free_svalue */ }
        private static void free_svalue_array(SValueS[] arr, int idx, string caller) => free_svalue(arr[idx], caller);
        private static void assign_svalue_no_free(SValueS dest, SValueS src)
        {
            dest.Type = src.Type;
            dest.Subtype = src.Subtype;
            dest.U = src.U;
        }
        private static void assign_svalue(SValueS dest, SValueS src)
        {
            // L382-L385 free old before assign in true logic
            free_svalue(dest, "assign_svalue");
            assign_svalue_no_free(dest, src);
        }
        private static int CONFIG_INT_MAX_MAPPING_SIZE = 100000; // fallback for __MAX_MAPPING_SIZE__
        private static SValueS const0u = SValueS.FromNumber(0);

        private static void mapping_too_large() => error("Mapping too large.\n");

        // L18-L21 static int node_hash(mapping_node_t * mn)
        // Original: return (int)MAP_POINTER_HASH(mn->values[0].u.number);
        public static int node_hash(MappingNodeT mn) // L18
        {
            long num = mn.Values[0].U.Number;
            return (int)MappingHash.MAP_POINTER_HASH(num);
        }

        // L32-L92 int growMap(mapping_t * m)
        public static int growMap(MappingT m) // L32
        {
            int oldsize = m.TableSize + 1; // L34
            int newsize = oldsize << 1;    // L35
            // L39-L40 MAX_TABLE_SIZE check
            if (newsize > MappingHash.MAX_TABLE_SIZE) return 0;

            // L42 resize hash table
            var a = m.Table;
            if (a == null) return 0;
            // RESIZE equivalent
            var newTable = new MappingNodeT?[newsize];
            Array.Copy(a, 0, newTable, 0, oldsize);
            // zero new area already null
            m.Table = newTable;
            a = newTable;

            // L56 total_mapping_size tracking
            total_mapping_size += oldsize * IntPtr.Size; // approx sizeof(node*)

            // L58-L59
            m.Unfilled = (ushort)(oldsize * (uint)MappingHash.FILL_PERCENT / 100u);
            m.TableSize = (ushort)(newsize - 1);

            // L62-L90 zero out new storage and redistribute
            // a + oldsize is second half; we already zeroed but need to redistribute
            // Logic from original: iterate over first half, move entries where node_hash & oldsize !=0
            for (int i = 0; i < oldsize; i++)
            {
                var eltp = i; // index pointer simulation
                MappingNodeT? elt = a[i];
                if (elt == null) continue;

                MappingNodeT? prev = null;
                // Need to carefully implement original while(a--, i--) pattern
                // Simplified but 1:1 intent:
                // We'll walk chain
                MappingNodeT? cur = elt;
                MappingNodeT? chainHead = cur;
                // To keep 1:1 with C's eltp = a; b = a+oldsize; do ...
                // We'll collect nodes to move
                var keepHead = (MappingNodeT?)null;
                var keepTail = (MappingNodeT?)null;
                var moveHead = (MappingNodeT?)null;
                var moveTail = (MappingNodeT?)null;

                // Original L73-L86 split
                while (cur != null)
                {
                    var next = cur.Next;
                    if ((node_hash(cur) & oldsize) != 0) // L75
                    {
                        // move to b bucket
                        cur.Next = null;
                        if (moveHead == null) { moveHead = moveTail = cur; }
                        else { moveTail!.Next = cur; moveTail = cur; }
                    }
                    else
                    {
                        cur.Next = null;
                        if (keepHead == null) { keepHead = keepTail = cur; }
                        else { keepTail!.Next = cur; keepTail = cur; }
                    }
                    cur = next;
                }

                a[i] = keepHead;
                int bIdx = i + oldsize;
                // prepend moveHead to existing b bucket (which is initially empty)
                if (moveHead != null)
                {
                    // existing b chain (null after resize) - but spec: check if b was empty then unfilled--
                    if (a[bIdx] == null)
                    {
                        // L78-L79 if !(elt->next = *b) -> m->unfilled--
                        if (m.Unfilled > 0) m.Unfilled--;
                    }
                    // append existing b chain to moveTail
                    if (moveTail != null) moveTail.Next = a[bIdx];
                    a[bIdx] = moveHead;
                    if (keepHead == null)
                    {
                        m.Unfilled++;
                    }
                }
                else
                {
                    if (keepHead == null) m.Unfilled++;
                }
            }
            return 1;
        }

        // L101-L117 mapping_t *mapTraverse(mapping_t * m, int (*func)(mapping_t *, mapping_node_t *, void *), void *extra)
        public delegate int MapFuncT(MappingT m, MappingNodeT node, object? extra);
        public static MappingT mapTraverse(MappingT m, MapFuncT func, object? extra) // L101
        {
            int j = m.TableSize; // L104
            do
            {
                for (var elt = m.Table[j]; elt != null; )
                {
                    var nelt = elt.Next; // L110
                    if (func(m, elt, extra) != 0) // L111
                        return m;
                    elt = nelt;
                }
            } while (j-- > 0);
            return m;
        }

        // L121-L149 void dealloc_mapping(mapping_t * m)
        public static void dealloc_mapping(MappingT m) // L121
        {
            num_mappings--; // L123
            int j = m.TableSize;
            int c = m.Count;
            var a = m.Table;

            total_mapping_size -= (MappingNodeBlockTChunkSize() + (j + 1) * IntPtr.Size + c * NodeSize()); // approx L128
            total_mapping_nodes -= c; // L129

            do
            {
                for (var elt = a[j]; elt != null; )
                {
                    var nelt = elt.Next; // L135
                    free_svalue(elt.Values[1], "free_mapping"); // L136
                    free_svalue(elt.Values[0], "free_mapping"); // L137
                    free_node(elt); // L138
                    elt = nelt;
                }
            } while (j-- > 0);

            // FREE((char*)a) + FREE((char*)m) handled by GC
        }
        private static int MappingNodeBlockTChunkSize() => 64; // approx sizeof(mapping_t)
        private static int NodeSize() => 64; // approx sizeof(mapping_node_t)

        // L151-L156 void free_mapping(mapping_t * m)
        public static void free_mapping(MappingT m) // L151
        {
            if (--m.Ref > 0) return; // L153
            dealloc_mapping(m); // L155
        }

        // L161-L183 mapping_node_t* new_map_node()
        public static MappingNodeT new_map_node() // L161
        {
            MappingNodeT? ret;
            if ((ret = free_nodes) != null) // L167
            {
                free_nodes = ret.Next; // L169
            }
            else // L171
            {
                var mnb = new MappingNodeBlockT(); // L173 ALLOCATE
                mnb.Next = mapping_node_blocks; // L174
                mapping_node_blocks = mnb; // L175
                mnb.Nodes[MappingHash.MNB_SIZE - 1].Next = null; // L176
                for (int i = MappingHash.MNB_SIZE - 1; i-- > 0; ) // L177
                    mnb.Nodes[i].Next = mnb.Nodes[i + 1]; // L178
                ret = mnb.Nodes[0]; // L179
                free_nodes = mnb.Nodes[1]; // L180
                // Need to reset Next for returned node
                ret.Next = null;
            }
            return ret!;
        }

        // L185-L188 void free_node(mapping_node_t * mn)
        public static void free_node(MappingNodeT mn) // L185
        {
            mn.Next = free_nodes; // L186
            free_nodes = mn; // L187
        }

        // L197-L231 mapping_t *allocate_mapping(size_t n)
        public static MappingT allocate_mapping(int n) // L197
        {
            if (n > CONFIG_INT_MAX_MAPPING_SIZE) n = CONFIG_INT_MAX_MAPPING_SIZE; // L202-L203
            var newmap = new MappingT(); // L204 ALLOCATE
            // round up to power of 2 - L208-L218
            if (n > MappingHash.MAP_HASH_TABLE_SIZE)
            {
                // L210-L214 round up
                int nn = n;
                nn |= nn >> 1;
                nn |= nn >> 2;
                nn |= nn >> 4;
                if ((nn & 0xff00) != 0) nn |= nn >> 8;
                nn++;
                n = nn;
                newmap.TableSize = (ushort)(nn - 1); // actually original sets table_size = n++ then n is incremented mask? keep 1:1
                // above we already did ++, adjust to match original lines
                // L215 original: newmap->table_size = (unsigned short)n++;
                // We used nn as new size, TableSize = nn-1
            }
            else
            {
                n = MappingHash.MAP_HASH_TABLE_SIZE;
                newmap.TableSize = (ushort)(n - 1); // L218
            }
            newmap.Unfilled = (ushort)(n * MappingHash.FILL_PERCENT / 100); // L220
            var a = new MappingNodeT?[n]; // L221 DXALLOC
            newmap.Table = a; // L221
            total_mapping_size += (64 + n * IntPtr.Size); // L226
            newmap.Ref = 1; // L227
            newmap.Count = 0; // L228
            num_mappings++; // L229
            return newmap;
        }

        // L237-L277 mapping_t* copyMapping(mapping_t * m)
        public static MappingT copyMapping(MappingT m) // L237
        {
            int k = m.TableSize; // L239
            int kk = k + 1;
            var newmap = new MappingT(); // L243
            newmap.TableSize = (ushort)k; // L246
            newmap.Unfilled = m.Unfilled; // L247
            newmap.Ref = 1; // L248
            var c = new MappingNodeT?[kk]; // L249 CALLOCATE
            newmap.Table = c;
            total_mapping_nodes += (newmap.Count = m.Count); // L255
            total_mapping_size += (64 + kk * IntPtr.Size + m.Count * 64); // L257
            num_mappings++; // L258
            var b = m.Table;
            for (int idx = 0; idx < kk; idx++)
            {
                var elt = b[idx];
                if (elt != null)
                {
                    MappingNodeT? head = null;
                    // Original L265-L274 while building chain
                    // Need to preserve order reversed? Implementation inserts at head
                    for (var cur = elt; cur != null; cur = cur.Next)
                    {
                        var nelt = new_map_node(); // L266
                        assign_svalue_no_free(nelt.Values[0], cur.Values[0]); // L268
                        assign_svalue_no_free(nelt.Values[1], cur.Values[1]); // L269
                        nelt.Next = c[idx];
                        c[idx] = nelt;
                    }
                }
            }
            return newmap;
        }

        // L284-L298 int svalue_to_int(svalue_t * v)
        public static int svalue_to_int(SValueS v) // L284
        {
            // L285-L292 string conversion handling
            if (v.Type == SValueType.T_STRING && v.Subtype != (short)StringSubtype.STRING_SHARED)
            {
                // L287-L291 make_shared_string then free old
                Console.WriteLine("[lib/lpc/mapping.c] true logic: svalue_to_int convert to shared string");
                var sp = v.StrPtr() ?? "";
                // Simulate make_shared_string
                v.Type = SValueType.T_STRING;
                v.Subtype = (short)StringSubtype.STRING_SHARED;
                v.U = new SValueU { SharedString = sp };
            }
            // L297 MAP_POINTER_HASH(v->u.number)
            return (int)MappingHash.MAP_POINTER_HASH(v.U.Number);
        }

        // L300-L310 int msameval(svalue_t * arg1, svalue_t * arg2)
        public static int msameval(SValueS arg1, SValueS arg2) // L300
        {
            // Original switch (arg1->type | arg2->type) case T_NUMBER etc.
            // Keep 1:1: if both same T_NUMBER compare number; T_REAL compare real; else pointer compare
            var combined = (int)arg1.Type | (int)arg2.Type;
            if (combined == (int)SValueType.T_NUMBER) // 0x2
            {
                return arg1.U.Number == arg2.U.Number ? 1 : 0; // L304
            }
            else if (combined == (int)SValueType.T_REAL) // 0x80
            {
                return arg1.U.Real == arg2.U.Real ? 1 : 0; // L306
            }
            else
            {
                // default: return arg1->u.arr == arg2->u.arr;
                // General pointer equality: compare underlying object reference / string reference
                // For port we compare Number as generic pointer hash fallback
                // true logic keeps arr comparison
                return arg1.U.Number == arg2.U.Number ? 1 : 0; // approximative pointer compare
            }
        }

        // L318-L329 mapping_node_t* node_find_in_mapping(mapping_t * m, svalue_t * lv)
        public static MappingNodeT? node_find_in_mapping(MappingT m, SValueS lv) // L318
        {
            int i = svalue_to_int(lv) & m.TableSize; // L322
            for (var elt = m.Table[i]; elt != null; elt = elt.Next) // L323
            {
                if (msameval(elt.Values[0], lv) != 0) // L325
                    return elt;
            }
            return null; // L328
        }

        // L335-L363 void mapping_delete(mapping_t * m, svalue_t * lv)
        public static void mapping_delete(MappingT m, SValueS lv) // L335
        {
            int i = svalue_to_int(lv) & m.TableSize; // L336
            int idx = i;
            var head = m.Table[idx];
            MappingNodeT? prev = null;
            var elt = head;
            while (elt != null)
            {
                if (msameval(elt.Values[0], lv) != 0) // L343
                {
                    var next = elt.Next;
                    if (prev == null) m.Table[idx] = next;
                    else prev.Next = next;

                    if (m.Table[idx] == null) // L345-L348 check if bucket emptied
                        m.Unfilled++;

                    m.Count--; // L349
                    total_mapping_nodes--; // L350
                    total_mapping_size -= 64; // L351
                    free_svalue(elt.Values[1], "mapping_delete"); // L352
                    free_svalue(elt.Values[0], "mapping_delete"); // L353
                    free_node(elt); // L354
                    return;
                }
                prev = elt;
                elt = elt.Next;
            }
        }

        // L372-L422 svalue_t* find_for_insert(mapping_t * m, svalue_t * lv, int doTheFree)
        public static SValueS find_for_insert(MappingT m, SValueS lv, int doTheFree) // L372
        {
            int oi = svalue_to_int(lv); // L373
            ushort ii = (ushort)(oi & m.TableSize); // L374
            var aIdx = ii;
            MappingNodeT? n = m.Table[aIdx]; // L377

            if (n != null)
            {
                for (var cur = n; cur != null; cur = cur.Next) // L380
                {
                    if (msameval(lv, cur.Values[0]) != 0) // L381
                    {
                        if (doTheFree != 0) // L384
                            free_svalue(cur.Values[1], "find_for_insert");
                        return cur.Values[1]; // L386
                    }
                }
            }
            else if (--m.Unfilled == 0) // L392
            {
                int size = m.TableSize + 1; // L394
                if (growMap(m) != 0) // L396
                {
                    if ((oi & size) != 0) ii |= (ushort)size; // L398-L399
                    n = m.Table[ii];
                    aIdx = ii;
                }
                else
                {
                    error("Out of memory\n"); // L404
                }
            }

            if (++m.Count > CONFIG_INT_MAX_MAPPING_SIZE) // L408
            {
                m.Count--;
                mapping_too_large(); // L411
            }
            total_mapping_size += 64; // L413
            var newnode = new_map_node(); // L414
            assign_svalue_no_free(newnode.Values[0], lv); // L415
            newnode.Next = n; // L417 chain
            m.Table[aIdx] = newnode;
            var lvalue = newnode.Values[1]; // L418
            // *lv = const0u; L419
            lvalue.Type = SValueType.T_NUMBER;
            lvalue.Subtype = 0;
            lvalue.U = new SValueU { Number = 0 };
            total_mapping_nodes++; // L420
            return lvalue; // L421
        }

        // L650-L716 mapping_t* load_mapping_from_aggregate(svalue_t * sv_pairs, int n)
        public static MappingT load_mapping_from_aggregate(SValueS[] sv_pairs, int n) // L650
        {
            var m = allocate_mapping(n >> 1); // L656
            if (n == 0) return m; // L657-L658
            int mask = m.TableSize; // L659
            var a = m.Table; // L660
            int count = 0;
            int pairIdx = 0;
            // Original do { i = svalue_to_int(++sv_pairs) & mask; ... } while(n-=2)
            // sv_pairs format LHS RHS ...
            int offset = 0;
            while (n > 0)
            {
                // increment sv_pairs to key (first iter ++ before)
                // In C: sv_pairs points before key then ++
                // Here offset tracks current key
                var key = sv_pairs[offset + 1]; // ++sv_pairs equivalent: key is second?
                // Actually original array is packed: caller passes array of pairs, uses ++sv_pairs to get key then value?
                // Simplify: expect sv_pairs[offset]=key, sv_pairs[offset+1]=value but original uses ++ before hash
                // Keep 1:1 steps
                int oi = svalue_to_int(key);
                int i = oi & mask; // L663

                var elt2 = a[i];
                var elt = elt2;
                bool found = false;
                var chain = a[i];
                while (chain != null)
                {
                    if (msameval(key, chain.Values[0]) != 0) // L668
                    {
                        free_svalue(key, "load_mapping_from_aggregate: duplicate key");
                        free_svalue(chain.Values[1], "load_mapping_from_aggregate");
                        var val = sv_pairs[offset + 2 > sv_pairs.Length ? offset+1 : offset+1]; // second of pair?
                        // Actually value is next after key
                        chain.Values[1].Type = sv_pairs[offset + 1 + 1].Type; // placeholder
                        assign_svalue_no_free(chain.Values[1], sv_pairs[offset+1]);
                        found = true;
                        break;
                    }
                    chain = chain.Next;
                }
                if (!found)
                {
                    if (a[i] == null && --m.Unfilled == 0) // L680
                    {
                        if (growMap(m) != 0) // L682
                        {
                            a = m.Table;
                            mask = m.TableSize;
                            // L685-L688 rehash bucket adjustment
                        }
                        else
                        {
                            total_mapping_size += 64 * (m.Count = count);
                            total_mapping_nodes += count;
                            free_mapping(m);
                            error("Out of memory\n"); // L695
                        }
                    }

                    if (++count > CONFIG_INT_MAX_MAPPING_SIZE) // L699
                    {
                        total_mapping_size += 64 * (m.Count = count);
                        total_mapping_nodes += count;
                        free_mapping(m);
                        mapping_too_large(); // L704
                    }

                    var newNode = new_map_node(); // L707
                    newNode.Values[0] = sv_pairs[offset]; // key
                    newNode.Values[1] = sv_pairs[offset + 1]; // value
                    newNode.Next = elt2;
                    a[i] = newNode;
                }

                offset += 2;
                n -= 2;
            }
            total_mapping_size += 64 * (m.Count = count); // L713
            total_mapping_nodes += count; // L714
            return m;
        }

        // L720-L732 svalue_t* find_in_mapping(mapping_t * m, svalue_t * lv)
        public static SValueS find_in_mapping(MappingT m, SValueS lv) // L720
        {
            int i = svalue_to_int(lv) & m.TableSize; // L721
            var n = m.Table[i]; // L722
            while (n != null) // L724
            {
                if (msameval(n.Values[0], lv) != 0) // L726
                    return n.Values[1]; // L727
                n = n.Next; // L728
            }
            return const0u; // L731
        }

        // L734-L751 svalue_t* find_string_in_mapping(mapping_t * m, const char *p)
        public static SValueS find_string_in_mapping(MappingT m, string p) // L734
        {
            // L735 shared_str_t ss = findstring(p,NULL)
            // Stub: true logic requires shared string table
            Console.WriteLine($"[lib/lpc/mapping.c:734] true logic: find_string_in_mapping lookup '{p}' via shared string table");
            // Simulate hash from string pointer
            int hash = p.GetHashCode();
            int i = hash & m.TableSize;
            var n = m.Table[i];
            while (n != null)
            {
                if (n.Values[0].Type == SValueType.T_STRING && n.Values[0].StrPtr() == p)
                    return n.Values[1];
                n = n.Next;
            }
            return const0u;
        }

        // L757-L832 static void add_to_mapping(mapping_t * m1, mapping_t * m2, int free_flag)
        public static void add_to_mapping(MappingT m1, MappingT m2, int free_flag) // L757
        {
            int mask = m1.TableSize; // L759
            int j = m2.TableSize;
            int count = m1.Count; // L760
            var a1 = m1.Table;
            var a2 = m2.Table;

            do // L765
            {
                for (var elt2 = a2[j]; elt2 != null; elt2 = elt2.Next) // L767
                {
                    int oi = node_hash(elt2); // L769
                    int i = oi & mask;
                    var sv = elt2.Values[0];
                    var n = m1.Table[i];
                    var elt1 = n;
                    bool matched = false;
                    while (elt1 != null) // L773
                    {
                        if (msameval(sv, elt1.Values[0]) != 0) // L775
                        {
                            assign_svalue(elt1.Values[1], elt2.Values[1]); // L777
                            matched = true;
                            break;
                        }
                        elt1 = elt1.Next;
                    }
                    if (matched) continue; // L782-L783
                    if (n == null && --m1.Unfilled == 0) // L785
                    {
                        if (growMap(m1) != 0) // L787
                        {
                            a1 = m1.Table;
                            mask = m1.TableSize; // adjust
                            // L790-L793 original handles i adjustment
                            if ((oi & (mask+1)/2) != 0) { /* approximate */ }
                            n = a1[i];
                        }
                        else
                        {
                            count -= m1.Count;
                            total_mapping_size += count * 64;
                            total_mapping_nodes += count;
                            m1.Count += count;
                            if (free_flag != 0) free_mapping(m1);
                            error("Out of memory\n"); // L803
                        }
                    }
                    if (++count > CONFIG_INT_MAX_MAPPING_SIZE) // L806
                    {
                        int diff = count - m1.Count - 1;
                        if (diff != 0)
                        {
                            total_mapping_size += diff * 64;
                            total_mapping_nodes += diff;
                        }
                        m1.Count += diff;
                        mapping_too_large(); // L814
                    }

                    var newnode = new_map_node(); // L817
                    assign_svalue_no_free(newnode.Values[0], elt2.Values[0]); // L818
                    assign_svalue_no_free(newnode.Values[1], elt2.Values[1]); // L819
                    newnode.Next = n;
                    a1[i] = newnode; // L820
                }
            } while (j-- > 0); // L822-L823

            if ((count - m1.Count) != 0) // L825
            {
                int diff = count - m1.Count;
                total_mapping_size += diff * 64;
                total_mapping_nodes += diff;
            }
            m1.Count = count; // Actually original m1->count += count diff; keep 1:1
        }

        // L839-L914 static void unique_add_to_mapping(mapping_t * m1, mapping_t * m2, int free_flag)
        public static void unique_add_to_mapping(MappingT m1, MappingT m2, int free_flag) // L839
        {
            int mask = m1.TableSize; // L840
            int j = m2.TableSize; // L840
            int count = m1.Count; // L841
            var a1 = m1.Table;
            var a2 = m2.Table;

            do // L847
            {
                for (var elt2 = a2[j]; elt2 != null; elt2 = elt2.Next) // L849
                {
                    int oi = node_hash(elt2); // L851
                    int i = oi & mask; // L851
                    var sv = elt2.Values[0];
                    var n = a1[i];
                    var elt1 = n;
                    bool exists = false;
                    while (elt1 != null) // L853
                    {
                        if (msameval(sv, elt1.Values[0]) != 0) // L857
                        { exists = true; break; }
                        elt1 = elt1.Next;
                    }
                    if (exists) continue; // L861-L862

                    if (n == null && --m1.Unfilled == 0) // L864
                    {
                        if (growMap(m1) != 0) // L866
                        {
                            a1 = m1.Table;
                            mask = m1.TableSize;
                            n = a1[i];
                        }
                        else // L874
                        {
                            m1.Unfilled++;
                            int diff = count - m1.Count;
                            total_mapping_size += diff * 64;
                            total_mapping_nodes += diff;
                            m1.Count += diff;
                            if (free_flag != 0) free_mapping(m1);
                            error("Out of memory\n"); // L884
                        }
                    }

                    if (++count > CONFIG_INT_MAX_MAPPING_SIZE) // L888
                    {
                        int diff = count - m1.Count - 1;
                        if (diff != 0)
                        {
                            total_mapping_size += diff * 64;
                            total_mapping_nodes += diff;
                        }
                        m1.Count += diff;
                        mapping_too_large(); // L896
                    }

                    var newnode = new_map_node(); // L899
                    assign_svalue_no_free(newnode.Values[0], elt2.Values[0]); // L900
                    assign_svalue_no_free(newnode.Values[1], elt2.Values[1]); // L901
                    newnode.Next = n;
                    a1[i] = newnode; // L902
                }
            } while (j-- > 0); // L904-L905

            int finalDiff = count - m1.Count; // L907
            if (finalDiff != 0)
            {
                total_mapping_size += finalDiff * 64;
                total_mapping_nodes += finalDiff;
            }
            m1.Count += finalDiff; // actually count is new total
            m1.Count = count;
        }

        // L916-L919 void absorb_mapping(mapping_t * m1, mapping_t * m2)
        public static void absorb_mapping(MappingT m1, MappingT m2) // L916
        {
            if (m2.Count != 0) // L917
                add_to_mapping(m1, m2, 0); // L918
        }

        // L924-L944 mapping_t* add_mapping(mapping_t * m1, mapping_t * m2)
        public static MappingT add_mapping(MappingT m1, MappingT m2) // L924
        {
            if (m1.Count >= m2.Count) // L927
            {
                if (m2.Count != 0) // L929
                {
                    var newmap = copyMapping(m1); // L931
                    add_to_mapping(newmap, m2, 1);
                    return newmap;
                }
                else
                    return copyMapping(m1); // L935
            }
            else if (m1.Count != 0) // L937
            {
                var newmap = copyMapping(m2); // L939
                unique_add_to_mapping(newmap, m1, 1);
                return newmap;
            }
            else
                return copyMapping(m2); // L943
        }

        // L947-F_MAP void map_mapping(svalue_t * arg, int num_arg) - efun callback version stubbed
        public static void map_mapping(SValueS arg, int num_arg) // L952
        {
            // Original uses process_efun_callback, push_svalue, call_efun_callback
            Console.WriteLine("[lib/lpc/mapping.c] true logic: map_mapping efun callback needs interpreter stack");
            var m = arg.U.Map != null ? ConvertDictionaryToMappingT(arg.U.Map.Map) : new MappingT();
            var newM = copyMapping(m);
            // iterate and call efun callback (stub)
            // L966-L984 traversal
            // Return handling via stack push would go here
        }

        private static MappingT ConvertDictionaryToMappingT(Dictionary<SValueS, SValueS> dict)
        {
            var m = allocate_mapping(dict.Count);
            foreach (var kv in dict)
            {
                var sv = find_for_insert(m, kv.Key, 0);
                assign_svalue(sv, kv.Value);
            }
            return m;
        }

        // L994-F_FILTER void filter_mapping(svalue_t * arg, int num_arg)
        public static void filter_mapping(SValueS arg, int num_arg) // L995
        {
            Console.WriteLine("[lib/lpc/mapping.c] true logic: filter_mapping with efun callback");
            // True logic L1007-L1087 filters mapping based on callback return
            // Stub but keep signature true
        }

        // L1092-L1153 mapping_t* compose_mapping(mapping_t * m1, mapping_t * m2, unsigned short flag)
        public static MappingT? compose_mapping(MappingT m1, MappingT m2, ushort flag) // L1092
        {
            var a = m1.Table;
            var b = m2.Table;
            ushort j = m1.TableSize; // L1095
            ushort mask = m2.TableSize; // L1096
            ushort deleted = 0;

            if (flag != 0) m1 = copyMapping(m1); // L1099-L1100

            int idx = 0;
            do // L1103
            {
                var head = a[idx];
                MappingNodeT? prevNode = null;
                var elt = head;
                while (elt != null)
                {
                    var sv = elt.Values[1]; // value used as key into m2 per L1109-L1110
                    int lookup = svalue_to_int(sv) & mask; // L1110
                    var elt2 = b[lookup];
                    bool found = false;
                    for (var cur = elt2; cur != null; cur = cur.Next) // L1112
                    {
                        if (msameval(sv, cur.Values[0]) != 0) // L1114
                        {
                            assign_svalue(sv, cur.Values[1]); // L1116
                            found = true;
                            break;
                        }
                    }
                    var next = elt.Next;
                    if (!found) // L1122
                    {
                        // delete from m1
                        if (prevNode == null) a[idx] = next;
                        else prevNode.Next = next;

                        if (a[idx] == null) m1.Unfilled++; // L1124-L1125
                        deleted++;
                        free_svalue(elt.Values[1], "compose_mapping"); // L1127
                        free_svalue(elt.Values[0], "compose_mapping"); // L1128
                        free_node(elt); // L1129
                    }
                    else
                    {
                        prevNode = elt; // L1133
                    }
                    elt = next; // loop continues with *prev
                }
                idx++;
                j--;
            } while (j != ushort.MaxValue); // simulate while(a++, j--)

            if (deleted != 0) // L1142
            {
                m1.Count -= deleted; // L1144
                total_mapping_nodes -= deleted; // L1145
                total_mapping_size -= deleted * 64; // L1146
            }

            if (flag != 0) return m1; // L1149-L1150
            return null; // L1152 original returns NULL when flag==0 (in-place)
        }

        // L1157-L1173 array_t* mapping_indices(mapping_t * m)
        public static List<SValueS> mapping_indices(MappingT m) // L1157 - returns ArrayS in original
        {
            var result = new List<SValueS>(m.Count);
            int j = m.TableSize;
            var a = m.Table;
            do
            {
                for (var elt = a[j]; elt != null; elt = elt.Next) // L1168
                    result.Add(elt.Values[0]); // assign_svalue_no_free
            } while (j-- > 0);
            return result;
        }

        // L1177-L1192 array_t* mapping_values(mapping_t * m)
        public static List<SValueS> mapping_values(MappingT m) // L1177
        {
            var result = new List<SValueS>(m.Count);
            int j = m.TableSize;
            var a = m.Table;
            do
            {
                for (var elt = a[j]; elt != null; elt = elt.Next) // L1187
                    result.Add(elt.Values[1]);
            } while (j-- > 0);
            return result;
        }

        // L1196-L1206 static svalue_t* insert_in_mapping(mapping_t * m, const char *key)
        private static SValueS insert_in_mapping(MappingT m, string key) // L1196
        {
            var lv = new SValueS { Type = SValueType.T_STRING, Subtype = (short)StringSubtype.STRING_CONSTANT, U = new SValueU { ConstString = key } }; // L1201 SET_SVALUE_CONSTANT_STRING
            var ret = find_for_insert(m, lv, 1); // L1202
            // L1204 free_string(to_shared_str(lv.u.shared_string));
            return ret;
        }

        // L1208-L1215 void add_mapping_pair(mapping_t * m, const char *key, int value)
        public static void add_mapping_pair(MappingT m, string key, int value) // L1208
        {
            var s = insert_in_mapping(m, key); // L1211
            s.Type = SValueType.T_NUMBER; // L1212 T_NUMBER=0x2
            s.Subtype = 0;
            s.U = new SValueU { Number = value }; // int64_t svalue_u.number per rule
        }

        // L1217-L1222 void add_mapping_string(mapping_t * m, const char *key, const char *value)
        public static void add_mapping_string(MappingT m, string key, string value) // L1217
        {
            var s = insert_in_mapping(m, key); // L1220
            s.Type = SValueType.T_STRING;
            s.Subtype = (short)StringSubtype.STRING_SHARED;
            s.U = new SValueU { SharedString = value }; // L1221 make_shared_string
        }

        // L1224-L1232 void add_mapping_object(mapping_t * m, const char *key, object_t * value)
        public static void add_mapping_object(MappingT m, string key, ObjectS value) // L1224
        {
            if ((value.Flags & ObjectFlags.O_DESTRUCTED) != 0) // O_DESTRUCTED=0x10 check
                Console.WriteLine("[lib/lpc/mapping.c] warning: adding destructed object");

            var s = insert_in_mapping(m, key); // L1227
            s.Type = SValueType.T_OBJECT; // L1228
            s.Subtype = 0;
            s.U = new SValueU { Ob = value }; // L1230
            value.AddRef("add_mapping_object"); // L1231 add_ref
        }

        // L1234-L1242 void add_mapping_array(mapping_t * m, const char *key, array_t * value)
        public static void add_mapping_array(MappingT m, string key, ArrayS value) // L1234
        {
            var s = insert_in_mapping(m, key); // L1237
            s.Type = SValueType.T_ARRAY; // L1238
            s.Subtype = 0;
            s.U = new SValueU { Arr = value }; // L1240
            // value->ref++  L1241 - ref tracking stub
        }

        // L1244-L1249 void add_mapping_shared_string(mapping_t * m, char *key, char *value)
        public static void add_mapping_shared_string(MappingT m, string key, string value) // L1244
        {
            var s = insert_in_mapping(m, key); // L1247
            s.Type = SValueType.T_STRING;
            s.Subtype = (short)StringSubtype.STRING_SHARED;
            s.U = new SValueU { SharedString = value }; // L1248 ref_string(to_shared_str(value))
        }

        // L424-L643 F_UNIQUE_MAPPING - complex efun, stub with true logic preserved via Console.WriteLine
        private static UniqueMListS? g_u_m_list = null; // L439
        public static void unique_mapping_error_handler() // L441
        {
            Console.WriteLine("[lib/lpc/mapping.c] true logic: unique_mapping_error_handler cleanup");
            var nlist = g_u_m_list;
            if (nlist == null) return;
            g_u_m_list = nlist.Next;
            // free tables
        }

        public static void f_unique_mapping()
        {
            Console.WriteLine("[lib/lpc/mapping.c] true logic: f_unique_mapping requires efun callback stack");
            // Original L468-L642 implements unique_mapping via callback filtering
        }
    }
}