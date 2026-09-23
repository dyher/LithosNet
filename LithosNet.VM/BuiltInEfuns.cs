#nullable disable
using System;
using System.Runtime.InteropServices;
using LithosNet.Core;

namespace LithosNet.VM {
    public static class BuiltInEfuns {
        
        [DllImport("libcombat.so", CallingConvention = CallingConvention.Cdecl)]
        private static extern int calc_damage(int atk, int def);

        [Efun("debug_message")]
        public static LpcValue DebugMessage(LpcValue[] args) {
            if (args.Length > 0 && args[0].Type == LpcType.String)
                Console.WriteLine($"💬 [LPC 輸出]: {args[0].AsString()}");
            return LpcValue.Create(0);
        }

        [Efun("debug_int")]
        public static LpcValue DebugInt(LpcValue[] args) {
            if (args.Length > 0 && args[0].Type == LpcType.Int)
                Console.WriteLine($"🔢 [LPC 數值]: {args[0].AsInt()}");
            return LpcValue.Create(0);
        }

        [Efun("calculate_damage")]
        public static LpcValue CalculateDamage(LpcValue[] args) {
            int atk = args[0].AsInt();
            int def = args[1].AsInt();
            return LpcValue.Create(calc_damage(atk, def));
        }

        // 【升級】sizeof() 同時支援字串長度與陣列長度！
        [Efun("sizeof")]
        public static LpcValue Sizeof(LpcValue[] args) {
            if (args.Length > 0) {
                if (args[0].Type == LpcType.String) return LpcValue.Create(args[0].AsString().Length);
                if (args[0].Type == LpcType.Array) return LpcValue.Create(args[0].AsArray().Count);
            }
            return LpcValue.Create(0);
        }
    }
}
