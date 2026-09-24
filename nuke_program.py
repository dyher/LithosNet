code = """using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LithosNet.Core;
using LithosNet.VM;

namespace LithosNet.Host {
    class Program {
        static readonly ObjectManager ObjMgr = new();
        static string MudlibPath;
        static string MasterObj;

        static async Task Main(string[] args) {
            Console.WriteLine("==================================================");
            Console.WriteLine("🔥 [Phase 34] 啟動雙軌制 MMORPG 引擎 (Text + Binary)！");
            Console.WriteLine("==================================================\\n");

            EfunRegistry.RegisterFromType(typeof(BuiltInEfuns));
            
            string cfgText = File.ReadAllText("config.json");
            var cfg = JsonDocument.Parse(cfgText).RootElement;
            MudlibPath = cfg.GetProperty("mudlib_dir").GetString();
            MasterObj = cfg.GetProperty("master_object").GetString();
            int port = cfg.GetProperty("port").GetInt32();

            ObjMgr.Preload(MudlibPath + "obj/" + MasterObj + ".c");
            try { ObjMgr.CallFunction(MasterObj, "preload"); } catch (Exception e) { Console.WriteLine($"⚠️ Master preload 錯誤: {e.Message}"); }

            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine($"\\n🚀 {cfg.GetProperty("name").GetString()} Driver 啟動！監聽端口: {port}\\n");

            while (true) {
                var client = await listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        static async Task HandleClientAsync(TcpClient client) {
            var reader = PipeReader.Create(client.GetStream());
            var writer = PipeWriter.Create(client.GetStream());
            
            string currentObj = "";
            try {
                currentObj = ObjMgr.CallFunction(MasterObj, "connect").AsString();
            } catch (Exception e) {
                Console.WriteLine($"❌ master->connect() 失敗: {e.Message}");
                client.Close(); return;
            }

            SessionManager.Bind(currentObj, writer);
            SessionManager.CurrentPlayer.Value = currentObj;
            try { ObjMgr.CallFunction(currentObj, "logon"); } catch {}

            try {
                while (true) {
                    ReadResult result = await reader.ReadAsync();
                    ReadOnlySequence<byte> buffer = result.Buffer;
                    
                    while (true) {
                        if (buffer.Length == 0) break;
                        byte firstByte = buffer.FirstSpan[0];
                        
                        // 【Binary Protocol】255 (0xFF) + 4 bytes Length + Payload
                        if (firstByte == 255) {
                            if (buffer.Length < 5) break;
                            var lenSpan = buffer.Slice(1, 4).FirstSpan;
                            int length = (lenSpan[0] << 24) | (lenSpan[1] << 16) | (lenSpan[2] << 8) | lenSpan[3];
                            if (buffer.Length < 5 + length) break;
                            
                            var payloadBytes = buffer.Slice(5, length);
                            string json = Encoding.UTF8.GetString(payloadBytes);
                            currentObj = SessionManager.GetObjName(writer);
                            if (!string.IsNullOrEmpty(currentObj)) {
                                try { ObjMgr.CallFunction(currentObj, "receive_binary", LpcValue.Create(json)); } catch {}
                            }
                            buffer = buffer.Slice(5 + length);
                        } 
                        // 【Text Protocol】按 10 ('\n') 分割
                        else {
                            SequencePosition? position = buffer.PositionOf(10); 
                            if (position == null) break;
                            var lineBytes = buffer.Slice(0, position.Value);
                            string line = Encoding.UTF8.GetString(lineBytes).Trim();
                            currentObj = SessionManager.GetObjName(writer);
                            if (!string.IsNullOrEmpty(currentObj)) {
                                try { ObjMgr.CallFunction(currentObj, "receive_message", LpcValue.Create(line)); } catch {}
                            }
                            buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
                        }
                    }
                    reader.AdvanceTo(buffer.Start, buffer.End);
                    if (result.IsCompleted) break;
                }
            } catch { }
            finally { 
                currentObj = SessionManager.GetObjName(writer);
                if (!string.IsNullOrEmpty(currentObj)) {
                    try { ObjMgr.CallFunction(currentObj, "logoff"); } catch {}
                    SessionManager.Unbind(currentObj);
                    ObjMgr.DestructObject(currentObj);
                }
                client.Close(); 
            }
        }
    }
}
"""
with open("LithosNet.Host/Program.cs", "w", encoding="utf-8") as f:
    f.write(code)
print("✅ Program.cs 已核彈級重寫！徹底免疫正則吞噬與字元轉義陷阱！")
