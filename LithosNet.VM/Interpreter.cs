#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LithosNet.Core;

namespace LithosNet.VM {
    public class ReturnSignal : Exception { public LpcValue Value; public ReturnSignal(LpcValue v) { Value = v; } }

    public class Interpreter {
        private readonly Scope _scope;
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
            if (_scope.HasFunction(name)) {
                var func = _scope.GetFunction(name);
                for (int i = 0; i < func.Parameters.Count && i < args.Count; i++) _scope.Set(func.Parameters[i].Name, args[i]);
                try { foreach (var s in func.Body) Visit(s); } catch (ReturnSignal r) { return r.Value; }
                return LpcValue.Create(0);
            }
            if (EfunRegistry.TryGet(name, out var efun)) return efun(args.ToArray());
            throw new Exception($"[VM] Function '{name}' not found in current scope.");
        }

        private void Visit(AstNode node) {
            switch (node) {
                case VariableDeclarationNode v: _scope.Set(v.VariableName, v.Initializer != null ? Eval(v.Initializer) : LpcValue.Create(0)); break;
                case AssignmentNode a: _scope.Set(a.VariableName, Eval(a.Value)); break;
                case IndexAssignmentNode ia:
                    var col = Eval(ia.Array); var idx = Eval(ia.Index); var val = Eval(ia.Value);
                    if (col.Type == LpcType.Array) col.AsArray()[idx.AsInt()] = val;
                    else if (col.Type == LpcType.Mapping) col.AsMapping()[idx.AsString()] = val;
                    break;
                case FunctionDeclarationNode f: _scope.RegisterFunction(f); break;
                case ReturnNode r: throw new ReturnSignal(r.Value != null ? Eval(r.Value) : LpcValue.Create(0));
                case IfNode i: if (EvalBool(i.Condition)) Visit(i.ThenBranch); else if (i.ElseBranch != null) Visit(i.ElseBranch); break;
                case WhileNode w: while (EvalBool(w.Condition)) Visit(w.Body); break;
                case ForNode f2: 
                    if (f2.Init != null) Visit(f2.Init); 
                    while (f2.Condition == null || EvalBool(f2.Condition)) { Visit(f2.Body); if (f2.Step != null) Visit(f2.Step); } 
                    break;
                case BlockNode b: foreach (var s in b.Statements) Visit(s); break;
                default: Eval(node); break;
            }
        }

        private bool EvalBool(AstNode node) {
            var val = Eval(node);
            return val.Type == LpcType.Int && val.AsInt() != 0;
        }

        // 【核心魔法】存檔與讀檔邏輯
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
            Console.WriteLine($"💾 [VM] 已存檔: {path}");
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
            Console.WriteLine($"📂 [VM] 已讀檔: {path}");
            return true;
        }

        private LpcValue Eval(AstNode node) {
            switch (node) {
                case LiteralNode l: return l.Value;
                case VariableRefNode v: return _scope.Get(v.Name);
                case FunctionCallNode c: 
                    var cArgs = new List<LpcValue>(); foreach (var a in c.Arguments) cArgs.Add(Eval(a));
                    
                    if (c.Name == "this_object") return LpcValue.Create(this.ObjectName);
                    if (c.Name == "objectp" && cArgs.Count >= 1) return LpcValue.Create(_objMgr.ObjectExists(cArgs[0].AsString()) ? 1 : 0);
                    
                    if (c.Name == "call_out" && cArgs.Count >= 2) {
                        string funcName = cArgs[0].AsString();
                        int delaySec = cArgs[1].AsInt();
                        var passArgs = cArgs.Skip(2).ToList();
                        string targetObj = this.ObjectName; 
                        Task.Run(async () => {
                            await Task.Delay(delaySec * 1000);
                            _objMgr.CallFunction(targetObj, funcName, passArgs.ToArray());
                        });
                        return LpcValue.Create(1);
                    }
                    
                    if (c.Name == "clone_object" && cArgs.Count >= 1) {
                        return LpcValue.Create(_objMgr.Clone(cArgs[0].AsString()));
                    }

                    if (c.Name == "send_to_user" && cArgs.Count >= 1) {
                        Task.Run(() => SessionManager.SendAsync(this.ObjectName, cArgs[0].AsString()));
                        return LpcValue.Create(1);
                    }

                    // 【核心】攔截 save_object 與 restore_object
                    if (c.Name == "save_object" && cArgs.Count >= 1) {
                        SaveScope(cArgs[0].AsString());
                        return LpcValue.Create(1);
                    }
                    if (c.Name == "destruct" && cArgs.Count >= 1) {
                        _objMgr.DestructObject(cArgs[0].AsString());
                        return LpcValue.Create(1);
                    }
                    if (c.Name == "restore_object" && cArgs.Count >= 1) {
                        bool success = RestoreScope(cArgs[0].AsString());
                        return LpcValue.Create(success ? 1 : 0);
                    }

                    return CallFunction(c.Name, cArgs);
                case CallOtherNode co:
                    var coArgs = new List<LpcValue>(); foreach (var a in co.Arguments) coArgs.Add(Eval(a));
                    string targetObjName = Eval(co.Target).AsString();
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
                    if (col2.Type == LpcType.Mapping) {
                        var m = col2.AsMapping(); string k = idx2.AsString();
                        return m.ContainsKey(k) ? m[k] : LpcValue.Create(0);
                    }
                    return LpcValue.Create(0);
                case LogicalOpNode l:
                    bool lBool = EvalBool(l.Left);
                    if (l.Op == "&&") return LpcValue.Create(lBool && EvalBool(l.Right) ? 1 : 0);
                    if (l.Op == "||") return LpcValue.Create(lBool || EvalBool(l.Right) ? 1 : 0);
                    return LpcValue.Create(0);
                case BinaryOpNode b:
                    var left = Eval(b.Left); var right = Eval(b.Right);
                    if (b.Op == "+") {
                        if (left.Type == LpcType.Int && right.Type == LpcType.Int) return LpcValue.Create(left.AsInt() + right.AsInt());
                        if (left.Type == LpcType.String || right.Type == LpcType.String) {
                            string lStr = left.Type == LpcType.String ? left.AsString() : left.ToString();
                            string rStr = right.Type == LpcType.String ? right.AsString() : right.ToString();
                            return LpcValue.Create(lStr + rStr);
                        }
                    }
                    if (b.Op == "-" && left.Type == LpcType.Int && right.Type == LpcType.Int) return LpcValue.Create(left.AsInt() - right.AsInt());
                    if (b.Op == "*" && left.Type == LpcType.Int && right.Type == LpcType.Int) return LpcValue.Create(left.AsInt() * right.AsInt());
                    if (b.Op == "/" && left.Type == LpcType.Int && right.Type == LpcType.Int) return LpcValue.Create(right.AsInt() != 0 ? left.AsInt() / right.AsInt() : 0);
                    if (b.Op == "%" && left.Type == LpcType.Int && right.Type == LpcType.Int) return LpcValue.Create(right.AsInt() != 0 ? left.AsInt() % right.AsInt() : 0);
                    if (left.Type == LpcType.Int && right.Type == LpcType.Int) {
                        int lVal = left.AsInt(), rVal = right.AsInt();
                        bool res = b.Op switch { "==" => lVal == rVal, "!=" => lVal != rVal, "<" => lVal < rVal, ">" => lVal > rVal, "<=" => lVal <= rVal, ">=" => lVal >= rVal, _ => false };
                        return LpcValue.Create(res ? 1 : 0);
                    }
                    if (left.Type == LpcType.String && right.Type == LpcType.String) {
                        bool res = b.Op == "==" ? left.AsString() == right.AsString() : left.AsString() != right.AsString();
                        return LpcValue.Create(res ? 1 : 0);
                    }
                    return LpcValue.Create(0);
                default: return LpcValue.Create(0);
            }
        }
    }
}
