#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LithosNet.Core;

namespace LithosNet.VM {
    public class BreakSignal : Exception { }

    public class LpcRuntimeException : Exception {
        public LpcRuntimeException(string msg) : base(msg) { }
    }

    public class ReturnSignal : Exception { public LpcValue Value; public ReturnSignal(LpcValue v) { Value = v; } }

    public class Interpreter {
        private Scope _scope;
        private readonly ObjectManager _objMgr;
        public string ObjectName { get; set; } 

        public Interpreter(Scope scope, ObjectManager objMgr) { _scope = scope; _objMgr = objMgr; }

        public void Execute(List<AstNode> ast) { 
            foreach (var n in ast) {
                if (n is InheritNode inh) {
                    Scope parentScope = _objMgr.LoadObject(inh.ParentObjName);
                    _scope.InheritFrom(parentScope);
                } else { Visit(n); }
            }
        }

        public LpcValue CallFunction(string name, List<LpcValue> args) {
            var compiled = _scope.GetCompiled(name);

            // 🔥【極致效能】優先呼叫 JIT 編譯後的 Delegate (納秒級跳轉)
            // var compiled (Duplicate removed) = _scope.GetCompiled(name);
            if (compiled != null) {
                int arg1 = args.Count > 0 ? args[0].AsInt() : 0;
                int arg2 = args.Count > 1 ? args[1].AsInt() : 0;
                int result = ((Func<int, int, int>)compiled).Invoke(arg1, arg2);
                return LpcValue.Create(result);
            }

            if (_scope.HasFunction(name)) {
                var func = _scope.GetFunction(name);
                
                // 【創世 Local Scope】建立獨立區域作用域，防止遞迴污染與參數覆蓋
                var localScope = new Scope();
                localScope.Parent = _scope; 
                var prevScope = _scope;
                _scope = localScope;
                
                try {
                    for (int i = 0; i < func.Parameters.Count && i < args.Count; i++) {
                        _scope.Set(func.Parameters[i].Name, args[i]);
                    }
                    foreach (var s in func.Body) {
                        Visit(s); 
                    }
                } catch (ReturnSignal r) {
                    _scope = prevScope;
                    return r.Value; 
                } finally {
                    _scope = prevScope; // 完美恢復 Scope
                }
                return LpcValue.Create(0);
            }
            if (EfunRegistry.TryGet(name, out var efun)) return efun(args.ToArray());
            throw new Exception($"[VM] Function '{name}' not found in current scope.");
        }

        private void Visit(AstNode node) {
            switch (node) {
                case VariableDeclarationNode v: 
                    if (v.Initializer != null) {
                        _scope.Set(v.VariableName, Eval(v.Initializer));
                    } else if (!_scope.Has(v.VariableName)) {
                        // 【參數保護】只有在變數不存在(不是參數)時，才初始化為 0
                        _scope.Set(v.VariableName, LpcValue.Create(0));
                    }
                    break;
                case AssignmentNode a:  var _assignVal = Eval(a.Value);
                    _scope.Set(a.VariableName, _assignVal); break;
                case IndexAssignmentNode ia:
                    var col = Eval(ia.Array); var idx = Eval(ia.Index); var val = Eval(ia.Value);
                    if (col.Type == LpcType.Array) col.AsArray()[idx.AsInt()] = val;
                    else if (col.Type == LpcType.Mapping) {
                        var map = col.AsMapping();
                        map[idx.AsString()] = val;
                    }
                    break;
                case FunctionDeclarationNode f: _scope.RegisterFunction(f); break;
                case ReturnNode r:
                    throw new ReturnSignal(r.Value != null ? Eval(r.Value) : LpcValue.Create(0));
                case IfNode i: if (EvalBool(i.Condition)) Visit(i.ThenBranch); else if (i.ElseBranch != null) Visit(i.ElseBranch); break;
                case SwitchNode sw:
                    var swCond = Eval(sw.Condition);
                    bool swMatched = false;
                    SwitchCaseNode defaultCase = null;
                    foreach(var c in sw.Cases) {
                        if (c.IsDefault) { defaultCase = c; continue; }
                        if (!swMatched) {
                            var caseVal = Eval(c.Value);
                            if ((swCond.Type == LpcType.Int && caseVal.Type == LpcType.Int && swCond.AsInt() == caseVal.AsInt()) ||
                                (swCond.Type == LpcType.String && caseVal.Type == LpcType.String && swCond.AsString() == caseVal.AsString())) {
                                swMatched = true;
                            }
                        }
                        if (swMatched) {
                            try { foreach(var s in c.Body) Visit(s); } catch (BreakSignal) { break; }
                        }
                    }
                    if (!swMatched && defaultCase != null) {
                        try { foreach(var s in defaultCase.Body) Visit(s); } catch (BreakSignal) { }
                    }
                    break;
                case BreakNode:
                    throw new BreakSignal();

                case WhileNode w: while (EvalBool(w.Condition)) Visit(w.Body); break;
                case ForNode f2: 
                    if (f2.Init != null) Visit(f2.Init); 
                    while (f2.Condition == null || EvalBool(f2.Condition)) { Visit(f2.Body); if (f2.Step != null) Visit(f2.Step); } 
                    break;
                case ForeachNode fe:
                    var feCol = Eval(fe.Collection);
                    if (feCol.Type == LpcType.Array) {
                        foreach (var item in feCol.AsArray()) { 
                            Console.WriteLine($"   -> Reading Item: Type={item.Type}, AsInt={item.AsInt()}");
                            _scope.Set(fe.VarName, item); 
                            Visit(fe.Body); 
                        } 
                    }
                    else if (feCol.Type == LpcType.Mapping) { foreach (var kvp in feCol.AsMapping()) { _scope.Set(fe.VarName, LpcValue.Create(kvp.Key)); Visit(fe.Body); } }
                    break;
                case BlockNode b: foreach (var s in b.Statements) Visit(s); break;
                default: Eval(node); break;
            }
        }

        private bool EvalBool(AstNode node) {
            var val = Eval(node);
            if (val.Type == LpcType.Int) return val.AsInt() != 0;
            if (val.Type == LpcType.String) return !string.IsNullOrEmpty(val.AsString());
            if (val.Type == LpcType.Object) return val.AsString() != ""; // Object ID 不為空即為 true
            if (val.Type == LpcType.Array) return val.AsArray().Count > 0;
            return false;
        }

        private void SaveScope(string filename) {
            var dict = new Dictionary<string, Dictionary<string, object>>();
            foreach (var kvp in _scope.GetAllVariables()) {
                var val = kvp.Value;
                var entry = new Dictionary<string, object> { {"type", val.Type.ToString()} };
                if (val.Type == LpcType.Int) entry["value"] = val.AsInt();
                else if (val.Type == LpcType.String) entry["value"] = val.AsString();
                dict[kvp.Key] = entry;
            }
            string json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
            string path = Path.Combine("/home/tiny/LithosNet/mudlib/save", filename + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);
        }

        private bool RestoreScope(string filename) {
            string path = Path.Combine("/home/tiny/LithosNet/mudlib/save", filename + ".json");
            if (!File.Exists(path)) return false;
            string json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            foreach (var kvp in dict) {
                string typeStr = kvp.Value.GetProperty("type").GetString();
                if (typeStr == "Int") _scope.Set(kvp.Key, LpcValue.Create(kvp.Value.GetProperty("value").GetInt32()));
                else if (typeStr == "String") _scope.Set(kvp.Key, LpcValue.Create(kvp.Value.GetProperty("value").GetString()));
            }
            return true;
        }

        private LpcValue Eval(AstNode node) {
            switch (node) {
                case LiteralNode l: return l.Value;
                case VariableRefNode v: var val = _scope.Get(v.Name); Console.WriteLine($"🔍 [VarRef X-Ray] Reading \'{v.Name}\' -> Type: {val.Type}, Val: {val.AsInt()}"); return val;
                case FunctionPointerNode fp: return LpcValue.CreateFunction(this.ObjectName, fp.FuncName);
                case FunctionCallNode c:
                    // 【特殊形式】catch 必須延遲求值，否則 throw 會在參數準備階段就崩潰！
                    if (c.Name == "catch") {
                        try {
                            if (c.Arguments != null && c.Arguments.Count > 0) {
                                Eval(c.Arguments[0]); // 在 try 區塊內安全執行
                            }

                            return LpcValue.Create(0); // 沒有錯誤，返回 0
                        } catch (LpcRuntimeException ex) {
                            return LpcValue.Create(ex.Message); // 完美捕獲！
                        } catch (Exception ex) {
                            return LpcValue.Create("Runtime Error: " + ex.Message);
                        }
                    }
 
                    var cArgs = new List<LpcValue>(); foreach (var a in c.Arguments) cArgs.Add(Eval(a));
                    
                                        if (c.Name == "exec" && cArgs.Count >= 2) {
                        SessionManager.Exec(cArgs[0].AsString(), cArgs[1].AsString());
                        return LpcValue.Create(1);
                    }
                    if (c.Name == "load_object" && cArgs.Count >= 1) {
                        _objMgr.LoadObject(cArgs[0].AsString());
                        return LpcValue.Create(cArgs[0].AsString());
                    }

                    // 【核心路由】throw 必須在這裡被攔截！
                    if (c.Name == "throw") {
                        string errMsg = "Unknown Error";
                        if (cArgs.Count > 0) errMsg = cArgs[0].AsString();
                        throw new LpcRuntimeException(errMsg);
                    }

                    // 【創世魔法】map_array 高階函數
                    if (c.Name == "map_array" && cArgs.Count >= 2) {
                        var arr = cArgs[0].AsArray();
                        var funcVal = cArgs[1];
                        var result = new System.Collections.Generic.List<LpcValue>();
                        if (funcVal.Type == LpcType.Function) {
                            var funcTuple = funcVal.AsFunction(); // Tuple<objName, funcName>
                            foreach(var item in arr) {
                                var res = _objMgr.CallFunction(funcTuple.Item1, funcTuple.Item2, item);
                                result.Add(res);
                            }
                        } else {
                            // 寬容模式：如果不是函數，直接返回原陣列
                            return cArgs[0]; 
                        }
                        return LpcValue.Create(result);
                    }

                                                            // 【Phase 50 Efun】bot_say: 讓 Bot 說話 (C# 骨架處理廣播，LPC 負責呼叫)
                    if (c.Name == "bot_say" && cArgs.Count >= 2) {
                        var botObj = cArgs[0];
                        string msg = cArgs[1].AsString();
                        if (botObj.Type == LpcType.Object) {
                            string botId = botObj.AsString();
                            Console.WriteLine($"🤖 [Bot Efun] {botId} says: {msg}");
                            // 未來可在此接入 AOI 廣播系統：SpaceManager.Broadcast(botId, msg);
                        }
                        return LpcValue.Create(1);
                    }

                    
                    // 【FluffOS 相容】query_name: 獲取物件名稱
                    Console.WriteLine($"🔍 [Efun X-Ray] Calling: '{c.Name}' with {cArgs.Count} args");
                    if (c.Name == "query_name") {
                        string name = this.ObjectName ?? "unknown";
                        return LpcValue.Create(name);
                    }
                    if (c.Name == "environment") {
                        string env = _scope.Has("environment") ? _scope.Get("environment").AsString() : "";
                        return LpcValue.Create(env);
                    }

                    // 【MMORPG 核心】present: 檢查某個物件是否在當前環境中
                    if (c.Name == "present" && cArgs.Count >= 1) {
                        string targetId = cArgs[0].AsString();
                        // 簡單實現：檢查 _objMgr 中是否存在該 ID
                        bool exists = _objMgr.ObjectExists(targetId);
                        return LpcValue.Create(exists ? 1 : 0);
                    }

                    // 【MMORPG 核心】all_inventory: 獲取當前環境內的所有物件
                    if (c.Name == "all_inventory") {
                        var inv = new List<LpcValue>();
                        // 這裡需要從 SpaceManager 或 ObjectManager 獲取當前房間的 inventory
                        // 暫時返回空陣列作為佔位，後續接入 SpaceManager
                        return LpcValue.Create(inv);
                    }

                    // 【FluffOS 相容】random: 隨機數生成器 (已存在，但確保語義一致)
                    if (c.Name == "random" && cArgs.Count >= 1) {
                        int max = cArgs[0].AsInt();
                        return LpcValue.Create(new Random().Next(max));
                    }

                    // 【MMORPG 核心】sprintf: 格式化字串 (FluffOS 核心)
                    if (c.Name == "sprintf" && cArgs.Count >= 1) {
                        string fmt = cArgs[0].AsString();
                        int argIdx = 1;
                        System.Text.StringBuilder sb = new System.Text.StringBuilder();
                        for (int i = 0; i < fmt.Length; i++) {
                            if (fmt[i] == '%' && i + 1 < fmt.Length && argIdx < cArgs.Count) {
                                char spec = fmt[i+1];
                                if (spec == 's' || spec == 'd' || spec == 'O' || spec == 'x') {
                                    sb.Append(cArgs[argIdx].AsString());
                                    argIdx++;
                                    i++; // 跳過格式符
                                    continue;
                                }
                            }
                            sb.Append(fmt[i]);
                        }
                        return LpcValue.Create(sb.ToString());
                    }

                    if (c.Name == "spawn_bot" && cArgs.Count >= 1) {
                        string blueprint = cArgs[0].AsString();
                        string cloneId = _objMgr.Clone(blueprint);
                        SessionManager.RegisterBot(cloneId);
                        return LpcValue.CreateObject(cloneId);
                    }

                    if (c.Name == "this_object") return LpcValue.Create(this.ObjectName);
                    if (c.Name == "this_player") return LpcValue.Create(SessionManager.CurrentPlayer.Value ?? "");
                    if (c.Name == "environment") return _scope.Has("environment") ? _scope.Get("environment") : LpcValue.Create("");
                    if (c.Name == "move" || c.Name == "move_object") { 
                        if (cArgs.Count >= 4) { SpaceManager.Move(cArgs[0].AsString(), cArgs[1].AsInt(), cArgs[2].AsInt(), cArgs[3].AsInt(), _objMgr); }
                        else if (cArgs.Count >= 3) { SpaceManager.Move(this.ObjectName, cArgs[0].AsInt(), cArgs[1].AsInt(), cArgs[2].AsInt(), _objMgr); }
                        return LpcValue.Create(1); 
                    }
                    if (c.Name == "get_objects_in_radius" && cArgs.Count >= 4) {
                        var radiusList = SpaceManager.GetObjectsInRadius(cArgs[0].AsInt(), cArgs[1].AsInt(), cArgs[2].AsInt(), cArgs[3].AsInt());
                        var lpcList = new System.Collections.Generic.List<LpcValue>(); 
                        foreach(var o in radiusList) lpcList.Add(LpcValue.Create(o));
                        return LpcValue.Create(lpcList);
                    }
                    if (c.Name == "all_inventory" && cArgs.Count >= 1) {
                        var inv = _objMgr.GetInventory(cArgs[0].AsString()); var invList = new List<LpcValue>(); foreach(var i in inv) invList.Add(LpcValue.Create(i)); return LpcValue.Create(invList);
                    }
                    if (c.Name == "message" && cArgs.Count >= 2) {
                        string env = _scope.Has("environment") ? _scope.Get("environment").AsString() : "";
                        string msg = cArgs[1].AsString(); var inv = _objMgr.GetInventory(env);
                        foreach(var obj in inv) { if (obj != this.ObjectName) { try { _objMgr.CallFunction(obj, "receive_message", LpcValue.Create(msg), LpcValue.Create(this.ObjectName)); } catch {} } }
                        return LpcValue.Create(1);
                    }
                    if (c.Name == "objectp" && cArgs.Count >= 1) return LpcValue.Create(_objMgr.ObjectExists(cArgs[0].AsString()) ? 1 : 0);
                    
                    // 🔥 觸發 JIT 編譯
                    if (c.Name == "compile_function" && cArgs.Count >= 1) {
                        string fName = cArgs[0].AsString();
                        var funcNode = _scope.GetFunction(fName);
                        var del = JitCompiler.Compile(funcNode);
                        _scope.SetCompiled(fName, del);
                        Console.WriteLine($"⚡ [JIT] 函數 {fName} 已編譯為原生 IL 機器碼！");
                        return LpcValue.Create(1);
                    }

                    if (c.Name == "call_out" && cArgs.Count >= 2) {
                        string funcName = cArgs[0].AsString(); 
                        int delaySec = cArgs[1].AsInt();
                        var passArgs = cArgs.Skip(2).ToArray(); 
                        int handle = CallOutManager.Schedule(this.ObjectName, funcName, delaySec, passArgs, _objMgr);
                        return LpcValue.Create(handle);
                    }
                    if (c.Name == "clone_object" && cArgs.Count >= 1) return LpcValue.Create(_objMgr.Clone(cArgs[0].AsString()));
                    if (c.Name == "remove_call_out" && cArgs.Count >= 1) { return LpcValue.Create(CallOutManager.Remove(this.ObjectName, cArgs[0].AsString())); }
                    if (c.Name == "destruct" && cArgs.Count >= 1) { 
                        string target = cArgs[0].AsString();
                        CallOutManager.ClearObject(target);
                        _objMgr.DestructObject(target); 
                        return LpcValue.Create(1); 
                    }
                    if (c.Name == "send_to_user" && cArgs.Count >= 1) { string target = this.ObjectName; // 【FluffOS 語意】嚴格發給當前執行的物件 (this_object)
                        Task.Run(() => SessionManager.SendAsync(target, cArgs[0].AsString())); return LpcValue.Create(1); }
                    if (c.Name == "save_object" && cArgs.Count >= 1) { SaveScope(cArgs[0].AsString()); return LpcValue.Create(1); }
                    if (c.Name == "restore_object" && cArgs.Count >= 1) return LpcValue.Create(RestoreScope(cArgs[0].AsString()) ? 1 : 0);
                    if (c.Name == "update_object" && cArgs.Count >= 1) {
                        string target = cArgs[0].AsString();
                        string path = "/home/tiny/LithosNet/mudlib/obj/" + target + ".c";
                        if (!File.Exists(path)) path = "/home/tiny/LithosNet/mudlib/room/" + target + ".c";
                        if (File.Exists(path)) { _objMgr.ReloadObject(path); return LpcValue.Create(1); }
                        return LpcValue.Create(0);
                    }
                    if (c.Name == "set_heart_beat" && cArgs.Count >= 1) { HeartbeatManager.SetHeartBeat(this.ObjectName, cArgs[0].AsInt() != 0); return LpcValue.Create(1); }

                    return CallFunction(c.Name, cArgs);
                case CallOtherNode co:
                    var coArgs = new List<LpcValue>(); foreach (var a in co.Arguments) coArgs.Add(Eval(a));
                    string targetObjName;
                    // 【FluffOS 經典特性】隱式物件引用：如果變數不存在，直接將其名稱視為物件 ID
                    if (co.Target is VariableRefNode vref && !_scope.Has(vref.Name)) targetObjName = vref.Name;
                    else targetObjName = Eval(co.Target).AsString();
                    return _objMgr.CallFunction(targetObjName, co.FuncName, coArgs.ToArray());
                case ArrayLiteralNode al:
                    var list = new List<LpcValue>(); foreach (var e in al.Elements) list.Add(Eval(e)); return LpcValue.Create(list);
                case MappingLiteralNode ml:
                    var dict = new Dictionary<string, LpcValue>();
                    for (int i = 0; i < ml.Keys.Count; i++) dict[Eval(ml.Keys[i]).AsString()] = Eval(ml.Values[i]);
                    return LpcValue.Create(dict);
                case IndexAccessNode ia:
                    var col2 = Eval(ia.Array); var idx2 = Eval(ia.Index);
                    if (col2.Type == LpcType.Array) return col2.AsArray()[idx2.AsInt()];
                    if (col2.Type == LpcType.Mapping) { var m = col2.AsMapping(); string k = idx2.AsString(); return m.ContainsKey(k) ? m[k] : LpcValue.Create(0); }
                    return LpcValue.Create(0);
                case LogicalOpNode l:
                    bool lBool = EvalBool(l.Left);
                    if (l.Op == "&&") return LpcValue.Create(lBool && EvalBool(l.Right) ? 1 : 0);
                    if (l.Op == "||") return LpcValue.Create(lBool || EvalBool(l.Right) ? 1 : 0);
                    return LpcValue.Create(0);
                case BinaryOpNode b:
                    b.Op = b.Op?.Trim();
                    var left = Eval(b.Left);
                    var right = Eval(b.Right);

                    // 【FluffOS/LDMud 標準】Type Promotion: String + Any = String
                    if (b.Op == "+") {
                        if (left.Type == LpcType.String || right.Type == LpcType.String)
                            return LpcValue.Create(left.AsString() + right.AsString());
                        if (left.Type == LpcType.Int && right.Type == LpcType.Int)
                            return LpcValue.Create(left.AsInt() + right.AsInt());
                        return LpcValue.Create(0);
                    }
                    if (b.Op == "-" && left.Type == LpcType.Int && right.Type == LpcType.Int)
                        return LpcValue.Create(left.AsInt() - right.AsInt());
                    if (b.Op == "*" && left.Type == LpcType.Int && right.Type == LpcType.Int)
                        return LpcValue.Create(left.AsInt() * right.AsInt());
                    if (b.Op == "/" && left.Type == LpcType.Int && right.Type == LpcType.Int)
                        return LpcValue.Create(right.AsInt() != 0 ? left.AsInt() / right.AsInt() : 0);
                    if (b.Op == "%" && left.Type == LpcType.Int && right.Type == LpcType.Int)
                        return LpcValue.Create(right.AsInt() != 0 ? left.AsInt() % right.AsInt() : 0);
                    if (left.Type == LpcType.String && right.Type == LpcType.String) {
                        bool res = b.Op == "==" ? left.AsString() == right.AsString() : left.AsString() != right.AsString();
                        return LpcValue.Create(res ? 1 : 0);
                    }
                    if (left.Type == LpcType.Int && right.Type == LpcType.Int) {
                        int lVal = left.AsInt(), rVal = right.AsInt();
                        bool res = b.Op switch { "==" => lVal == rVal, "!=" => lVal != rVal, "<" => lVal < rVal, ">" => lVal > rVal, "<=" => lVal <= rVal, ">=" => lVal >= rVal, _ => false };
                        return LpcValue.Create(res ? 1 : 0);
                    }
                    return LpcValue.Create(0);
