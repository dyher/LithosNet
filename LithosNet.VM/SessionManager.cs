using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LithosNet.VM {
    public static class SessionManager {
        // 【Phase 62: 登入流程】Session 級別的輸入攔截器 (Input Trap)
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> InputTraps = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();

        public static void SetInputTrap(string sessionObj, string funcName) {
            InputTraps[sessionObj] = funcName;
        }

        public static string GetAndClearInputTrap(string sessionObj) {
            if (InputTraps.TryRemove(sessionObj, out string func)) {
                return func;
            }
            return null;
        }


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
                // 【Phase 74: Telnet 標準】確保以 \r\n 結尾
                if (!message.EndsWith("\r\n") && !message.EndsWith("\n")) message += "\r\n";
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
        
        
        public static string GetObjNameByWriter(System.IO.Pipelines.PipeWriter writer) {
            return _writerToObj.TryGetValue(writer, out var name) ? name : null;
        }

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
