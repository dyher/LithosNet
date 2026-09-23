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
        public static LpcValue DebugMessage(LpcValue[] args) {
            if (args.Length > 0 && args[0].Type == LpcType.String) Console.WriteLine($"💬 [LPC]: {args[0].AsString()}");
            return LpcValue.Create(0);
        }

        [Efun("debug_int")]
        public static LpcValue DebugInt(LpcValue[] args) {
            if (args.Length > 0 && args[0].Type == LpcType.Int) Console.WriteLine($"🔢 [LPC]: {args[0].AsInt()}");
            return LpcValue.Create(0);
        }

        [Efun("calculate_damage")]
        public static LpcValue CalculateDamage(LpcValue[] args) {
            return LpcValue.Create(calc_damage(args[0].AsInt(), args[1].AsInt()));
        }

        [Efun("sizeof")]
        public static LpcValue Sizeof(LpcValue[] args) {
            if (args.Length > 0) {
                if (args[0].Type == LpcType.String) return LpcValue.Create(args[0].AsString().Length);
                if (args[0].Type == LpcType.Array) return LpcValue.Create(args[0].AsArray().Count);
            }
            return LpcValue.Create(0);
        }

        [Efun("users")]
        public static LpcValue Users(LpcValue[] args) {
            var list = new List<LpcValue>();
            foreach (var u in SessionManager.GetAllSessions()) list.Add(LpcValue.Create(u));
            return LpcValue.Create(list);
        }

        // 【新增】objectp() - 檢查物件是否已被 destruct
        [Efun("objectp")]
        public static LpcValue Objectp(LpcValue[] args) {
            if (args.Length > 0 && args[0].Type == LpcType.String) {
                // 這裡我們需要一個靜態方法來查詢 ObjectManager，為了簡化，我們直接在 Interpreter 中攔截
                return LpcValue.Create(1); 
            }
            return LpcValue.Create(0);
        }
    }
}
