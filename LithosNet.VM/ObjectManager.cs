#nullable disable
using System;
using System.IO;
using System.Collections.Generic;
using LithosNet.Core;
using LithosNet.Compiler;

namespace LithosNet.VM {
    public class ObjectManager {
        public static ObjectManager Instance { get; private set; }
        public ObjectManager() { Instance = this; }
        private readonly Dictionary<string, (Scope scope, Interpreter interp)> _objects = new();
        private int _cloneCounter = 0;
        private readonly Dictionary<string, List<string>> _inventories = new();
        private readonly string _mudlibBase = "/home/tiny/LithosNet/mudlib/";

        public Scope LoadObject(string pathOrName) {
            string fullPath = pathOrName;
            if (System.IO.File.Exists(fullPath) == false) {
                string[] dirs = { "/home/tiny/LithosNet/mudlib/obj/", "/home/tiny/LithosNet/mudlib/room/", "/home/tiny/LithosNet/mudlib/" };
                foreach(var d in dirs) {
                    if(System.IO.File.Exists(d + pathOrName + ".c")) { fullPath = d + pathOrName + ".c"; break; }
                }
            }
            string objName = Path.GetFileNameWithoutExtension(fullPath);
            if (_objects.ContainsKey(objName)) return _objects[objName].scope;
            return CompileAndRegister(fullPath, objName);
        }

        private string ResolvePath(string pathOrName) {
            if (File.Exists(pathOrName)) return pathOrName;
            string[] searchDirs = { "obj/", "room/", "" };
            foreach (var dir in searchDirs) {
                string p = _mudlibBase + dir + pathOrName + ".c";
                if (File.Exists(p)) return p;
            }
            throw new Exception($"[VM] Cannot resolve LPC object path: {pathOrName}");
        }

        public void ReloadObject(string path) {
            string objName = Path.GetFileNameWithoutExtension(path);
            Console.WriteLine($"🔄 [VM] 熱更新: {objName}.c");
            CompileAndRegister(path, objName);
        }

        private Scope CompileAndRegister(string path, string objName) {
            string src = (new LithosNet.Compiler.LpcPreprocessor(_mudlibPath ?? "").Process(path);
            src = LithosNet.Compiler.Preprocessor.Process(src, Path.GetDirectoryName(path));
            var inputStream = new Antlr4.Runtime.AntlrInputStream(src);
            var lexer = new LithosNet.Compiler.Ast.LPCLexer(inputStream);
            var tokenStream = new Antlr4.Runtime.CommonTokenStream(lexer);
            var parser = new LithosNet.Compiler.Ast.LPCParser(tokenStream);
            var tree = parser.program();
            var builder = new LithosNet.Compiler.AstBuilder();
                var ast = new System.Collections.Generic.List<LithosNet.Core.AstNode>();
                foreach(var decl in tree.topLevelDecl()) {
                    var node = builder.Visit(decl);
                    if (node != null) ast.Add(node);
                }
            var scope = new Scope();
            var interp = new Interpreter(scope, this); 
            interp.ObjectName = objName;
            interp.Execute(ast);
            _objects[objName] = (scope, interp);
            return scope;
        }

        public string Clone(string blueprintName) {
            string fullPath = ResolvePath(blueprintName);
            string actualName = Path.GetFileNameWithoutExtension(fullPath);
            if (!_objects.ContainsKey(actualName)) LoadObject(fullPath);
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
            if(_objects.ContainsKey(objName)) _objects[objName].scope.Set("environment", LpcValue.Create(destName));
            try { CallFunction(destName, "init", LpcValue.Create(objName)); } catch {}
        }

        public List<string> GetInventory(string objName) => _inventories.ContainsKey(objName) ? _inventories[objName] : new List<string>();

        public void DestructObject(string objName) {
            foreach(var inv in _inventories.Values) inv.Remove(objName);
            _inventories.Remove(objName);
            GridMapManager.Unregister(objName);
            if (_objects.ContainsKey(objName)) { _objects.Remove(objName); Console.WriteLine($"💥 [VM] 銷毀: {objName}"); }
        }

        public LpcValue CallFunction(string objName, string funcName, params LpcValue[] args) {
            if (!_objects.ContainsKey(objName)) throw new Exception($"[VM] Object '{objName}' not loaded.");
            return _objects[objName].interp.CallFunction(funcName, new List<LpcValue>(args));
        }
        
        public bool ObjectExists(string objName) => _objects.ContainsKey(objName);
        public void Preload(string fullPath) { LoadObject(fullPath); }
    }
}
