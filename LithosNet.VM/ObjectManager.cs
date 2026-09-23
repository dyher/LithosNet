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
        private readonly Dictionary<string, List<string>> _inventories = new();
        private readonly string _mudlibBase = "/home/tiny/LithosNet/mudlib/";

        public Scope LoadObject(string pathOrName) {
            string fullPath = ResolvePath(pathOrName);
            string objName = Path.GetFileNameWithoutExtension(fullPath);
            if (_objects.ContainsKey(objName)) return _objects[objName].scope;
            return CompileAndRegister(fullPath, objName);
        }

        // 【MUD 核心】智慧路徑解析：將 "room" 或 "obj/room" 轉換為絕對路徑
        private string ResolvePath(string pathOrName) {
            // 1. 如果已經是存在的絕對路徑，直接返回
            if (File.Exists(pathOrName)) return pathOrName;
            
            // 2. 嘗試在 mudlib 的標準目錄中尋找
            string[] searchDirs = { "obj/", "room/", "" };
            foreach (var dir in searchDirs) {
                string p = _mudlibBase + dir + pathOrName + ".c";
                if (File.Exists(p)) return p;
            }
            
            throw new Exception($"[VM] Cannot resolve LPC object path: {pathOrName}");
        }

        public void ReloadObject(string path) {
            // FileSystemWatcher 傳入的已經是絕對路徑
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
            // Clone 也需要經過路徑解析
            string fullPath = ResolvePath(blueprintName);
            string actualName = Path.GetFileNameWithoutExtension(fullPath);
            
            if (!_objects.ContainsKey(actualName)) LoadObject(fullPath); // 確保藍本已載入
            var blueprint = _objects[actualName].scope;
            
            string cloneId = $"{actualName}#{++_cloneCounter}";
            var newScope = new Scope();
            newScope.InheritFrom(blueprint); 
            var interp = new Interpreter(newScope, this);
            interp.ObjectName = cloneId;
            _objects[cloneId] = (newScope, interp);
            if (newScope.HasFunction("create")) interp.CallFunction("create", new List<LpcValue>());
            return cloneId;
        }

        public void MoveObject(string objName, string destName) {
            foreach(var inv in _inventories.Values) inv.Remove(objName);
            if(!_inventories.ContainsKey(destName)) _inventories[destName] = new List<string>();
            _inventories[destName].Add(objName);
            if(_objects.ContainsKey(objName)) {
                _objects[objName].scope.Set("environment", LpcValue.Create(destName));
            }
            try { CallFunction(destName, "init", LpcValue.Create(objName)); } catch {}
        }

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
