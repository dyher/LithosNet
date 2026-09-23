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
        
        // 【MUD 核心】記錄每個物件(房間)裡面有哪些物件(玩家/怪物)
        private readonly Dictionary<string, List<string>> _inventories = new();

        public Scope LoadObject(string path) {
            string objName = Path.GetFileNameWithoutExtension(path);
            if (_objects.ContainsKey(objName)) return _objects[objName].scope;
            return CompileAndRegister(path, objName);
        }

        public void ReloadObject(string path) {
            string objName = Path.GetFileNameWithoutExtension(path);
            Console.WriteLine($"🔄 [VM] 偵測到檔案變更，正在熱更新: {objName}.c");
            CompileAndRegister(path, objName);
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

        // 【MUD 核心】move(dest) - 移動物件並觸發 init()
        public void MoveObject(string objName, string destName) {
            // 1. 從所有舊環境中移除
            foreach(var inv in _inventories.Values) inv.Remove(objName);
            
            // 2. 加入新環境的 Inventory
            if(!_inventories.ContainsKey(destName)) _inventories[destName] = new List<string>();
            _inventories[destName].Add(objName);
            
            // 3. 更新物件自身的 environment 變數
            if(_objects.ContainsKey(objName)) {
                _objects[objName].scope.Set("environment", LpcValue.Create(destName));
            }
            
            // 4. 【FluffOS 標準】觸發新房間的 init() Apply (讓房間知道有人進來了)
            try { CallFunction(destName, "init", LpcValue.Create(objName)); } catch {}
        }

        // 【MUD 核心】all_inventory(obj) - 取得房間內的所有物件
        public List<string> GetInventory(string objName) {
            return _inventories.ContainsKey(objName) ? _inventories[objName] : new List<string>();
        }

        public void DestructObject(string objName) {
            foreach(var inv in _inventories.Values) inv.Remove(objName);
            _inventories.Remove(objName);
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
