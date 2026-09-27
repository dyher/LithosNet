using System.Threading;
using System.Collections.Concurrent;
#nullable disable
using System;
using System.IO;
using System.Collections.Generic;
using LithosNet.Core;
using LithosNet.Compiler;

namespace LithosNet.VM {
    public class ObjectManager {

        // 【Phase 60: call_out 核心欄位】
        private readonly object _callOutLock = new object();
        private readonly System.Collections.Generic.List<CallOutTask> _callOutTasks = new System.Collections.Generic.List<CallOutTask>();
        private System.Timers.Timer _callOutTimer;

        // 【Phase 59: P1】線程親和性護城河
        private void AssertThreadAffinity(string context) {
            // 【Phase 59: 啟動豁免】允許 Main Thread (Thread 1) 在 Worker 啟動前執行
            if (_workerThread == null && Thread.CurrentThread.ManagedThreadId == 1) return;
            
            if (_workerThread != null && Thread.CurrentThread != _workerThread) {
                string msg = string.Format("🚨 [CRITICAL] LPC Thread Affinity Violated! [{0}] on Thread {1} instead of {2}", context, Thread.CurrentThread.ManagedThreadId, _workerThread.ManagedThreadId);
                throw new InvalidOperationException(msg);
            }
        }

        // 【Phase 58: FluffOS 核心】單執行緒事件隊列相關欄位
        private System.Collections.Concurrent.BlockingCollection<Action> _eventQueue = new BlockingCollection<Action>(new ConcurrentQueue<Action>());
        private System.Threading.Thread _workerThread;

    private Interpreter _simulEfunInterp; // 【FluffOS】Simul_efun 後備解釋器
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
            _workerThread = new System.Threading.Thread(EventLoop);
            _workerThread.IsBackground = true;
            _workerThread.Start();

            // 【Phase 60: 啟動 call_out 監控計時器】
            _callOutTimer = new System.Timers.Timer(100); 
            _callOutTimer.Elapsed += (s, e) => ProcessCallOuts();
            _callOutTimer.AutoReset = true;
            _callOutTimer.Start();
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
            Instance = this;
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
            // 【Phase 59: P0】熱重載強制推入 Event Queue
            _eventQueue.Add(() => {
                string objName = Path.GetFileNameWithoutExtension(path);
                Console.WriteLine($"🔄 [VM] 熱更新: {objName}.c");
                CompileAndRegister(path, objName);
            });
        }

        private Scope CompileAndRegister(string path, string objName) {
            string src = (new LithosNet.Compiler.LpcPreprocessor(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(path))).Process(path));
            src = LithosNet.Compiler.Preprocessor.Process(src, Path.GetDirectoryName(path));
            // 【Phase 58: FluffOS 關鍵字降級】完美兼容現有 Mudlib (在 ANTLR 解析前清洗)
            src = System.Text.RegularExpressions.Regex.Replace(src, @"\b(object|array|mapping)\b", "mixed");
            src = System.Text.RegularExpressions.Regex.Replace(src, @"\b(public|private|protected|static|nosave|ref)\b", "");

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
            AssertThreadAffinity("CallFunction");
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
            if (_objects.TryGetValue(objName, out var objData)) {
                // 1. 嘗試呼叫 clean_up apply
                try { objData.interp.CallFunction("clean_up", new System.Collections.Generic.List<LpcValue>()); } catch {}
                // 2. 清空 Scope 變數，主動切斷引用鏈，協助 C# GC 快速回收
                objData.scope.GetAllVariables().Clear();
            }
            _objects.Remove(objName);

            // 【Phase 60: 物件銷毀時清理其未執行的 call_out】
            lock (_callOutLock) {
                _callOutTasks.RemoveAll(t => t.ObjName == objName);
            }
            _heartBeatObjects.Remove(objName);
            Console.WriteLine($"💥 [Lifecycle] Object '{objName}' has been destructed and GC-ready.");
        }
        // 【Phase 57: 架構優化】標準 FluffOS clone_object 邏輯
        public string CloneObject(string blueprintName) {
            // 1. 確保藍圖已載入
            if (!_objects.TryGetValue(blueprintName, out var blueprint)) {
                LoadObject(blueprintName);
                if (!_objects.TryGetValue(blueprintName, out blueprint)) throw new Exception($"Blueprint '{blueprintName}' not found");
            }
            // 2. 生成新的 clone ID (例如 obj/bot#1)
            int cloneCount = _objects.Keys.Count(k => k.StartsWith(blueprintName + "#"));
            string cloneId = $"{blueprintName}#{cloneCount + 1}";
            
            // 3. 創建新的 Scope 並繼承藍圖變數與函數
            var newScope = new Scope();
            newScope.InheritFrom(blueprint.scope);
            // 【Phase 57: 架構優化】為克隆體打上標記，防止 LPC 腳本中的無限遞迴
            newScope.Set("is_clone", LpcValue.Create(1));
            
            // 4. 創建新的 Interpreter
            var newInterp = new Interpreter(newScope, this);
            
            // 5. 註冊到 _objects
            _objects[cloneId] = (newScope, newInterp);
            
            // 6. 呼叫 clone 的 create() apply
            try { newInterp.CallFunction("create", new System.Collections.Generic.List<LpcValue>()); } catch {}
            
            return cloneId;
        }

        // 【Phase 55.2/56: FluffOS 核心】SimulEfun Fallback 與生命週期管理

        // 【Phase 56: FluffOS 核心】暴露所有已載入物件供 find_object 查詢

        // 【Phase 55.2/56: FluffOS 核心】SimulEfun Fallback 與生命週期管理

        // 【Phase 56: FluffOS 核心】暴露所有已載入物件供 find_object 查詢

        // 【Phase 55.2/56: FluffOS 核心】SimulEfun Fallback 與生命週期管理

        // 【Phase 52: Heartbeat 方法】
            
        // 【Phase 58: FluffOS 核心】單執行緒事件循環 (偽多線程)
        private void EventLoop() {
            _workerThread = Thread.CurrentThread;
            Console.WriteLine($"🚀 [EventLoop] Worker Thread started on ID: {_workerThread.ManagedThreadId}");
            foreach (var action in _eventQueue.GetConsumingEnumerable()) {
                try { action(); } catch (Exception ex) { Console.WriteLine($"[EventLoop Error] {ex.Message}"); }
            }
        }

        // 【Phase 59: P0.1 核心】同步阻塞等待 Worker Thread 執行並返回結果
        public LpcValue EnqueueAndAwait(Func<LpcValue> func) {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<LpcValue>();
            _eventQueue.Add(() => {
                try { tcs.SetResult(func()); } catch (Exception ex) { tcs.SetException(ex); }
            });
            return tcs.Task.Result;
        }

        public void EnqueueAction(Action action) => _eventQueue.Add(action);

        public void SetHeartBeat(string objName, bool enable) {
                if (enable) _heartBeatObjects.Add(objName);
                else _heartBeatObjects.Remove(objName);
            }

            private void OnHeartBeatTick(object sender, System.Timers.ElapsedEventArgs e) {
            var targets = _heartBeatObjects.ToList();
            foreach (var objName in targets) {
                if (_objects.ContainsKey(objName)) {
                    // 【Phase 58: FluffOS 核心】推入事件隊列，確保單執行緒順序執行，杜絕 Race Condition
                    _eventQueue.Add(() => CallFunction(objName, "heart_beat", Array.Empty<LpcValue>()));
                }
            }
        }

        // ==========================================
        // 【Phase 60: MUD 靈魂】call_out 核心實作
        // ==========================================
        private class CallOutTask {
            public string ObjName;
            public string FuncName;
            public System.Collections.Generic.List<LpcValue> Args;
            public long TriggerTicks;
        }

        public void ScheduleCallOut(string objName, string funcName, double delaySeconds, System.Collections.Generic.List<LpcValue> args) {
            long triggerTicks = DateTime.UtcNow.AddSeconds(delaySeconds).Ticks;
            lock (_callOutLock) {
                _callOutTasks.Add(new CallOutTask {
                    ObjName = objName,
                    FuncName = funcName,
                    Args = args,
                    TriggerTicks = triggerTicks
                });
            }
        }

        public bool RemoveCallOut(string objName, string funcName) {
            lock (_callOutLock) {
                return _callOutTasks.RemoveAll(t => t.ObjName == objName && t.FuncName == funcName) > 0;
            }
        }

        private void ProcessCallOuts() {
            long now = DateTime.UtcNow.Ticks;
            System.Collections.Generic.List<CallOutTask> triggered = new System.Collections.Generic.List<CallOutTask>();
            lock (_callOutLock) {
                for (int i = _callOutTasks.Count - 1; i >= 0; i--) {
                    if (_callOutTasks[i].TriggerTicks <= now) {
                        triggered.Add(_callOutTasks[i]);
                        _callOutTasks.RemoveAt(i);
                    }
                }
            }
            foreach (var task in triggered) {
                _eventQueue.Add(() => {
                    if (_objects.ContainsKey(task.ObjName)) {
                        try {
                            CallFunction(task.ObjName, task.FuncName, task.Args.ToArray());
                        } catch (Exception ex) {
                            Console.WriteLine(string.Format("[CallOut Error] {0}->{1}: {2}", task.ObjName, task.FuncName, ex.Message));
                        }
                    }
                });
            }
        }
            }
    }
