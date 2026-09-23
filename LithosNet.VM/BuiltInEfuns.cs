#nullable disable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LithosNet.Core;

namespace LithosNet.VM {
    public static class BuiltInEfuns {
        [DllImport("libcombat.so", CallingConvention = CallingConvention.Cdecl)]
        private static extern int calc_damage(int atk, int def);

        [Efun("debug_message")]
        public static LpcValue DebugMessage(LpcValue[] args) { if (args.Length > 0 && args[0].Type == LpcType.String) Console.WriteLine($"💬 [LPC]: {args[0].AsString()}"); return LpcValue.Create(0); }
        [Efun("debug_int")]
        public static LpcValue DebugInt(LpcValue[] args) { if (args.Length > 0 && args[0].Type == LpcType.Int) Console.WriteLine($"🔢 [LPC]: {args[0].AsInt()}"); return LpcValue.Create(0); }
        [Efun("calculate_damage")]
        public static LpcValue CalculateDamage(LpcValue[] args) { return LpcValue.Create(calc_damage(args[0].AsInt(), args[1].AsInt())); }
        [Efun("sizeof")]
        public static LpcValue Sizeof(LpcValue[] args) { if (args.Length > 0) { if (args[0].Type == LpcType.String) return LpcValue.Create(args[0].AsString().Length); if (args[0].Type == LpcType.Array) return LpcValue.Create(args[0].AsArray().Count); } return LpcValue.Create(0); }
        [Efun("users")]
        public static LpcValue Users(LpcValue[] args) { var list = new List<LpcValue>(); foreach (var u in SessionManager.GetAllSessions()) list.Add(LpcValue.Create(u)); return LpcValue.Create(list); }
        [Efun("explode")]
        public static LpcValue Explode(LpcValue[] args) { if (args.Length < 2) return LpcValue.Create(new List<LpcValue>()); var parts = args[0].AsString().Split(new[] { args[1].AsString() }, StringSplitOptions.None); var list = new List<LpcValue>(); foreach (var p in parts) list.Add(LpcValue.Create(p)); return LpcValue.Create(list); }
        [Efun("implode")]
        public static LpcValue Implode(LpcValue[] args) { if (args.Length < 2) return LpcValue.Create(""); var arr = args[0].AsArray(); var strings = new List<string>(); foreach (var v in arr) strings.Add(v.Type == LpcType.String ? v.AsString() : v.ToString()); return LpcValue.Create(string.Join(args[1].AsString(), strings)); }
        [Efun("get_tick")]
        public static LpcValue GetTick(LpcValue[] args) { return LpcValue.Create(Environment.TickCount); }
    }
}
