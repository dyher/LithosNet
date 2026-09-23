using System;
using System.Runtime.InteropServices;
using LithosNet.Core;

namespace LithosNet.VM {
    public static class BuiltInEfuns {
        
        // 【FFI 核心】直接綁定 C 語言的動態庫！(納秒級效能)
        [DllImport("libcombat.so", CallingConvention = CallingConvention.Cdecl)]
        private static extern int calc_damage(int atk, int def);

        // 【Efun 1】debug_message(string msg)
        [Efun("debug_message")]
        public static LpcValue DebugMessage(LpcValue[] args) {
            if (args.Length > 0 && args[0].Type == LpcType.String) {
                Console.WriteLine($"💬 [LPC 輸出]: {args[0].AsString()}");
            }
            return LpcValue.Create(0);
        }

        // 【Efun 2】debug_int(int val) - 方便我們印出數值
        [Efun("debug_int")]
        public static LpcValue DebugInt(LpcValue[] args) {
            if (args.Length > 0 && args[0].Type == LpcType.Int) {
                Console.WriteLine($"🔢 [LPC 數值]: {args[0].AsInt()}");
            }
            return LpcValue.Create(0);
        }

        // 【Efun 3】calculate_damage(int atk, int def) - 呼叫 C 庫！
        [Efun("calculate_damage")]
        public static LpcValue CalculateDamage(LpcValue[] args) {
            int atk = args[0].AsInt();
            int def = args[1].AsInt();
            
            // 🔥 跨語言呼叫：C# 直接調用 C 語言編譯的 .so 庫！
            int dmg = calc_damage(atk, def); 
            
            return LpcValue.Create(dmg);
        }
    }
}
