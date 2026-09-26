#nullable disable
using System;
using System.Collections.Generic;
using LithosNet.Core;

namespace LithosNet.VM {
    public class Scope {
        private readonly Dictionary<string, LpcValue> _variables = new();
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
            foreach (var kvp in parent._variables) if (!_variables.ContainsKey(kvp.Key)) _variables[kvp.Key] = kvp.Value;
            foreach (var kvp in parent._functions) if (!_functions.ContainsKey(kvp.Key)) _functions[kvp.Key] = kvp.Value;
        }
        
        public Dictionary<string, LpcValue> GetAllVariables() => _variables;
        public Dictionary<string, FunctionDeclarationNode> GetFunctions() => _functions;
    }
}
