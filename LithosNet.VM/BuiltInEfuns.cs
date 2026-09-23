using System;
using LithosNet.Core;

namespace LithosNet.VM {
    public static class BuiltInEfuns {
        
        // 【Efun 1】debug_message(string msg)
        [Efun("debug_message")]
        public static LpcValue DebugMessage(LpcValue[] args) {
            if (args.Length > 0 && args[0].Type == LpcType.String) {
                Console.WriteLine($"💬 [LPC 輸出]: {args[0].AsString()}");
            }
            return LpcValue.Create(0);
        }

        // 【Efun 2】sizeof(mixed val) - 致敬我們曾經踩過的坑！
        [Efun("sizeof")]
        public static LpcValue Sizeof(LpcValue[] args) {
            if (args.Length > 0) {
                if (args[0].Type == LpcType.String) return LpcValue.Create(args[0].AsString().Length);
            }
            return LpcValue.Create(0);
        }
    }
}
