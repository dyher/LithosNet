#nullable disable
using System;
using System.Collections.Generic;
using LithosNet.Core;

namespace LithosNet.VM {
    public class Scope {
        private readonly Dictionary<string, LpcValue> _variables = new();
        private readonly Dictionary<string, FunctionDeclarationNode> _functions = new();

        public void Set(string name, LpcValue value) => _variables[name] = value;

        public LpcValue Get(string name) {
            if (_variables.TryGetValue(name, out var v)) return v;
            throw new Exception($"[VM] Variable '{name}' not found.");
        }

        public void RegisterFunction(FunctionDeclarationNode func) => _functions[func.Name] = func;

        public FunctionDeclarationNode GetFunction(string name) {
            if (_functions.TryGetValue(name, out var f)) return f;
            throw new Exception($"[VM] Function '{name}' not found.");
        }

        public bool HasFunction(string name) => _functions.ContainsKey(name);
        public bool Has(string name) => _variables.ContainsKey(name);
    }
}
