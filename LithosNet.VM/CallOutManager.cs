using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LithosNet.Core;

namespace LithosNet.VM {
    public static class CallOutManager {
        private static int _nextId = 1;
        // handle -> (Cts, ObjName, FuncName)
        private static readonly ConcurrentDictionary<int, (CancellationTokenSource Cts, string ObjName, string FuncName)> _timers = new();
        // ObjName -> List of handles
        private static readonly ConcurrentDictionary<string, List<int>> _objTimers = new();

        public static int Schedule(string objName, string funcName, int delaySec, LpcValue[] args, ObjectManager objMgr) {
            int handle = Interlocked.Increment(ref _nextId);
            var cts = new CancellationTokenSource();
            
            _timers[handle] = (cts, objName, funcName);
            _objTimers.AddOrUpdate(objName, 
                new List<int> { handle }, 
                (key, list) => { lock(list) { list.Add(handle); } return list; });

            Task.Run(async () => {
                try {
                    await Task.Delay(delaySec * 1000, cts.Token);
                    if (!cts.IsCancellationRequested) {
                        try {
                            Console.WriteLine($"⏰ [CallOut] 觸發: {objName}->{funcName}");
                            objMgr.CallFunction(objName, funcName, args);
                        } catch (Exception ex) {
                            Console.WriteLine($"⚠ [CallOut] 執行失敗: {ex.Message}");
                        }
                    }
                } catch (TaskCanceledException) { }
                finally {
                    _timers.TryRemove(handle, out _);
                    if (_objTimers.TryGetValue(objName, out var list)) {
                        lock(list) { list.Remove(handle); }
                    }
                }
            });

            return handle;
        }

        public static int Remove(string objName, string funcNameOrHandle) {
            int removedCount = 0;
            if (int.TryParse(funcNameOrHandle, out int handle)) {
                if (_timers.TryGetValue(handle, out var data) && data.ObjName == objName) {
                    data.Cts.Cancel();
                    removedCount = 1;
                }
            } else {
                if (_objTimers.TryGetValue(objName, out var list)) {
                    lock(list) {
                        foreach(var h in list.ToArray()) {
                            if (_timers.TryGetValue(h, out var data) && data.FuncName == funcNameOrHandle) {
                                data.Cts.Cancel();
                                removedCount++;
                            }
                        }
                    }
                }
            }
            return removedCount;
        }

        public static void ClearObject(string objName) {
            if (_objTimers.TryRemove(objName, out var list)) {
                lock(list) {
                    foreach(var h in list) {
                        if (_timers.TryGetValue(h, out var data)) {
                            data.Cts.Cancel();
                        }
                    }
                }
            }
        }
    }
}
