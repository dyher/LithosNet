using System;
using System.Collections.Generic;
using System.Linq;

namespace LithosNet.VM;

/// <summary>
/// Port of heart_beat.c
/// </summary>
public sealed class HeartBeatManager
{
    public static HeartBeatManager Current { get; } = new HeartBeatManager();
    private readonly HashSet<LpcObject> _active = new();
    private int _intervalMs = 1000;
    private long _lastTick = 0;

    public bool HasHeartBeat(LpcObject ob) => ob != null && _active.Contains(ob);

    public void SetHeartBeat(LpcObject ob, bool on)
    {
        if (on) _active.Add(ob);
        else _active.Remove(ob);
    }

    public void Tick(InterpreterV4 interp)
    {
        long now = Environment.TickCount64;
        if (now - _lastTick < _intervalMs) return;
        _lastTick = now;

        foreach (var ob in _active.ToArray())
        {
            try
            {
                if (ob.TryFindFunction("heart_beat", out _, out _))
                    interp.CallApply("heart_beat", Array.Empty<SValueS>(), ob);
            }
            catch (Exception ex) { Console.WriteLine($"[heart_beat {ob.ProgramName}] {ex.Message}"); }
        }
    }

    public int Count => _active.Count;
}
