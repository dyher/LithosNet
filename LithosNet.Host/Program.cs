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
            Console.WriteLine("🔥 [Phase 30] 啟動正統 FluffOS 架構模式！");
            Console.WriteLine("==================================================\n");

            EfunRegistry.RegisterFromType(typeof(BuiltInEfuns));
            
            // 讀取 config.json
            string cfgText = File.ReadAllText("config.json");
            var cfg = JsonDocument.Parse(cfgText).RootElement;
            MudlibPath = cfg.GetProperty("mudlib_dir").GetString();
            MasterObj = cfg.GetProperty("master_object").GetString();
            int port = cfg.GetProperty("port").GetInt32();

            // 【FluffOS 標準】Driver 只負責載入 Master Object
            ObjMgr.Preload(MudlibPath + "obj/" + MasterObj + ".c");
            
            // 呼叫 master->preload() 讓 LPC 自己決定要預載入什麼
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
            
            // 【FluffOS 標準】呼叫 master->connect() 取得 login 物件
            string currentObj = "";
            try {
                currentObj = ObjMgr.CallFunction(MasterObj, "connect").AsString();
            } catch (Exception e) {
                Console.WriteLine($"❌ master->connect() 失敗: {e.Message}");
                client.Close(); return;
            }

            SessionManager.Bind(currentObj, writer);
            SessionManager.CurrentPlayer.Value = currentObj;
            
            // 呼叫 login->logon() 進行初始握手
            try { ObjMgr.CallFunction(currentObj, "logon"); } catch {}

            try {
                while (true) {
                    ReadResult result = await reader.ReadAsync();
                    ReadOnlySequence<byte> buffer = result.Buffer;
                    SequencePosition? position;
                    while ((position = buffer.PositionOf((byte)'\n')) != null) {
                        var lineBytes = buffer.Slice(0, position.Value);
                        string line = Encoding.UTF8.GetString(lineBytes).Trim();
                        
                        // 【FluffOS 標準】Driver 不拆分指令，直接將整行字串丟給 receive_message
                        currentObj = SessionManager.GetObjName(writer);
                        if (string.IsNullOrEmpty(currentObj)) break;
                        
                        try {
                            ObjMgr.CallFunction(currentObj, "receive_message", LpcValue.Create(line));
                        } catch (Exception e) {
                            Console.WriteLine($"⚠️ LPC 執行錯誤 ({currentObj}): {e.Message}");
                        }
                        
                        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
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
