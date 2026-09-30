using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LithosNet.Core;

namespace LithosNet.VM
{
    public class CallOutManager
    {
        private readonly ObjectManager _objMgr;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        
        // call_out 結構
        private class CallOutTask {
            public string ObjectName { get; set; }
            public string FunctionName { get; set; }
            public LpcValue[] Args { get; set; }
            public DateTime ExecuteTime { get; set; }
            public int Id { get; set; }
        }

        // 使用 SortedSet 或 PriorityQueue 來管理 call_out，這裡用簡單的 List + 排序，或 ConcurrentDictionary + 定時檢查
        // 為了簡單且高效，我們使用一個背景執行緒定期掃描
        private readonly List<CallOutTask> _callOuts = new List<CallOutTask>();
        private int _nextCallOutId = 1;
        private readonly object _callOutLock = new object();

        // heart_beat 結構
        private class HeartBeatInfo {
            public float Interval { get; set; }
            public DateTime NextTrigger { get; set; }
        }
        private readonly ConcurrentDictionary<string, HeartBeatInfo> _heartBeats = new ConcurrentDictionary<string, HeartBeatInfo>();

        public CallOutManager(ObjectManager objMgr) {
            _objMgr = objMgr;
            // 啟動背景時間引擎
            _ = Task.Run(TimeEngineLoop);
        }

        private async Task TimeEngineLoop() {
            while (!_cts.IsCancellationRequested) {
                try {
                    await Task.Delay(100, _cts.Token); // 每 100ms 檢查一次
                    ProcessCallOuts();
                    ProcessHeartBeats();
                } catch (TaskCanceledException) {
                    break;
                } catch (Exception ex) {
                    Console.WriteLine($"[CallOutManager] TimeEngineLoop error: {ex.Message}");
                }
            }
        }

        private void ProcessCallOuts() {
            List<CallOutTask> toExecute = new List<CallOutTask>();
            lock (_callOutLock) {
                var now = DateTime.UtcNow;
                // 找出所有到期的 call_out
                toExecute = _callOuts.Where(c => c.ExecuteTime <= now).ToList();
                // 從清單中移除
                foreach (var task in toExecute) {
                    _callOuts.Remove(task);
                }
            }

            // 在非鎖狀態下執行，避免阻塞
            foreach (var task in toExecute) {
                _ = _objMgr.EnqueueAndAwaitAsync(() => {
                    try {
                        return _objMgr.CallFunction(task.ObjectName, task.FunctionName, task.Args);
                    } catch (Exception ex) {
                        Console.WriteLine($"[call_out] 執行錯誤 ({task.ObjectName}::{task.FunctionName}): {ex.Message}");
                        return LpcValue.Create(0);
                    }
                });
            }
        }

        private void ProcessHeartBeats() {
            var now = DateTime.UtcNow;
            var toTrigger = new List<string>();

            foreach (var kvp in _heartBeats) {
                if (now >= kvp.Value.NextTrigger) {
                    toTrigger.Add(kvp.Key);
                    // 更新下次觸發時間
                    kvp.Value.NextTrigger = now.AddSeconds(kvp.Value.Interval);
                }
            }

            foreach (var objName in toTrigger) {
                _ = _objMgr.EnqueueAndAwaitAsync(() => {
                    try {
                        return _objMgr.CallFunction(objName, "heart_beat", new LpcValue[0]);
                    } catch {
                        // 物件可能已被 destruct，忽略錯誤
                        return LpcValue.Create(0);
                    }
                });
            }
        }

        public int AddCallOut(string objName, string funcName, float delay, LpcValue[] args) {
            lock (_callOutLock) {
                int id = _nextCallOutId++;
                _callOuts.Add(new CallOutTask {
                    ObjectName = objName,
                    FunctionName = funcName,
                    Args = args ?? new LpcValue[0],
                    ExecuteTime = DateTime.UtcNow.AddSeconds(delay),
                    Id = id
                });
                return id;
            }
        }

        public int RemoveCallOut(string objName, string funcName) {
            lock (_callOutLock) {
                int removedCount = 0;
                _callOuts.RemoveAll(c => {
                    if (c.ObjectName == objName && c.FunctionName == funcName) {
                        removedCount++;
                        return true;
                    }
                    return false;
                });
                return removedCount;
            }
        }

        public void SetHeartBeat(string objName, float interval) {
            if (interval <= 0) {
                _heartBeats.TryRemove(objName, out _);
            } else {
                _heartBeats[objName] = new HeartBeatInfo {
                    Interval = interval,
                    NextTrigger = DateTime.UtcNow.AddSeconds(interval)
                };
            }
        }

        public float GetHeartBeat(string objName) {
            if (_heartBeats.TryGetValue(objName, out var info)) {
                return info.Interval;
            }
            return 0.0f;
        }

        public void CleanupObject(string objName) {
            // 當物件被 destruct 時，清理其相關的 call_out 和 heart_beat
            lock (_callOutLock) {
                _callOuts.RemoveAll(c => c.ObjectName == objName);
            }
            _heartBeats.TryRemove(objName, out _);
        }

        public void Shutdown() {
            _cts.Cancel();
        }
    }
}
