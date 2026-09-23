using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace LithosNet.VM {
    // 管理所有物件的心跳 (heart_beat)
    public static class HeartbeatManager {
        private static readonly ConcurrentDictionary<string, int> _heartbeats = new();
        private static readonly ObjectManager _objMgrRef;
        private static Timer _timer;

        static HeartbeatManager() { }

        public static void Initialize(ObjectManager objMgr, int intervalMs = 2000) {
            _timer = new Timer(Tick, objMgr, intervalMs, intervalMs);
            Console.WriteLine($"💓 [Heartbeat] 心跳系統啟動！間隔: {intervalMs}ms");
        }

        // 每 2 秒觸發一次
        private static void Tick(object state) {
            var objMgr = (ObjectManager)state;
            foreach (var kvp in _heartbeats) {
                try {
                    objMgr.CallFunction(kvp.Key, "heart_beat");
                } catch (Exception ex) {
                    Console.WriteLine($"⚠️ [Heartbeat] {kvp.Key} 心跳錯誤: {ex.Message}");
                    _heartbeats.TryRemove(kvp.Key, out _);
                }
            }
        }

        // 開啟/關閉某個物件的心跳
        public static void SetHeartBeat(string objName, bool enable) {
            if (enable) {
                _heartbeats.TryAdd(objName, 1);
                Console.WriteLine($"💓 [Heartbeat] 開啟心跳: {objName}");
            } else {
                _heartbeats.TryRemove(objName, out _);
                Console.WriteLine($"🛑 [Heartbeat] 關閉心跳: {objName}");
            }
        }
    }
}
