using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LithosNet.VM {
    public static class SessionManager {
        private static readonly ConcurrentDictionary<string, PipeWriter> _sessions = new();

        public static void Bind(string objName, PipeWriter writer) {
            _sessions[objName] = writer;
            Console.WriteLine($"🔌 [Session] 綁定連線: {objName}");
        }

        public static void Unbind(string objName) {
            _sessions.TryRemove(objName, out _);
            Console.WriteLine($"❌ [Session] 斷開連線: {objName}");
        }

        public static async Task SendAsync(string objName, string message) {
            if (_sessions.TryGetValue(objName, out var writer)) {
                byte[] bytes = Encoding.UTF8.GetBytes(message + "\n");
                await writer.WriteAsync(bytes);
                await writer.FlushAsync();
            }
        }

        // 【新增】獲取所有在線玩家的 Object ID 列表
        public static List<string> GetAllSessions() {
            return _sessions.Keys.ToList();
        }
    }
}
