#nullable disable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LithosNet.Core;

namespace LithosNet.VM {
    public static class BuiltInEfuns {

        // 【MMORPG Fiber】非阻塞式異步延遲 (不卡死主線程)
        [Efun("task_sleep")]
        public static LpcValue TaskSleep(LpcValue[] args) {
            if (args.Length < 2) return LpcValue.Create(0);
            int ms = args[0].AsInt();
            string callback = args[1].AsString();
            string targetObj = SessionManager.CurrentPlayer.Value ?? "";
            
            // 利用 C# 原生的 Task.Delay 實現非阻塞掛起
            System.Threading.Tasks.Task.Run(async () => {
                await System.Threading.Tasks.Task.Delay(ms);
                if (ObjectManager.Instance.ObjectExists(targetObj)) {
                    ObjectManager.Instance.CallFunction(targetObj, callback);
                }
            });
            return LpcValue.Create(1);
        }


        // 【Mudlib 基礎】to_int 字串轉整數
        [Efun("to_int")]
        public static LpcValue ToInt(LpcValue[] args) {
            if (args.Length > 0 && args[0].Type == LpcType.String) {
                if (int.TryParse(args[0].AsString(), out int res)) return LpcValue.Create(res);
            } else if (args.Length > 0 && args[0].Type == LpcType.Int) return args[0];
            return LpcValue.Create(0);
        }


        // 【MMORPG Efun】註冊實體到空間網格
        [Efun("map_register")]
        public static LpcValue MapRegister(LpcValue[] args) {
            if (args.Length >= 3) GridMapManager.Register(args[0].AsString(), args[1].AsInt(), args[2].AsInt());
            return LpcValue.Create(1);
        }

        // 【MMORPG Efun】移動實體
        [Efun("map_move")]
        public static LpcValue MapMove(LpcValue[] args) {
            if (args.Length >= 3) GridMapManager.MoveWithAOI(ObjectManager.Instance, args[0].AsString(), args[1].AsInt(), args[2].AsInt());
            return LpcValue.Create(1);
        }

        // 【MMORPG Efun】AOE 範圍查詢 (效能碾壓 rAthena 的核心)
        [Efun("get_objects_in_radius")]
        public static LpcValue GetObjectsInRadius(LpcValue[] args) {
            if (args.Length < 3) return LpcValue.Create(new List<LpcValue>());
            var list = GridMapManager.GetObjectsInRadius(args[0].AsInt(), args[1].AsInt(), args[2].AsInt());
            var lpcList = new List<LpcValue>();
            foreach(var s in list) lpcList.Add(LpcValue.Create(s));
            return LpcValue.Create(lpcList);
        }


        // 【FFI 底層委託定義】
        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private delegate int IntReturnDelegate();

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private delegate IntPtr StringArgReturnIntPtrDelegate([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPStr)] string arg);

        // 【Mudlib 靈魂】sprintf 格式化輸出
        [Efun("sprintf")]
        public static LpcValue Sprintf(LpcValue[] args) {
            if (args.Length < 1) return LpcValue.Create("");
            string fmt = args[0].AsString();
            int argIdx = 1;
            var sb = new System.Text.StringBuilder();
            for(int i=0; i<fmt.Length; i++) {
                if (fmt[i] == '%' && i+1 < fmt.Length && argIdx < args.Length) {
                    char next = fmt[i+1];
                    if (next == 's' || next == 'd' || next == 'i') {
                        var val = args[argIdx];
                        sb.Append(val.Type == LpcType.String ? val.AsString() : val.ToString());
                        argIdx++; i++; continue;
                    }
                }
                sb.Append(fmt[i]);
            }
            return LpcValue.Create(sb.ToString());
        }

        // 【Mudlib 通訊】tell_object 與 shout (使用 GetAwaiter 確保同步執行)
        [Efun("tell_object")]
        public static LpcValue TellObject(LpcValue[] args) {
            if (args.Length >= 2) SessionManager.SendAsync(args[0].AsString(), args[1].AsString()).GetAwaiter().GetResult();
            return LpcValue.Create(1);
        }

        [Efun("shout")]
        public static LpcValue Shout(LpcValue[] args) {
            if (args.Length >= 1) {
                foreach(var s in SessionManager.GetAllSessions()) SessionManager.SendAsync(s, args[0].AsString()).GetAwaiter().GetResult();
            }
            return LpcValue.Create(1);
        }

        // 【God Mode FFI】LuaJIT 風格的 native_call
        [Efun("native_call")]
        public static LpcValue NativeCall(LpcValue[] args) {
            if (args.Length < 2) return LpcValue.Create(0);
            string lib = args[0].AsString();
            string func = args[1].AsString();
            try {
                IntPtr handle = System.Runtime.InteropServices.NativeLibrary.Load(lib);
                IntPtr ptr = System.Runtime.InteropServices.NativeLibrary.GetExport(handle, func);
                if (func == "getpid" || func == "time") {
                    var del = (IntReturnDelegate)System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(ptr, typeof(IntReturnDelegate));
                    return LpcValue.Create(del());
                }
                if (func == "getenv" && args.Length >= 3) {
                    var del = (StringArgReturnIntPtrDelegate)System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(ptr, typeof(StringArgReturnIntPtrDelegate));
                    IntPtr res = del(args[2].AsString());
                    return LpcValue.Create(System.Runtime.InteropServices.Marshal.PtrToStringAnsi(res) ?? "");
                }
                return LpcValue.Create(0);
            } catch (Exception ex) {
                Console.WriteLine($"⚠️ FFI 錯誤: {ex.Message}");
                return LpcValue.Create(0);
            }
        }

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
