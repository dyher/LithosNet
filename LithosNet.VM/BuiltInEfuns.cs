#nullable disable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LithosNet.Core;

namespace LithosNet.VM {
    public static class BuiltInEfuns {
        public static ObjectManager ObjMgr;

        // 【MMORPG 通訊】json_decode (將 JSON 字串轉為 Mapping)
        [Efun("json_decode")]
        public static LpcValue JsonDecode(LpcValue[] args) {
            if (args.Length < 1) return LpcValue.Create(new Dictionary<string, LpcValue>());
            try {
                var doc = System.Text.Json.JsonDocument.Parse(args[0].AsString());
                var dict = new Dictionary<string, LpcValue>();
                foreach (var prop in doc.RootElement.EnumerateObject()) {
                    if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Number)
                        dict[prop.Name] = LpcValue.Create(prop.Value.GetInt32());
                    else if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                        dict[prop.Name] = LpcValue.Create(prop.Value.GetString());
                    else
                        dict[prop.Name] = LpcValue.Create(prop.Value.ToString());
                }
                return LpcValue.Create(dict);
            } catch { return LpcValue.Create(new Dictionary<string, LpcValue>()); }
        }


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
        
        [Efun("send_to_user")]
        public static LpcValue SendToUser(LpcValue[] args) {
            if (args.Length >= 1) {
                string obj = SessionManager.CurrentPlayer.Value ?? "";
                if (string.IsNullOrEmpty(obj)) {
                    var all = SessionManager.GetAllSessions();
                    if (all.Count > 0) obj = all[0];
                }
                if (!string.IsNullOrEmpty(obj)) {
                    SessionManager.SendAsync(obj, args[0].AsString()).GetAwaiter().GetResult();
                }
            }
            return LpcValue.Create(1);
        }

        [Efun("this_object")]
        public static LpcValue ThisObject(LpcValue[] args) {
            return LpcValue.Create(SessionManager.CurrentPlayer.Value ?? "unknown");
        }

        [Efun("clone_object")]
        public static LpcValue CloneObject(LpcValue[] args) {
            if (args.Length >= 1) {
                string blueprint = args[0].AsString();
                string cloneName = blueprint + "#" + Guid.NewGuid().ToString().Substring(0, 4);
                try { ObjMgr.LoadObject(blueprint); } catch {}
                return LpcValue.Create(cloneName);
            }
            return LpcValue.Create(0);
        }

        [Efun("exec")]
        public static LpcValue Exec(LpcValue[] args) {
            if (args.Length >= 2) {
                SessionManager.Exec(args[0].AsString(), args[1].AsString());
            }
            return LpcValue.Create(1);
        }

        [Efun("destruct")]
        public static LpcValue Destruct(LpcValue[] args) {
            Console.WriteLine($"💥 [Efun] destruct({args[0].AsString()})");
            return LpcValue.Create(1);
        }

        
        // ==========================================
        // 🎲 【MMORPG 核心】隨機與數學矩陣
        // ==========================================
        private static readonly System.Random _rng = new System.Random();
        
        [Efun("random")]
        public static LpcValue RandomEfun(LpcValue[] args) {
            if (args.Length < 1) return LpcValue.Create(0);
            int max = args[0].AsInt();
            return LpcValue.Create(max > 0 ? _rng.Next(max) : 0);
        }

        [Efun("abs")]
        public static LpcValue AbsEfun(LpcValue[] args) {
            if (args.Length < 1) return LpcValue.Create(0);
            return LpcValue.Create(System.Math.Abs(args[0].AsInt()));
        }

        // ==========================================
        // 🔪 【MMORPG 核心】字串處理
        // ==========================================
        [Efun("replace_string")]
        public static LpcValue ReplaceString(LpcValue[] args) {
            if (args.Length < 3) return LpcValue.Create("");
            return LpcValue.Create(args[0].AsString().Replace(args[1].AsString(), args[2].AsString()));
        }

        // ==========================================
        // ⏱️ 【MMORPG 核心】時間矩陣
        // ==========================================
        [Efun("time")]
        public static LpcValue TimeEfun(LpcValue[] args) {
            return LpcValue.Create((int)(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        }

        
        // ==========================================
        // 🎒 【MMORPG 核心】Mapping (字典) 操作矩陣
        // ==========================================
        [Efun("keys")]
        public static LpcValue Keys(LpcValue[] args) {
            Console.WriteLine($"🔍 [Efun X-Ray] keys() arg count: {args.Length}, arg0 Type: {(args.Length > 0 ? args[0].Type.ToString() : "NONE")}");
            if (args.Length < 1 || args[0].Type != LpcType.Mapping) return LpcValue.Create(new System.Collections.Generic.List<LpcValue>());
            var dict = args[0].AsMapping();
            var list = new System.Collections.Generic.List<LpcValue>();
            foreach(var k in dict.Keys) list.Add(LpcValue.Create(k));
            return LpcValue.Create(list);
        }

        [Efun("values")]
        public static LpcValue Values(LpcValue[] args) {
            if (args.Length < 1 || args[0].Type != LpcType.Mapping) return LpcValue.Create(new System.Collections.Generic.List<LpcValue>());
            var dict = args[0].AsMapping();
            var list = new System.Collections.Generic.List<LpcValue>();
            foreach(var v in dict.Values) list.Add(v);
            return LpcValue.Create(list);
        }

        [Efun("m_delete")]
        public static LpcValue MDelete(LpcValue[] args) {
            if (args.Length < 2 || args[0].Type != LpcType.Mapping) return args[0];
            var dict = args[0].AsMapping();
            dict.Remove(args[1].AsString());
            return args[0];
        }

        [Efun("element_of")]
        public static LpcValue ElementOf(LpcValue[] args) {
            Console.WriteLine($"🔍 [Efun X-Ray] element_of() arg0 Type: {(args.Length > 0 ? args[0].Type.ToString() : "NONE")}");
            if (args.Length < 1) return LpcValue.Create(0);
            if (args[0].Type == LpcType.Array) {
                var arr = args[0].AsArray();
                return arr.Count > 0 ? arr[_rng.Next(arr.Count)] : LpcValue.Create(0);
            }
            if (args[0].Type == LpcType.Mapping) {
                var dict = args[0].AsMapping();
                if (dict.Count == 0) return LpcValue.Create(0);
                var keys = new System.Collections.Generic.List<string>(dict.Keys);
                return dict[keys[_rng.Next(keys.Count)]];
            }
            return LpcValue.Create(0);
        }

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
        public static LpcValue Implode(LpcValue[] args) {
            Console.WriteLine($"🔍 [Efun X-Ray] implode() arg count: {args.Length}, arg0 Type: {(args.Length > 0 ? args[0].Type.ToString() : "NONE")}"); if (args.Length < 2) return LpcValue.Create(""); var arr = args[0].AsArray(); var strings = new List<string>(); foreach (var v in arr) strings.Add(v.Type == LpcType.String ? v.AsString() : v.ToString()); return LpcValue.Create(string.Join(args[1].AsString(), strings)); }
        [Efun("get_tick")]
        public static LpcValue GetTick(LpcValue[] args) { return LpcValue.Create(Environment.TickCount); }
    }
}
