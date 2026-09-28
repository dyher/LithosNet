#nullable disable
using System;
using System.Collections.Generic;
using LithosNet.Core;

namespace LithosNet.VM {
    public class Scope {
        public bool IsDestructed { get; set; } = false;
        
        private readonly Dictionary<string, LpcValue> _variables = new();
        
        // 【Phase 66: 房間系統基礎】
        public string Environment { get; set; } = ""; // 當前所在的房間/容器物件名稱
        public readonly System.Collections.Generic.Dictionary<string, string> Actions = new(); // 【Phase 72: 指令路由註冊表】
        public readonly System.Collections.Generic.List<string> Inventory = new(); // 內部包含的物件名稱列表
        public Scope Parent { get; set; }
        private readonly Dictionary<string, FunctionDeclarationNode> _functions = new();
        private readonly Dictionary<string, Delegate> _compiledFunctions = new(); // 【JIT】原生 Delegate 快取

        public void Set(string name, LpcValue value) {
            if (_variables.ContainsKey(name)) {
                _variables[name] = value; // 區域變數或已存在的全域變數，直接更新當前 Scope
            } else if (Parent != null) {
                Parent.Set(name, value); // 【Phase 54.1 修復】全域變數委託給父級 (根) Scope 更新，防止局部 Scope 丟失
            } else {
                _variables[name] = value; // 根 Scope，直接創建
            }
        }
        public LpcValue Get(string name) {
            if (_variables.TryGetValue(name, out var v)) return v;
            if (Parent != null) return Parent.Get(name); // 【原型鏈】向上層查找
            return LpcValue.Create(0);
        }
        public void RegisterFunction(FunctionDeclarationNode func) => _functions[func.Name] = func;
        public FunctionDeclarationNode GetFunction(string name) {
            if (_functions.TryGetValue(name, out var f)) return f;
            if (Parent != null) return Parent.GetFunction(name); // 【原型鏈】向上層查找
            throw new Exception($"[VM] Function '{name}' not found.");
        }
        public bool HasFunction(string name) => _functions.ContainsKey(name) || (Parent?.HasFunction(name) ?? false);
        public bool Has(string name) => _variables.ContainsKey(name) || (Parent?.Has(name) ?? false);

        // 【JIT】存取編譯後的 Delegate
        public void SetCompiled(string name, Delegate del) => _compiledFunctions[name] = del;
        public Delegate GetCompiled(string name) => _compiledFunctions.TryGetValue(name, out var d) ? d : null;

        public void InheritFrom(Scope parent) {
            this.Parent = parent; // 【Phase 64】建立原型鏈，支援 :: 查找
            foreach (var kvp in parent._variables) if (!_variables.ContainsKey(kvp.Key)) _variables[kvp.Key] = kvp.Value;
            foreach (var kvp in parent._functions) if (!_functions.ContainsKey(kvp.Key)) _functions[kvp.Key] = kvp.Value;
        }
        
        public FunctionDeclarationNode GetParentFunction(string name) {
            if (Parent != null) return Parent.GetFunction(name);
            throw new Exception($"[VM] Parent function '{name}' not found in inheritance chain.");
        }
        public bool HasParentFunction(string name) => Parent?.HasFunction(name) ?? false;
        
        public Dictionary<string, LpcValue> GetAllVariables() => _variables;
        public Dictionary<string, FunctionDeclarationNode> GetFunctions() => _functions;
    }
}
