#nullable disable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LithosNet.Core;

namespace LithosNet.VM {
    public static class BuiltInEfuns {

        // 【MMORPG 通訊】json_decode (將 JSON 字串轉為 Mapping)
        [Efun("json_decode")]
        public static LpcValue JsonDecode(EfunContext ctx, LpcValue[] args) {
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
        public static LpcValue TaskSleep(EfunContext ctx, LpcValue[] args) {
            if (args.Length < 2) return LpcValue.Create(0);
            int ms = args[0].AsInt();
            string callback = args[1].AsString();
            string targetObj = ctx.CurrentObject ?? "";
            
            // 利用 C# 原生的 Task.Delay 實現非阻塞掛起
            System.Threading.Tasks.Task.Run(async () => {
                await System.Threading.Tasks.Task.Delay(ms);
                if (ctx.ObjMgr.ObjectExists(targetObj)) {
                    ctx.ObjMgr.CallFunction(targetObj, callback);
                }
            });
            return LpcValue.Create(1);
        }

        // 【Mudlib 基礎】to_int 字串轉整數
        [Efun("to_int")]
        public static LpcValue ToInt(EfunContext ctx, LpcValue[] args) {
            if (args.Length > 0 && args[0].Type == LpcType.String) {
                if (int.TryParse(args[0].AsString(), out int res)) return LpcValue.Create(res);
            } else if (args.Length > 0 && args[0].Type == LpcType.Int) return args[0];
            return LpcValue.Create(0);
        }

        // 【MMORPG Efun】註冊實體到空間網格
        [Efun("map_register")]
        public static LpcValue MapRegister(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 3) GridMapManager.Register(args[0].AsString(), args[1].AsInt(), args[2].AsInt());
            return LpcValue.Create(1);
        }

        // 【MMORPG Efun】移動實體
        [Efun("map_move")]
        public static LpcValue MapMove(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 3) GridMapManager.MoveWithAOI(ctx.ObjMgr, args[0].AsString(), args[1].AsInt(), args[2].AsInt());
            return LpcValue.Create(1);
        }

        // 【MMORPG Efun】AOE 範圍查詢 (效能碾壓 rAthena 的核心)
        [Efun("get_objects_in_radius")]
        public static LpcValue GetObjectsInRadius(EfunContext ctx, LpcValue[] args) {
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
        public static LpcValue Sprintf(EfunContext ctx, LpcValue[] args) {
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
        public static LpcValue SendToUser(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) {
                string obj = ctx.CurrentObject ?? "";
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




        
        // ==========================================
        // 🎲 【MMORPG 核心】隨機與數學矩陣
        // ==========================================
        private static readonly System.Random _rng = new System.Random();
        
        [Efun("random")]
        public static LpcValue RandomEfun(EfunContext ctx, LpcValue[] args) {
            if (args.Length < 1) return LpcValue.Create(0);
            int max = args[0].AsInt();
            return LpcValue.Create(max > 0 ? _rng.Next(max) : 0);
        }

        [Efun("abs")]
        public static LpcValue AbsEfun(EfunContext ctx, LpcValue[] args) {
            if (args.Length < 1) return LpcValue.Create(0);
            return LpcValue.Create(System.Math.Abs(args[0].AsInt()));
        }

        // ==========================================
        // 🔪 【MMORPG 核心】字串處理
        // ==========================================
        [Efun("replace_string")]
        public static LpcValue ReplaceString(EfunContext ctx, LpcValue[] args) {
            if (args.Length < 3) return LpcValue.Create("");
            return LpcValue.Create(args[0].AsString().Replace(args[1].AsString(), args[2].AsString()));
        }

        // ==========================================
        // ⏱️ 【MMORPG 核心】時間矩陣
        // ==========================================
        [Efun("time")]
        public static LpcValue TimeEfun(EfunContext ctx, LpcValue[] args) {
            return LpcValue.Create((int)(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        }

        
        // ==========================================
        // 🎒 【MMORPG 核心】Mapping (字典) 操作矩陣
        // ==========================================
        [Efun("keys")]
        public static LpcValue Keys(EfunContext ctx, LpcValue[] args) {
            Console.WriteLine($"🔍 [Efun X-Ray] keys() arg count: {args.Length}, arg0 Type: {(args.Length > 0 ? args[0].Type.ToString() : "NONE")}");
            if (args.Length < 1 || args[0].Type != LpcType.Mapping) return LpcValue.Create(new System.Collections.Generic.List<LpcValue>());
            var dict = args[0].AsMapping();
            var list = new System.Collections.Generic.List<LpcValue>();
            foreach(var k in dict.Keys) list.Add(LpcValue.Create(k));
            return LpcValue.Create(list);
        }

        [Efun("values")]
        public static LpcValue Values(EfunContext ctx, LpcValue[] args) {
            if (args.Length < 1 || args[0].Type != LpcType.Mapping) return LpcValue.Create(new System.Collections.Generic.List<LpcValue>());
            var dict = args[0].AsMapping();
            var list = new System.Collections.Generic.List<LpcValue>();
            foreach(var v in dict.Values) list.Add(v);
            return LpcValue.Create(list);
        }

        [Efun("m_delete")]
        public static LpcValue MDelete(EfunContext ctx, LpcValue[] args) {
            if (args.Length < 2 || args[0].Type != LpcType.Mapping) return args[0];
            var dict = args[0].AsMapping();
            dict.Remove(args[1].AsString());
            return args[0];
        }

        [Efun("element_of")]
        public static LpcValue ElementOf(EfunContext ctx, LpcValue[] args) {
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

        [Efun("shout")]
        public static LpcValue Shout(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) {
                foreach(var s in SessionManager.GetAllSessions()) SessionManager.SendAsync(s, args[0].AsString()).GetAwaiter().GetResult();
            }
            return LpcValue.Create(1);
        }

        // 【God Mode FFI】LuaJIT 風格的 native_call
        // [Efun("native_call")] // DEFUSED: Depends on missing libcombat.so
        public static LpcValue NativeCall(EfunContext ctx, LpcValue[] args) {
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
        public static LpcValue DebugMessage(EfunContext ctx, LpcValue[] args) {
            if (args.Length > 0) {
                Console.WriteLine($"💬 [LPC]: {args[0].AsString()}");
            } else {
                Console.WriteLine($"💬 [LPC]: ");
            }
            return LpcValue.Create(1);
        }

        [Efun("debug_int")]
        public static LpcValue DebugInt(EfunContext ctx, LpcValue[] args) { if (args.Length > 0 && args[0].Type == LpcType.Int) Console.WriteLine($"🔢 [LPC]: {args[0].AsInt()}"); return LpcValue.Create(0); }
        // [Efun("calculate_damage")] // DEFUSED: Depends on missing libcombat.so
        public static LpcValue CalculateDamage(EfunContext ctx, LpcValue[] args) { return LpcValue.Create(calc_damage(args[0].AsInt(), args[1].AsInt())); }
        [Efun("sizeof")]
        public static LpcValue Sizeof(EfunContext ctx, LpcValue[] args) { if (args.Length > 0) { if (args[0].Type == LpcType.String) return LpcValue.Create(args[0].AsString().Length); if (args[0].Type == LpcType.Array) return LpcValue.Create(args[0].AsArray().Count); if (args[0].Type == LpcType.Mapping) return LpcValue.Create(args[0].AsMapping().Count); } return LpcValue.Create(0); }
        [Efun("users")]
        public static LpcValue Users(EfunContext ctx, LpcValue[] args) { var list = new List<LpcValue>(); foreach (var u in SessionManager.GetAllSessions()) list.Add(LpcValue.Create(u)); return LpcValue.Create(list); }
        [Efun("explode")]
        public static LpcValue Explode(EfunContext ctx, LpcValue[] args) {
            if (args.Length < 2) return LpcValue.Create(new List<LpcValue>());
            
            string str = args[0].AsString();
            string delimiter = args[1].AsString();
            
            string[] parts = string.IsNullOrEmpty(delimiter) 
                ? new[] { str } 
                : str.Split(new[] { delimiter }, StringSplitOptions.None);
            
            var list = new List<LpcValue>(parts.Length);
            foreach (var p in parts) {
                list.Add(LpcValue.Create(p));
            }
            return LpcValue.Create(list);
        }
        [Efun("implode")]
        public static LpcValue Implode(EfunContext ctx, LpcValue[] args) {
            if (args.Length < 2) return LpcValue.Create("");
            
            // 優化：利用 LINQ 與 C# 底層高度優化的 string.Join，實現零冗餘的陣列拼接
            var arr = args[0].AsArray();
            string delimiter = args[1].AsString();
            
            return LpcValue.Create(string.Join(delimiter, arr.Select(v => v.AsString())));
        }
        [Efun("this_object")]
        public static LpcValue ThisObject(EfunContext ctx, LpcValue[] args) {
            return LpcValue.Create(ctx.CurrentObject ?? "unknown");
        }

        [Efun("write")]
        public static LpcValue Write(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) {
                string msg = args[0].AsString();
                // 【Phase 86】輸出到對應的 TCP session
                string target = Interpreter.CommandGiver ?? ctx.CurrentObject;
                var session = SessionManager.GetSessionByObject(target) ?? ctx.Session;
                if (session != null) {
                    session.Write(msg);
                } else {
                    // 降級：輸出到 console (例如 preload 階段)
                    Console.Write(msg);
                }
                return LpcValue.Create(1);
            }
            return LpcValue.Create(0);
        }


        [Efun("tell_object")]
        public static LpcValue TellObject(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 2) {
                SessionManager.SendAsync(args[0].AsString(), args[1].AsString()).GetAwaiter().GetResult();
            }
            return LpcValue.Create(1);
        }

        [Efun("input_to")]
        public static LpcValue InputTo(EfunContext ctx, LpcValue[] args) {
            Console.WriteLine($"[input_to DEBUG] Session is null? {ctx.Session == null}, Obj: {ctx.CurrentObject}");
            if (args.Length >= 1 && ctx.Session != null) {
                string funcName = args[0].AsString();
                ctx.Session.InputToFunc = funcName;
                ctx.Session.InputToObj = ctx.CurrentObject;
                Console.WriteLine($"[input_to] OK: {funcName} on {ctx.CurrentObject}");
                return LpcValue.Create(1);
            }
            Console.WriteLine($"[input_to] FAIL: Session null or no args");
            return LpcValue.Create(0);
        }


        [Efun("objectp")]
        public static LpcValue Objectp(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) {
                var scope = ctx.ObjMgr.GetScope(args[0].AsString());
                return LpcValue.Create((scope != null && !scope.IsDestructed) ? 1 : 0);
            }
            return LpcValue.Create(0);
        }
        [Efun("clone_object")]
        public static LpcValue CloneObject(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) {
                try {
                    // 提取除了 blueprint 之外的所有參數，傳遞給 create
                    var createArgs = args.Length > 1 ? args.Skip(1).ToArray() : null;
                    return LpcValue.Create(ctx.ObjMgr.Clone(args[0].AsString(), createArgs));
                } catch (System.Exception ex) {
                    System.Console.WriteLine($"[CloneObject] Failed gracefully: {ex.Message}");
                    return LpcValue.Create(0);
                }
            }
            return LpcValue.Create(0);
        }

        [Efun("exec")]
        public static LpcValue Exec(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 2) {
                Console.WriteLine($"[EXEC DIAG] args[0].Type={args[0].Type}, AsString={args[0].AsString()}");
                if (args[0].Type == LpcType.Int && args[0].AsInt() == 0) {
                    Console.WriteLine("[EXEC] Rejecting 0");
                    return LpcValue.Create(0);
                }
                string newObjName = args[0].AsString();
                string oldObjName = args[1].AsString();
                
                Console.WriteLine($"🔌 [Efun:exec] 嘗試將連線從 '{oldObjName}' 轉移至 '{newObjName}'");
                
                // 1. 轉移 Session 綁定
                SessionManager.TransferSession(oldObjName, newObjName);
                
                // 2. 更新 ObjectManager 中的 Interpreter Session 綁定
                if (ctx.Session != null) {
                    ctx.ObjMgr.BindSessionToObject(newObjName, ctx.Session);
                }
                
                return LpcValue.Create(1);
            }
            return LpcValue.Create(0);
        }


        [Efun("destruct")]
        public static LpcValue Destruct(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) {
                string objName = args[0].AsString();
                var scope = ctx.ObjMgr.GetScope(objName);
                if (scope != null && !scope.IsDestructed) {
                    // T2.3 修復：如果該物件綁定了 Session，先關閉連線
                    if (SessionManager.HasSession(objName)) {
                        Console.WriteLine($"[Destruct] Closing interactive session for '{objName}'");
                        SessionManager.CloseSession(objName);
                    }
                    ctx.ObjMgr.DestructObject(objName);
                    return LpcValue.Create(1);
                }
            }
            return LpcValue.Create(0);
        }

        [Efun("call_other")]
        public static LpcValue CallOther(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 2) {
                var callArgs = args.Length > 2 ? args[2..] : new LpcValue[0];
                try { return ctx.ObjMgr.CallFunction(args[0].AsString(), args[1].AsString(), callArgs); } 
                catch { return LpcValue.Create(0); }
            }
            return LpcValue.Create(0);
        }

        [Efun("load_object")]
        public static LpcValue LoadObject(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) {
                try {
                    string actualName = ctx.ObjMgr.LoadObjectAndGetActualName(args[0].AsString());
                    if (actualName != null) {
                        return LpcValue.Create(actualName);
                    }
                } catch (System.Exception ex) {
                    Console.WriteLine($"[load_object] Failed: {ex.Message}");
                }
            }
            return LpcValue.Create(0);
        }


        [Efun("set_environment")]
        public static LpcValue SetEnvironment(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1 && ctx.CurrentScope != null) {
                ctx.CurrentScope.Environment = args[0].AsString();
                return LpcValue.Create(1);
            }
            return LpcValue.Create(0);
        }

        [Efun("environment")]
        public static LpcValue GetEnvironment(EfunContext ctx, LpcValue[] args) { // 改名避免與 System.Environment 衝突
            return LpcValue.Create(ctx.CurrentScope?.Environment ?? "");
        }

        [Efun("throw")]
        public static LpcValue ThrowEfun(EfunContext ctx, LpcValue[] args) { // 改名避免與 C# throw 關鍵字衝突
            throw new LpcThrowException(args.Length >= 1 ? args[0] : LpcValue.Create(0));
        }

        [Efun("save_object")]
        public static LpcValue SaveObject(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1 && ctx.CurrentScope != null) {
                string path = System.IO.Path.Combine("save", args[0].AsString() + ".json");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                var dict = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, object>>();
                foreach (var kvp in ctx.CurrentScope.GetAllVariablesDeep()) {
                    var val = kvp.Value;
                    dict[kvp.Key] = new System.Collections.Generic.Dictionary<string, object> {
                        { "type", val.Type.ToString() },
                        { "value", val.Type == LpcType.String ? val.AsString() : (object)val.AsInt() }
                    };
                }
                System.IO.File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(dict));
                return LpcValue.Create(1);
            }
            return LpcValue.Create(0);
        }

        [Efun("restore_object")]
        public static LpcValue RestoreObject(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1 && ctx.CurrentScope != null) {
                string path = System.IO.Path.Combine("save", args[0].AsString() + ".json");
                if (!System.IO.File.Exists(path)) return LpcValue.Create(0);
                var dict = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>>(System.IO.File.ReadAllText(path));
                foreach (var kvp in dict) {
                    string typeStr = kvp.Value.GetProperty("type").GetString();
                    if (typeStr == "String") ctx.CurrentScope.Set(kvp.Key, LpcValue.Create(kvp.Value.GetProperty("value").GetString()));
                    else if (typeStr == "Int") ctx.CurrentScope.Set(kvp.Key, LpcValue.Create(kvp.Value.GetProperty("value").GetInt32()));
                }
                return LpcValue.Create(1);
            }
            return LpcValue.Create(0);
        }
        [Efun("set_heart_beat")]
        public static LpcValue SetHeartBeat(EfunContext ctx, LpcValue[] args) {
            // FluffOS 標準：set_heart_beat(int flag)，預設作用於 this_object()
            if (args.Length >= 1 && ctx.CurrentObject != null) {
                bool enable = args[0].AsInt() != 0;
                ctx.ObjMgr.SetHeartBeat(ctx.CurrentObject, enable);
                return LpcValue.Create(1);
            }
            return LpcValue.Create(0);
        }
        [Efun("move")]
        public static LpcValue Move(EfunContext ctx, LpcValue[] args) {
            // FluffOS 標準: move(mixed dest, object ob) 
            // 如果只給一個參數，預設移動 this_object()
            string destName = args.Length >= 1 ? args[0].AsString() : "";
            string objName = args.Length >= 2 ? args[1].AsString() : ctx.CurrentObject;
            
            if (string.IsNullOrEmpty(destName) || string.IsNullOrEmpty(objName)) {
                return LpcValue.Create(0);
            }
            
            int result = ctx.ObjMgr.MoveObject(objName, destName);
            return LpcValue.Create(result);
        }
        [Efun("set_reset")]
        public static LpcValue SetReset(EfunContext ctx, LpcValue[] args) {
            // FluffOS 標準：set_reset(int flag)，預設作用於 this_object()
            if (args.Length >= 1 && ctx.CurrentObject != null) {
                bool enable = args[0].AsInt() != 0;
                ctx.ObjMgr.SetReset(ctx.CurrentObject, enable);
                return LpcValue.Create(1);
            }
            return LpcValue.Create(0);
        }






        
        // ==========================================
        // 【Phase 77: 補齊核心基礎 Efun】
        // ==========================================
        [Efun("to_string")]
        public static LpcValue ToString(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) return LpcValue.Create(args[0].ToString());
            return LpcValue.Create("");
        }

        [Efun("member_array")]
        public static LpcValue MemberArray(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 2 && args[1].Type == LpcType.Array) {
                var target = args[0];
                var arr = args[1].AsArray();
                for (int i = 0; i < arr.Count; i++) {
                    if (arr[i].Equals(target)) return LpcValue.Create(i);
                }
            }
            return LpcValue.Create(-1);
        }

        [Efun("clonep")]
        public static LpcValue Clonep(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) {
                string objName = args[0].AsString();
                var scope = ctx.ObjMgr.GetScope(objName);
                return LpcValue.Create(scope != null && objName.Contains("#") ? 1 : 0);
            }
            return LpcValue.Create(0);
        }

        [Efun("call_out")]
        public static LpcValue CallOut(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 2 && ctx.CurrentObject != null) {
                string funcName = args[0].AsString();
                int delay = args[1].AsInt();
                var callArgs = args.Length > 2 ? new System.Collections.Generic.List<LpcValue>(args[2..]) : new System.Collections.Generic.List<LpcValue>();
                ctx.ObjMgr.ScheduleCallOut(ctx.CurrentObject, funcName, delay, callArgs);
                return LpcValue.Create(1); // 返回 1 表示成功調度
            }
            return LpcValue.Create(0);
        }

        [Efun("remove_call_out")]
        public static LpcValue RemoveCallOut(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1 && ctx.CurrentObject != null) {
                string funcNameOrHandle = args[0].AsString();
                bool removed = ctx.ObjMgr.RemoveCallOut(ctx.CurrentObject, funcNameOrHandle);
                return LpcValue.Create(removed ? 1 : 0);
            }
            return LpcValue.Create(0);
        }

        
        // ==========================================
        // 【Phase 80: 物件遍歷與查找核心 Efun】
        // ==========================================
        [Efun("find_object")]
        public static LpcValue FindObject(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) {
                string fileName = args[0].AsString();
                var scope = ctx.ObjMgr.GetScope(fileName);
                if (scope != null && !scope.IsDestructed) {
                    return LpcValue.Create(fileName);
                }
            }
            return LpcValue.Create(0);
        }

        [Efun("all_inventory")]
        public static LpcValue AllInventory(EfunContext ctx, LpcValue[] args) {
            // FluffOS 標準：若不傳參數，預設為 this_object()
            string envName = args.Length >= 1 && args[0].Type == LpcType.String ? args[0].AsString() : ctx.CurrentObject;
            
            if (string.IsNullOrEmpty(envName)) {
                return LpcValue.Create(new System.Collections.Generic.List<LpcValue>());
            }
            
            var invList = ctx.ObjMgr.GetInventory(envName);
            var result = new System.Collections.Generic.List<LpcValue>();
            
            foreach (var itemName in invList) {
                var scope = ctx.ObjMgr.GetScope(itemName);
                if (scope != null && !scope.IsDestructed) {
                    result.Add(LpcValue.Create(itemName));
                }
            }
            return LpcValue.Create(result);
        }

        [Efun("present")]
        public static LpcValue Present(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) {
                string searchStr = args[0].AsString();
                string envName = args.Length >= 2 ? args[1].AsString() : ctx.CurrentObject;
                
                if (string.IsNullOrEmpty(envName)) return LpcValue.Create(0);

                var invList = ctx.ObjMgr.GetInventory(envName);
                foreach (var itemName in invList) {
                    var scope = ctx.ObjMgr.GetScope(itemName);
                    if (scope != null && !scope.IsDestructed) {
                        try {
                            var result = ctx.ObjMgr.CallFunction(itemName, "id", new LpcValue[] { LpcValue.Create(searchStr) });
                            if (result.Type == LpcType.Int && result.AsInt() != 0) {
                                return LpcValue.Create(itemName);
                            }
                        } catch {
                            // 如果物件沒有 id 函數，忽略並繼續尋找
                        }
                    }
                }
            }
            return LpcValue.Create(0);
        }

        
        // ==========================================
        // 【Phase 80.2: 檔案系統核心 Efun】
        // ==========================================
        [Efun("file_size")]
        public static LpcValue FileSize(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) {
                string path = args[0].AsString();
                if (System.IO.File.Exists(path)) {
                    return LpcValue.Create((int)new System.IO.FileInfo(path).Length);
                }
                if (System.IO.Directory.Exists(path)) {
                    return LpcValue.Create(-2); // FluffOS 標準：目錄回傳 -2
                }
                return LpcValue.Create(-1); // FluffOS 標準：不存在回傳 -1
            }
            return LpcValue.Create(-1);
        }

        [Efun("read_file")]
        public static LpcValue ReadFile(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) {
                string path = args[0].AsString();
                
                // 【Phase 84.1 安全閘道】呼叫 master->valid_read()
                try {
                    // 【關鍵修復】caller 參數暫時傳入 0，避免 object 與 string 的型別檢查衝突
                    var masterResult = ctx.ObjMgr.CallFunction("master", "valid_read", new LpcValue[] {
                        LpcValue.Create(path),
                        LpcValue.Create("read_file"),
                        LpcValue.Create(0) 
                    });
                    
                    if (masterResult.Type == LpcType.Int && masterResult.AsInt() == 0) {
                        return LpcValue.Create(0); // valid_read 拒絕
                    }
                } catch {
                    // 【FluffOS 降級行為】如果 master 沒有 valid_read 或發生錯誤，預設允許讀取
                }

                try {
                    // 將虛擬路徑轉換為物理路徑
                    string physicalPath = path;
                    if (path.StartsWith("/") && ctx.ObjMgr.Config != null) {
                        physicalPath = ctx.ObjMgr.Config.ResolveMudlibPath(path);
                    }
                    
                    if (System.IO.File.Exists(physicalPath)) {
                        return LpcValue.Create(System.IO.File.ReadAllText(physicalPath));
                    }
                } catch {
                    // 讀取失敗回傳 0
                }
            }
            return LpcValue.Create(0);
        }


        [Efun("write_file")]
        public static LpcValue WriteFile(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 2) {
                string path = args[0].AsString();
                string data = args[1].AsString();
                
                // 【Phase 84.2 安全閘道】呼叫 master->valid_write()
                try {
                    var masterResult = ctx.ObjMgr.CallFunction("master", "valid_write", new LpcValue[] {
                        LpcValue.Create(path),
                        LpcValue.Create("write_file"),
                        LpcValue.Create(0)
                    });
                    
                    if (masterResult.Type == LpcType.Int && masterResult.AsInt() == 0) {
                        return LpcValue.Create(0); // valid_write 拒絕
                    }
                } catch {
                    // 【FluffOS 降級行為】如果 master 沒有 valid_write，預設允許
                }

                try {
                    // 將虛擬路徑轉換為物理路徑
                    string physicalPath = path;
                    if (path.StartsWith("/") && ctx.ObjMgr.Config != null) {
                        physicalPath = ctx.ObjMgr.Config.ResolveMudlibPath(path);
                    }
                    
                    // 確保目錄存在
                    string dir = System.IO.Path.GetDirectoryName(physicalPath);
                    if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir)) {
                        System.IO.Directory.CreateDirectory(dir);
                    }
                    
                    System.IO.File.AppendAllText(physicalPath, data);
                    return LpcValue.Create(1);
                } catch {
                    return LpcValue.Create(0);
                }
            }
            return LpcValue.Create(0);
        }


        [Efun("mkdir")]
        public static LpcValue Mkdir(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) {
                string path = args[0].AsString();
                try {
                    System.IO.Directory.CreateDirectory(path);
                    return LpcValue.Create(1);
                } catch {
                    return LpcValue.Create(0);
                }
            }
            return LpcValue.Create(0);
        }

        [Efun("rm")]
        public static LpcValue Rm(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) {
                string path = args[0].AsString();
                try {
                    if (System.IO.File.Exists(path)) {
                        System.IO.File.Delete(path);
                        return LpcValue.Create(1);
                    }
                    if (System.IO.Directory.Exists(path)) {
                        System.IO.Directory.Delete(path, true); // true = recursive
                        return LpcValue.Create(1);
                    }
                } catch {
                    return LpcValue.Create(0);
                }
            }
            return LpcValue.Create(0);
        }
        
        // ==========================================
        // 【Phase 80.3: 字串處理與類型檢查核心 Efun (缺失部分)】
        // ==========================================
        [Efun("replace")]
        public static LpcValue Replace(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 3) return LpcValue.Create(args[0].AsString().Replace(args[1].AsString(), args[2].AsString()));
            return LpcValue.Create(args.Length >= 1 ? args[0].AsString() : "");
        }

        [Efun("lower_case")]
        public static LpcValue LowerCase(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) return LpcValue.Create(args[0].AsString().ToLower());
            return LpcValue.Create("");
        }

        [Efun("upper_case")]
        public static LpcValue UpperCase(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) return LpcValue.Create(args[0].AsString().ToUpper());
            return LpcValue.Create("");
        }

        [Efun("strlen")]
        public static LpcValue Strlen(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) return LpcValue.Create(args[0].AsString().Length);
            return LpcValue.Create(0);
        }

        [Efun("stringp")]
        public static LpcValue Stringp(EfunContext ctx, LpcValue[] args) {
            return LpcValue.Create(args.Length >= 1 && args[0].Type == LpcType.String ? 1 : 0);
        }

        [Efun("intp")]
        public static LpcValue Intp(EfunContext ctx, LpcValue[] args) {
            return LpcValue.Create(args.Length >= 1 && args[0].Type == LpcType.Int ? 1 : 0);
        }

        [Efun("arrayp")]
        public static LpcValue Arrayp(EfunContext ctx, LpcValue[] args) {
            return LpcValue.Create(args.Length >= 1 && args[0].Type == LpcType.Array ? 1 : 0);
        }

        
        // ==========================================
        // 【Phase 81.2: 4 大高頻核心 Efun 補齊】
        // ==========================================
        [Efun("allocate")]
        public static LpcValue Allocate(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1 && args[0].Type == LpcType.Int) {
                int size = args[0].AsInt();
                var arr = new System.Collections.Generic.List<LpcValue>();
                LpcValue def = args.Length >= 2 ? args[1] : LpcValue.Create(0);
                for (int i = 0; i < size; i++) arr.Add(def);
                return LpcValue.Create(arr);
            }
            return LpcValue.Create(new System.Collections.Generic.List<LpcValue>());
        }

        [Efun("get_dir")]
        public static LpcValue GetDir(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) {
                string path = args[0].AsString();
                string dir = System.IO.Path.GetDirectoryName(path) ?? ".";
                string pattern = System.IO.Path.GetFileName(path) ?? "*";
                if (string.IsNullOrEmpty(dir)) dir = ".";
                
                var result = new System.Collections.Generic.List<LpcValue>();
                try {
                    foreach (var f in System.IO.Directory.GetFiles(dir, pattern)) {
                        result.Add(LpcValue.Create(System.IO.Path.GetFileName(f)));
                    }
                    foreach (var d in System.IO.Directory.GetDirectories(dir, pattern)) {
                        result.Add(LpcValue.Create(System.IO.Path.GetFileName(d) + "/"));
                    }
                } catch {}
                return LpcValue.Create(result);
            }
            return LpcValue.Create(new System.Collections.Generic.List<LpcValue>());
        }

        [Efun("map_array")]
        public static LpcValue MapArray(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 2 && args[0].Type == LpcType.Array && args[1].Type == LpcType.String) {
                var arr = args[0].AsArray();
                string func = args[1].AsString();
                var result = new System.Collections.Generic.List<LpcValue>();
                foreach (var item in arr) {
                    try {
                        var res = ctx.ObjMgr.CallFunction(ctx.CurrentObject, func, new LpcValue[] { item });
                        result.Add(res);
                    } catch {
                        result.Add(item);
                    }
                }
                return LpcValue.Create(result);
            }
            return args.Length >= 1 ? args[0] : LpcValue.Create(new System.Collections.Generic.List<LpcValue>());
        }

        [Efun("sscanf")]
        public static LpcValue Sscanf(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 2) {
                string str = args[0].AsString();
                string fmt = args[1].AsString();
                
                string regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(fmt)
                    .Replace("%s", "(.*?)")
                    .Replace("%d", "(\\d+)")
                    .Replace("%O", "(.*?)")
                    .Replace("%f", "([\\d\\.]+)") + "$";
                
                var match = System.Text.RegularExpressions.Regex.Match(str, regexPattern);
                if (match.Success) {
                    int matchCount = match.Groups.Count - 1;
                    for (int i = 0; i < System.Math.Min(matchCount, args.Length - 2); i++) {
                        string valStr = match.Groups[i + 1].Value;
                        LpcValue assignVal;
                        if (int.TryParse(valStr, out int intVal) && fmt.Contains("%d")) {
                            assignVal = LpcValue.Create(intVal);
                        } else {
                            assignVal = LpcValue.Create(valStr);
                        }
                        string varName = args[i + 2].AsString();
                        ctx.CurrentScope.Set(varName, assignVal);
                    }
                    return LpcValue.Create(matchCount);
                }
            }
            return LpcValue.Create(0);
        }
        [Efun("get_tick")]
        public static LpcValue GetTick(EfunContext ctx, LpcValue[] args) { return LpcValue.Create(Environment.TickCount); }
    
        // ==========================================
        // 【Phase 79: C# 原生 FFI Efun】
        // 利用 P/Invoke 直接穿透虛擬機，呼叫作業系統底層 C API (libc)
        // ==========================================

        [DllImport("libc", EntryPoint = "getpid", SetLastError = true)]
        private static extern int native_getpid();

        [DllImport("libc", EntryPoint = "gethostname", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern int native_gethostname(byte[] name, int len);

        [Efun("native_getpid")]
        public static LpcValue NativeGetPid(EfunContext ctx, LpcValue[] args) {
            return LpcValue.Create(native_getpid());
        }

        [Efun("native_gethostname")]
        public static LpcValue NativeGetHostName(EfunContext ctx, LpcValue[] args) {
            byte[] buffer = new byte[256];
            int result = native_gethostname(buffer, buffer.Length);
            if (result == 0) {
                string hostname = System.Text.Encoding.ASCII.GetString(buffer).TrimEnd('\0');
                return LpcValue.Create(hostname);
            }
            return LpcValue.Create("unknown");
        }


        // ==========================================
        // 【Phase 85】高頻 🔥 Efun 批量注入 (正確 API 版)
        // ==========================================

        // --- 字串處理 ---
        [Efun("strsrch")]
        public static LpcValue Strsrch(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 2) {
                string str = args[0].AsString();
                string substr = args[1].AsString();
                int flag = args.Length >= 3 ? args[2].AsInt() : 0;
                
                if (flag == 0) {
                    int idx = str.IndexOf(substr, StringComparison.Ordinal);
                    return LpcValue.Create(idx);
                } else {
                    int idx = str.LastIndexOf(substr, StringComparison.Ordinal);
                    return LpcValue.Create(idx);
                }
            }
            return LpcValue.Create(-1);
        }

        [Efun("capitalize")]
        public static LpcValue Capitalize(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) {
                string str = args[0].AsString();
                if (string.IsNullOrEmpty(str)) return LpcValue.Create("");
                return LpcValue.Create(char.ToUpper(str[0]) + str.Substring(1));
            }
            return LpcValue.Create("");
        }

        [Efun("trim")]
        public static LpcValue Trim(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) {
                string str = args[0].AsString();
                int trimMask = args.Length >= 2 ? args[1].AsInt() : 3;
                string chrs = args.Length >= 3 ? args[2].AsString() : " \t\n\r";
                
                char[] trimChars = string.IsNullOrEmpty(chrs) ? new char[] { ' ', '\t', '\n', '\r' } : chrs.ToCharArray();
                if (trimMask == 1) return LpcValue.Create(str.TrimStart(trimChars));
                if (trimMask == 2) return LpcValue.Create(str.TrimEnd(trimChars));
                return LpcValue.Create(str.Trim(trimChars));
            }
            return LpcValue.Create("");
        }

        // --- 陣列操作 ---
        [Efun("sort_array")]
        public static LpcValue SortArray(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1 && args[0].Type == LpcType.Array) {
                var arr = new System.Collections.Generic.List<LpcValue>(args[0].AsArray());
                
                if (args.Length >= 2 && args[1].Type == LpcType.String) {
                    string funcName = args[1].AsString();
                    string callerObj = ctx.CurrentObject ?? "";
                    arr.Sort((a, b) => {
                        try {
                            var result = ctx.ObjMgr.CallFunction(callerObj, funcName, new LpcValue[] { a, b });
                            return result.AsInt();
                        } catch {
                            return 0;
                        }
                    });
                } else {
                    arr.Sort((a, b) => {
                        if (a.Type == LpcType.Int && b.Type == LpcType.Int) return a.AsInt().CompareTo(b.AsInt());
                        return a.AsString().CompareTo(b.AsString());
                    });
                }
                // 【關鍵修復】使用 List<LpcValue> 建立陣列
                return LpcValue.Create(arr);
            }
            return LpcValue.Create(new System.Collections.Generic.List<LpcValue>());
        }

        [Efun("filter_array")]
        public static LpcValue FilterArray(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 2 && args[0].Type == LpcType.Array) {
                var arr = args[0].AsArray();
                string funcName = args[1].AsString();
                string callerObj = ctx.CurrentObject ?? "";
                var result = new System.Collections.Generic.List<LpcValue>();
                
                foreach (var item in arr) {
                    try {
                        var keep = ctx.ObjMgr.CallFunction(callerObj, funcName, new LpcValue[] { item });
                        if (keep.AsInt() != 0) result.Add(item);
                    } catch {
                        // 過濾失敗時跳過
                    }
                }
                return LpcValue.Create(result);
            }
            return LpcValue.Create(new System.Collections.Generic.List<LpcValue>());
        }

        [Efun("unique_array")]
        public static LpcValue UniqueArray(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1 && args[0].Type == LpcType.Array) {
                var arr = args[0].AsArray();
                var seen = new System.Collections.Generic.HashSet<string>();
                var result = new System.Collections.Generic.List<LpcValue>();
                
                foreach (var item in arr) {
                    string key = item.AsString();
                    if (seen.Add(key)) result.Add(item);
                }
                return LpcValue.Create(result);
            }
            return LpcValue.Create(new System.Collections.Generic.List<LpcValue>());
        }

        // --- 數學運算 ---
        [Efun("max")]
        public static LpcValue Max(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 2) {
                if (args[0].Type == LpcType.Int && args[1].Type == LpcType.Int) {
                    return LpcValue.Create(System.Math.Max(args[0].AsInt(), args[1].AsInt()));
                }
                double a = Convert.ToDouble(args[0].AsString());
                double b = Convert.ToDouble(args[1].AsString());
                return LpcValue.Create((int)System.Math.Max(a, b));
            }
            return LpcValue.Create(0);
        }

        [Efun("min")]
        public static LpcValue Min(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 2) {
                if (args[0].Type == LpcType.Int && args[1].Type == LpcType.Int) {
                    return LpcValue.Create(System.Math.Min(args[0].AsInt(), args[1].AsInt()));
                }
                double a = Convert.ToDouble(args[0].AsString());
                double b = Convert.ToDouble(args[1].AsString());
                return LpcValue.Create((int)System.Math.Min(a, b));
            }
            return LpcValue.Create(0);
        }

        // --- 類型檢查 ---
        [Efun("mappingp")]
        public static LpcValue Mappingp(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) return LpcValue.Create(args[0].Type == LpcType.Mapping ? 1 : 0);
            return LpcValue.Create(0);
        }

        [Efun("nullp")]
        public static LpcValue Nullp(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) return LpcValue.Create(args[0].Type == LpcType.Int && args[0].AsInt() == 0 ? 1 : 0);
            return LpcValue.Create(1);
        }

        [Efun("floatp")]
        public static LpcValue Floatp(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) return LpcValue.Create(args[0].Type == LpcType.Float ? 1 : 0);
            return LpcValue.Create(0);
        }

        [Efun("functionp")]
        public static LpcValue Functionp(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) return LpcValue.Create(args[0].Type == LpcType.Function ? 1 : 0);
            return LpcValue.Create(0);
        }

        // --- 檔案系統 ---
        [Efun("file_exists")]
        public static LpcValue FileExists(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 1) {
                string path = args[0].AsString();
                if (path.StartsWith("/") && ctx.ObjMgr.Config != null) {
                    path = ctx.ObjMgr.Config.ResolveMudlibPath(path);
                }
                return LpcValue.Create(System.IO.File.Exists(path) ? 1 : 0);
            }
            return LpcValue.Create(0);
        }

        [Efun("cp")]
        public static LpcValue Cp(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 2) {
                try {
                    string src = args[0].AsString();
                    string dst = args[1].AsString();
                    if (src.StartsWith("/") && ctx.ObjMgr.Config != null) src = ctx.ObjMgr.Config.ResolveMudlibPath(src);
                    if (dst.StartsWith("/") && ctx.ObjMgr.Config != null) dst = ctx.ObjMgr.Config.ResolveMudlibPath(dst);
                    System.IO.File.Copy(src, dst, true);
                    return LpcValue.Create(1);
                } catch { return LpcValue.Create(0); }
            }
            return LpcValue.Create(0);
        }

        [Efun("mv")]
        public static LpcValue Mv(EfunContext ctx, LpcValue[] args) {
            if (args.Length >= 2) {
                try {
                    string src = args[0].AsString();
                    string dst = args[1].AsString();
                    if (src.StartsWith("/") && ctx.ObjMgr.Config != null) src = ctx.ObjMgr.Config.ResolveMudlibPath(src);
                    if (dst.StartsWith("/") && ctx.ObjMgr.Config != null) dst = ctx.ObjMgr.Config.ResolveMudlibPath(dst);
                    System.IO.File.Move(src, dst, true);
                    return LpcValue.Create(1);
                } catch { return LpcValue.Create(0); }
            }
            return LpcValue.Create(0);
        }

        // --- 時間處理 ---
        [Efun("ctime")]
        public static LpcValue Ctime(EfunContext ctx, LpcValue[] args) {
            long timestamp = args.Length >= 1 ? args[0].AsInt() : (long)(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var dt = System.DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
            return LpcValue.Create(dt.ToString("ddd MMM dd HH:mm:ss yyyy"));
        }


        // 【Phase 86】input_to efun：註冊輸入回調

    }
}
