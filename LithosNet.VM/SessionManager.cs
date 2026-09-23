using System;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;

namespace LithosNet.VM {
    // 管理所有在線玩家的 TCP 連線
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

        // 【核心】將字串發送給特定的玩家
        public static async Task SendAsync(string objName, string message) {
            if (_sessions.TryGetValue(objName, out var writer)) {
                byte[] bytes = Encoding.UTF8.GetBytes(message + "\n");
                await writer.WriteAsync(bytes);
                await writer.FlushAsync();
            }
        }
    }
}
