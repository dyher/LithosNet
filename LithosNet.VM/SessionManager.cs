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
        private static readonly ConcurrentDictionary<PipeWriter, string> _writerToObj = new();
        public static readonly System.Threading.AsyncLocal<string> CurrentPlayer = new System.Threading.AsyncLocal<string>();

        public static void Bind(string objName, PipeWriter writer) {
            _sessions[objName] = writer;
            _writerToObj[writer] = objName;
            Console.WriteLine($"🔌 [Session] 綁定連線: {objName}");
        }

        public static void Unbind(string objName) {
            if (_sessions.TryRemove(objName, out var writer)) {
                _writerToObj.TryRemove(writer, out _);
                Console.WriteLine($"❌ [Session] 斷開連線: {objName}");
            }
        }

        public static async Task SendAsync(string objName, string message) {
            if (_sessions.TryGetValue(objName, out var writer)) {
                byte[] bytes = Encoding.UTF8.GetBytes(message + "\n");
                await writer.WriteAsync(bytes);
                await writer.FlushAsync();
            }
        }

        public static List<string> GetAllSessions() => _sessions.Keys.ToList();
        
        public static string GetObjName(PipeWriter writer) {
            return _writerToObj.TryGetValue(writer, out var name) ? name : "";
        }

        // 【FluffOS 核心】exec(new_obj, old_obj) - 無縫轉移 TCP 連線
        public static void Exec(string newObj, string oldObj) {
            if (_sessions.TryRemove(oldObj, out var writer)) {
                _sessions[newObj] = writer;
                _writerToObj[writer] = newObj;
                CurrentPlayer.Value = newObj;
                Console.WriteLine($"🔄 [Session] 連線無縫轉移: {oldObj} -> {newObj}");
            }
        }
    }
}
