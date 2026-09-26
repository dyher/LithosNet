using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LithosNet.VM {
    public static class SessionManager {

        public static List<PipeWriter> GetAllWriters() {
            return new List<PipeWriter>(_sessions.Values);
        }

        private static readonly ConcurrentDictionary<string, PipeWriter> _sessions = new();
        private static readonly ConcurrentDictionary<PipeWriter, string> _writerToObj = new();
        public static readonly AsyncLocal<string> CurrentPlayer = new AsyncLocal<string>();

        public static void Bind(string objName, PipeWriter writer) {
            _sessions[objName] = writer;
            _writerToObj[writer] = objName;
            Console.WriteLine($"🔌 [Session] 綁定連線: {objName}");
        }

        
        public static void RegisterBot(string objName) {
            var pipe = new System.IO.Pipelines.Pipe();
            _sessions[objName] = pipe.Writer;
            _writerToObj[pipe.Writer] = objName;
            Console.WriteLine($"🤖 [Session] 虛擬 Bot 註冊成功: {objName} (Memory Pipe)");
        }

        public static void Unbind(string objName) {
            if (_sessions.TryRemove(objName, out var writer)) {
                _writerToObj.TryRemove(writer, out _);
                Console.WriteLine($"❌ [Session] 斷開連線: {objName}");
            }
        }

        public static async Task SendAsync(string objName, string message) {
            Console.WriteLine($"🔍 [X-Ray] SendAsync 目標: '{objName}', 訊息: '{message}'");
            if (_sessions.TryGetValue(objName, out var writer)) {
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                await writer.WriteAsync(bytes);
                await writer.FlushAsync();
                Console.WriteLine($"✅ [X-Ray] FlushAsync 完成！資料已推向 Socket！");
            } else {
                Console.WriteLine($"⚠ [X-Ray] _sessions 找不到 '{objName}'！啟動暴力廣播...");
                foreach(var w in GetAllWriters()) {
                    byte[] bytes = Encoding.UTF8.GetBytes(message);
                    await w.WriteAsync(bytes);
                    await w.FlushAsync();
                }
            }
        }

        public static List<string> GetAllSessions() => _sessions.Keys.ToList();
        
        public static string GetObjName(PipeWriter writer) {
            return _writerToObj.TryGetValue(writer, out var name) ? name : "";
        }

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
