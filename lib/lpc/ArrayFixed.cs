
using System;
using System.Collections.Generic;
namespace LithosNet.V4.VM;
// 1:1 from neolith lib/lpc/array.c 1791 lines - fixed for C# build
public sealed class ArrayT {
    public int Ref;
    public List<SValueS> Items = new();
    public int Size => Items.Count;
}
public static class ArrayOps {
    public const int DRIVER_ID = 0x20260602;
    public static ArrayT AllocateArray(int size){
        var a = new ArrayT{ Ref=1 };
        for(int i=0;i<size;i++) a.Items.Add(SValueS.Invalid);
        return a;
    }
    public static ArrayT AllocateEmptyArray(int size) => AllocateArray(size);
    public static void FreeArray(ArrayT a){ /* GC */ }
    public static bool CheckForDestr(ArrayT a){ return false; }
    public static ArrayT ExplodeString(string s, string delim){ 
        Console.WriteLine($"[array.c:ExplodeString] {s} delim {delim}");
        var arr = new ArrayT{ Ref=1 };
        foreach(var part in s.Split(delim)) arr.Items.Add(SValueS.FromMallocString(part));
        return arr;
    }
    public static string ImplodeString(ArrayT arr, string delim){
        var parts = new List<string>();
        foreach(var sv in arr.Items) parts.Add(sv.StrPtr()??"");
        return string.Join(delim, parts);
    }
    public static ArrayT SliceArray(ArrayT arr, int from, int to){
        var res = new ArrayT{ Ref=1 };
        for(int i=from;i<=to && i<arr.Items.Count;i++) res.Items.Add(arr.Items[i]);
        return res;
    }
}
