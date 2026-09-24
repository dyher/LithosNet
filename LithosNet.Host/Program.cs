using System;
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
            Console.WriteLine("==================================================\n");

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
            Console.WriteLine($"\n🚀 {cfg.GetProperty("name").GetString()} Driver 啟動！監聽端口: {port}\n");

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
                var connectRet = ObjMgr.CallFunction(MasterObj, "connect");
                Console.WriteLine($"🔍 [Diag] master->connect() 返回: Type={connectRet.Type}, Value={connectRet.Value}");
                
                // 【終極修復】LpcValue 是 struct，不能使用 ?.
                currentObj = connectRet.Value?.ToString() ?? "";
                
                if (string.IsNullOrEmpty(currentObj) || currentObj == "0") {
                    Console.WriteLine("⚠️ connect() 返回無效值，強制 Fallback 載入 login 藍圖並使用 login#1...");
                    ObjMgr.LoadObject("obj/login");
                    currentObj = "login#1";
                }
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
                        
                        // 【免疫 Span 限制】使用 ToArray() 安全讀取首字節
                        byte firstByte = buffer.Slice(0, 1).ToArray()[0];
                        
                        // 【Binary Protocol】255 (0xFF) + 4 bytes Length + Payload
                        if (firstByte == 255) {
                            if (buffer.Length < 5) break;
                            byte[] lenBytes = buffer.Slice(1, 4).ToArray();
                            int length = (lenBytes[0] << 24) | (lenBytes[1] << 16) | (lenBytes[2] << 8) | lenBytes[3];
                            if (buffer.Length < 5 + length) break;
                            
                            byte[] payloadBytes = buffer.Slice(5, length).ToArray();
                            string json = Encoding.UTF8.GetString(payloadBytes);
                            currentObj = SessionManager.GetObjName(writer);
                            if (!string.IsNullOrEmpty(currentObj)) {
                                try { ObjMgr.CallFunction(currentObj, "receive_binary", LpcValue.Create(json)); } catch {}
                            }
                            buffer = buffer.Slice(5 + length);
                        } 
                        // 【Text Protocol】按 10 (ASCII LF) 分割
                        else {
                            // 【免疫型別推斷失敗】強制轉型為 (byte)
                            SequencePosition? position = buffer.PositionOf((byte)10); 
                            if (position == null) break;
                            byte[] lineBytes = buffer.Slice(0, position.Value).ToArray();
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
