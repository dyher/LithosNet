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
        public Scope InheritedScope { get; set; } // 【Phase 82.4】專門用於 :: (Super Call) 查找的類繼承鏈
        private readonly Dictionary<string, FunctionDeclarationNode> _functions = new();

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
            if (Parent != null) {
                var parentFunc = Parent.GetFunction(name);
                if (parentFunc != null) return parentFunc;
            }
            if (InheritedScope != null) {
                var inheritedFunc = InheritedScope.GetFunction(name);
                if (inheritedFunc != null) return inheritedFunc;
            }
            throw new Exception($"[VM] Function '{name}' not found.");
        }
        public bool HasFunction(string name) => _functions.ContainsKey(name) || (Parent?.HasFunction(name) ?? false);
        public bool Has(string name) => _variables.ContainsKey(name) || (Parent?.Has(name) ?? false);

        // 【JIT】存取編譯後的 Delegate

        public void InheritFromClass(Scope parentClass) {
            this.InheritedScope = parentClass; // 【Phase 82.4】設定類繼承鏈，支援 :: 查找
            foreach (var kvp in parentClass._variables) if (!_variables.ContainsKey(kvp.Key)) _variables[kvp.Key] = kvp.Value;
            foreach (var kvp in parentClass._functions) if (!_functions.ContainsKey(kvp.Key)) _functions[kvp.Key] = kvp.Value;
        }
        
        public FunctionDeclarationNode GetParentFunction(string name) {
            if (InheritedScope != null) {
                var func = InheritedScope.GetFunction(name);
                if (func != null) return func;
            }
            if (Parent != null) return Parent.GetParentFunction(name); // 【Phase 82.11 修復】沿著局部作用域鏈向上查找類繼承鏈
            throw new Exception($"[VM] Parent function '{name}' not found in inheritance chain.");
        }
        public bool HasParentFunction(string name) {
            if (InheritedScope != null) return InheritedScope.HasFunction(name);
            if (Parent != null) return Parent.HasParentFunction(name); // 【Phase 82.10 修復】沿著局部作用域鏈向上查找類繼承鏈
            return false;
        }
        
        public Dictionary<string, LpcValue> GetAllVariables() => _variables;

        // 【Phase 82.4 修復】實例 Clone 時的初始化：繼承 Blueprint 的 InheritedScope
        public void CloneFrom(Scope blueprint) {
            this.InheritedScope = blueprint.InheritedScope;
            this.Parent = blueprint.Parent;
            foreach (var kvp in blueprint._variables) if (!_variables.ContainsKey(kvp.Key)) _variables[kvp.Key] = kvp.Value;
            foreach (var kvp in blueprint._functions) if (!_functions.ContainsKey(kvp.Key)) _functions[kvp.Key] = kvp.Value;
        }

        // 【Phase 73-C】強制遍歷完整 Scope 鏈，合併所有變數
        public Dictionary<string, LpcValue> GetAllVariablesDeep() {
            var result = new Dictionary<string, LpcValue>();
            Scope current = this;
            while (current != null) {
                foreach (var kvp in current._variables) {
                    if (!result.ContainsKey(kvp.Key)) result[kvp.Key] = kvp.Value;
                }
                current = current.Parent;
            }
            return result;
        }

        public Dictionary<string, FunctionDeclarationNode> GetFunctions() => _functions;
    }
}
