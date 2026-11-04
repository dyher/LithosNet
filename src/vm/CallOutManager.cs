using System;
using System.Collections.Generic;
using System.Linq;

namespace LithosNet.VM;

/// <summary>
/// Port of call_out.cpp
/// </summary>
public sealed class CallOutManager
{
    private sealed class Entry
    {
        public long DueTick;
        public string Func;
        public LpcObject Obj;
        public SValueS[] Args;
        public int Handle;
    }

    private readonly List<Entry> _queue = new();
    private int _nextHandle = 1;

    public int CallOut(LpcObject obj, string func, int delayMs, SValueS[] args)
    {
        var e = new Entry
        {
            DueTick = Environment.TickCount64 + delayMs,
            Func = func,
            Obj = obj,
            Args = args ?? Array.Empty<SValueS>(),
            Handle = _nextHandle++
        };
        _queue.Add(e);
        _queue.Sort((a,b)=>a.DueTick.CompareTo(b.DueTick));
        return e.Handle;
    }

    public bool RemoveCallOut(int handle)
    {
        int idx = _queue.FindIndex(x=>x.Handle==handle);
        if (idx<0) return false;
        _queue.RemoveAt(idx);
        return true;
    }

    public void Tick(InterpreterV4 interp)
    {
        long now = Environment.TickCount64;
        while (_queue.Count>0 && _queue[0].DueTick <= now)
        {
            var e = _queue[0];
            _queue.RemoveAt(0);
            try { interp.CallApply(e.Func, e.Args, e.Obj); }
            catch (Exception ex) { Console.WriteLine($"[call_out {e.Func} handle={e.Handle}] {ex.Message}"); }
        }
    }

    public int Count => _queue.Count;
}
