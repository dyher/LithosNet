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

        // 【核心魔法】徹底銷毀物件，釋放記憶體
        public void DestructObject(string objName) {
            if (_objects.ContainsKey(objName)) {
                // 如果物件有 net_dead 或 destruct_apply，可以在這裡觸發
                _objects.Remove(objName);
                Console.WriteLine($"💥 [VM] 物件已徹底銷毀，記憶體已釋放: {objName}");
            }
        }

        public LpcValue CallFunction(string objName, string funcName, params LpcValue[] args) {
            if (!_objects.ContainsKey(objName)) throw new Exception($"[VM] Object '{objName}' not loaded or destructed.");
            return _objects[objName].interp.CallFunction(funcName, new List<LpcValue>(args));
        }
        
        public bool ObjectExists(string objName) => _objects.ContainsKey(objName);
        public void Preload(string fullPath) { LoadObject(fullPath); }
    }
}
