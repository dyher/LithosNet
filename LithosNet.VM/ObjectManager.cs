#nullable disable
using System;
using System.IO;
using System.Collections.Generic;
using LithosNet.Core;
using LithosNet.Compiler;

namespace LithosNet.VM {
    public class ObjectManager {
        private readonly Dictionary<string, (Scope scope, Interpreter interp)> _objects = new();

        public Scope LoadObject(string path) {
            string objName = Path.GetFileNameWithoutExtension(path);
            if (_objects.ContainsKey(objName)) return _objects[objName].scope;

            Console.WriteLine($"📂 [VM] 正在編譯 LPC 物件: {objName}.c");
            string src = File.ReadAllText(path);
            var tokens = new Lexer(src).Tokenize();
            var ast = new Parser(tokens).Parse();

            var scope = new Scope();
            // 【關鍵】將 ObjectManager 自己注入到 Interpreter 中！
            var interp = new Interpreter(scope, this);
            interp.ObjectName = objName; 
            interp.Execute(ast);

            _objects[objName] = (scope, interp);
            Console.WriteLine($"✅ [VM] 物件 {objName} 就緒！\n");
            return scope;
        }

        public LpcValue CallFunction(string objName, string funcName, params LpcValue[] args) {
            if (!_objects.ContainsKey(objName)) throw new Exception($"[VM] Object '{objName}' not loaded.");
            return _objects[objName].interp.CallFunction(funcName, new List<LpcValue>(args));
        }
        
        // 供 Host 層使用 (帶有完整路徑的載入)
        public void Preload(string fullPath) {
            LoadObject(fullPath);
        }
    }
}
