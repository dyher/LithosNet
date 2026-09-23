#nullable disable
using System;
using System.IO;
using System.Collections.Generic;
using LithosNet.Core;
using LithosNet.Compiler;

namespace LithosNet.VM {
    public class ObjectManager {
        private readonly Dictionary<string, (Scope scope, Interpreter interp)> _objects = new();
        private int _cloneCounter = 0; // 【新增】克隆計數器

        public Scope LoadObject(string path) {
            string objName = Path.GetFileNameWithoutExtension(path);
            if (_objects.ContainsKey(objName)) return _objects[objName].scope;

            Console.WriteLine($"📂 [VM] 正在編譯 LPC 物件: {objName}.c");
            string src = File.ReadAllText(path);
            var tokens = new Lexer(src).Tokenize();
            var ast = new Parser(tokens).Parse();

            var scope = new Scope();
            var interp = new Interpreter(scope, this); 
            interp.ObjectName = objName;
            interp.Execute(ast);

            _objects[objName] = (scope, interp);
            Console.WriteLine($"✅ [VM] 物件 {objName} 就緒！\n");
            return scope;
        }

        // 【核心魔法】Clone (複製物件)
        public string Clone(string blueprintName) {
            if (!_objects.ContainsKey(blueprintName)) throw new Exception($"[VM] Blueprint '{blueprintName}' not found.");
            var blueprint = _objects[blueprintName].scope;
            
            string cloneId = $"{blueprintName}#{++_cloneCounter}";
            Console.WriteLine($"   🧬 [VM] 複製物件: {blueprintName} -> {cloneId}");
            
            // 深度複製 Scope (繼承所有變數與函數)
            var newScope = new Scope();
            newScope.InheritFrom(blueprint); 
            
            var interp = new Interpreter(newScope, this);
            interp.ObjectName = cloneId;
            
            _objects[cloneId] = (newScope, interp);
            
            // MUD 標準：克隆後自動觸發 create() Apply
            if (newScope.HasFunction("create")) {
                interp.CallFunction("create", new List<LpcValue>());
            }
            
            return cloneId;
        }

        public LpcValue CallFunction(string objName, string funcName, params LpcValue[] args) {
            if (!_objects.ContainsKey(objName)) throw new Exception($"[VM] Object '{objName}' not loaded.");
            return _objects[objName].interp.CallFunction(funcName, new List<LpcValue>(args));
        }
        
        public void Preload(string fullPath) { LoadObject(fullPath); }
    }
}
