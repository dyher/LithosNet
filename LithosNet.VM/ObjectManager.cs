#nullable disable
using System;
using System.IO;
using System.Collections.Generic;
using LithosNet.Core;
using LithosNet.Compiler;

namespace LithosNet.VM {
    public class ObjectManager {
        // 【Phase 55.1: C# 原生 FFI 管理器】
        private readonly Dictionary<string, Func<LpcValue[], LpcValue>> _nativeHandlers = new();

        public void RegisterNativeHandler(string name, Func<LpcValue[], LpcValue> handler) {
            _nativeHandlers[name] = handler;
        }

        public Func<LpcValue[], LpcValue> GetNativeHandler(string name) {
            _nativeHandlers.TryGetValue(name, out var handler);
            return handler;
        }

        private void SetupDefaultNativeHandlers() {
            // 示例 1: 高性能數學計算 (模擬 C/C++ 庫調用)
            RegisterNativeHandler("fast_pow", args => {
                double baseVal = args.Length > 0 ? args[0].AsInt() : 0;
                double expVal = args.Length > 1 ? args[1].AsInt() : 0;
                return LpcValue.Create((int)Math.Pow(baseVal, expVal));
            });

            // 示例 2: 字串處理 (模擬原生加密或編碼)
            RegisterNativeHandler("string_hash", args => {
                string input = args.Length > 0 ? args[0].AsString() : "";
                int hash = 0;
                foreach (char c in input) hash = (hash * 31) + c;
                return LpcValue.Create(hash);
            });
        }

        public static ObjectManager Instance { get; private set; }
        public ObjectManager() { 
            Instance = this;
            // 【Phase 52: Heartbeat 初始化與啟動】
            _heartBeatTimer = new System.Timers.Timer(1000); // 1 秒 tick 一次
            _heartBeatTimer.Elapsed += OnHeartBeatTick;
            _heartBeatTimer.AutoReset = true;
            _heartBeatTimer.Start();
            SetupDefaultNativeHandlers();
        }
        private readonly Dictionary<string, (Scope scope, Interpreter interp)> _objects = new();
        // 【Phase 52: Heartbeat 管理器】
        private readonly HashSet<string> _heartBeatObjects = new HashSet<string>();
        private readonly System.Timers.Timer _heartBeatTimer;

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
            string src = (new LithosNet.Compiler.LpcPreprocessor(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(path))).Process(path));
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

        

        public LpcValue CallFunction(string objName, string funcName, params LpcValue[] args) {
            if (!_objects.ContainsKey(objName)) throw new Exception($"[VM] Object '{objName}' not loaded.");
            return _objects[objName].interp.CallFunction(funcName, new List<LpcValue>(args));
        }
        
        public bool ObjectExists(string objName) => _objects.ContainsKey(objName);
        public void Preload(string fullPath) { LoadObject(fullPath); }
        public void LoadSimulEfun(string path) {
            Console.WriteLine($"📦 [SimulEfun] Loading global simul_efun from: {path}");
            // 加載 simul_efun，並將其標記為特殊的全局物件
            LoadObject(path);
            Console.WriteLine($"✅ [SimulEfun] Global functions registered successfully!");
        }

        // 【Phase 55.2/56: FluffOS 核心】SimulEfun Fallback 與生命週期管理
        public bool CallSimulEfunSafe(string name, System.Collections.Generic.List<LpcValue> args, out LpcValue result) {
            if (_simulEfunInterp != null && _simulEfunInterp._scope.HasFunction(name)) {
                result = _simulEfunInterp.CallFunction(name, args);
                return true;
            }
            result = LpcValue.Create(0);
            return false;
        }

        // 【Phase 56: FluffOS 核心】暴露所有已載入物件供 find_object 查詢
        public Dictionary<string, (Scope scope, Interpreter interp)> GetAllObjects() => _objects;

        // 【Phase 56: FluffOS 核心】銷毀物件並清理相關狀態
        public void DestructObject(string objName) {
            _objects.Remove(objName);
            _heartBeatObjects.Remove(objName);
            Console.WriteLine($"💥 [Lifecycle] Object '{objName}' has been destructed.");
        }

        // 【Phase 55.2/56: FluffOS 核心】SimulEfun Fallback 與生命週期管理
        public bool CallSimulEfunSafe(string name, System.Collections.Generic.List<LpcValue> args, out LpcValue result) {
            if (_simulEfunInterp != null && _simulEfunInterp._scope.HasFunction(name)) {
                result = _simulEfunInterp.CallFunction(name, args);
                return true;
            }
            result = LpcValue.Create(0);
            return false;
        }

        // 【Phase 52: Heartbeat 方法】
            public void SetHeartBeat(string objName, bool enable) {
                if (enable) _heartBeatObjects.Add(objName);
                else _heartBeatObjects.Remove(objName);
            }

            private async void OnHeartBeatTick(object sender, System.Timers.ElapsedEventArgs e) {
                var targets = _heartBeatObjects.ToList();
                foreach (var objName in targets) {
                    if (_objects.ContainsKey(objName)) {
                        try {
                            _ = Task.Run(() => CallFunction(objName, "heart_beat", Array.Empty<LpcValue>()));
                        } catch { 
                            // 忽略 Bot heart_beat 內部的錯誤，防止崩潰
                        }
                    }
                }
            }
    }
}
