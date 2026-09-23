#nullable disable
using System;
using System.IO;
using System.Collections.Generic;
using LithosNet.Core;
using LithosNet.Compiler;

namespace LithosNet.VM {
    public class ObjectManager {
        private readonly Dictionary<string, (Scope scope, Interpreter interp)> _objects = new();
        private int _cloneCounter = 0;

        public Scope LoadObject(string path) {
            string objName = Path.GetFileNameWithoutExtension(path);
            if (_objects.ContainsKey(objName)) return _objects[objName].scope;
            return CompileAndRegister(path, objName);
        }

        // 【核心】重新編譯並替換藍本 (Hot-Reload)
        public void ReloadObject(string path) {
            string objName = Path.GetFileNameWithoutExtension(path);
            Console.WriteLine($"🔄 [VM] 偵測到檔案變更，正在熱更新: {objName}.c");
            CompileAndRegister(path, objName);
            Console.WriteLine($"✅ [VM] 熱更新完成: {objName}.c 已替換！新 clone 將使用新程式碼。");
        }

        private Scope CompileAndRegister(string path, string objName) {
            string src = File.ReadAllText(path);
            var tokens = new Lexer(src).Tokenize();
            var ast = new Parser(tokens).Parse();

            var scope = new Scope();
            var interp = new Interpreter(scope, this); 
            interp.ObjectName = objName;
            interp.Execute(ast);

            _objects[objName] = (scope, interp);
            return scope;
        }

        public string Clone(string blueprintName) {
            if (!_objects.ContainsKey(blueprintName)) throw new Exception($"[VM] Blueprint '{blueprintName}' not found.");
            var blueprint = _objects[blueprintName].scope;
            
            string cloneId = $"{blueprintName}#{++_cloneCounter}";
            var newScope = new Scope();
            newScope.InheritFrom(blueprint); 
            
            var interp = new Interpreter(newScope, this);
            interp.ObjectName = cloneId;
            _objects[cloneId] = (newScope, interp);
            
            if (newScope.HasFunction("create")) interp.CallFunction("create", new List<LpcValue>());
            return cloneId;
        }

        public void DestructObject(string objName) {
            if (_objects.ContainsKey(objName)) {
                _objects.Remove(objName);
                Console.WriteLine($"💥 [VM] 物件已徹底銷毀: {objName}");
            }
        }

        public LpcValue CallFunction(string objName, string funcName, params LpcValue[] args) {
            if (!_objects.ContainsKey(objName)) throw new Exception($"[VM] Object '{objName}' not loaded.");
            return _objects[objName].interp.CallFunction(funcName, new List<LpcValue>(args));
        }
        
        public bool ObjectExists(string objName) => _objects.ContainsKey(objName);
        public void Preload(string fullPath) { LoadObject(fullPath); }
    }
}
