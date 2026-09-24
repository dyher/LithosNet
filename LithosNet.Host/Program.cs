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
        LithosNet.VM.BuiltInEfuns.ObjMgr = ObjMgr;
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
            try { ObjMgr.CallFunction(MasterObj, "preload"); } catch (Exception e) { Console.WriteLine($"⚠️ Master preload 錯誤: {e.ToString()}"); }

            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine($"\n🚀 {cfg.GetProperty("name").GetString()} Driver 啟動！監聽端口: {port}\n");

            Console.WriteLine("🔍 [Diag] 進入 TCP 讀取迴圈...");
                while (true) {
                var client = await listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        static async Task HandleClientAsync(TcpClient client) {
        Console.WriteLine("🔌 [Network] 新客戶端連線！");
        var reader = PipeReader.Create(client.GetStream());
        var writer = PipeWriter.Create(client.GetStream());
        
        string currentObj = "";
        try {
            var connectRet = ObjMgr.CallFunction(MasterObj, "connect");
            Console.WriteLine($"🔍 [Diag] master->connect() 返回: {connectRet.AsString()}");
            currentObj = connectRet.AsString();
            if (string.IsNullOrEmpty(currentObj) || currentObj == "0") {
                Console.WriteLine("⚠ connect() 返回無效值，強制 Fallback 載入藍圖並直接使用它...");
                ObjMgr.LoadObject("obj/login");
                
                // 探測真實的物件名稱 (可能是 "obj/login" 或 "login")
                currentObj = "obj/login";
                try { 
                    ObjMgr.CallFunction(currentObj, "query_name"); 
                } catch { 
                    currentObj = "login"; 
                }
                Console.WriteLine($"✅ Fallback 鎖定物件: {currentObj}");
            }
        } catch (Exception ex) {
            Console.WriteLine($"❌ master->connect() 失敗:\n{ex}");
            client.Close(); return;
        }

        SessionManager.Bind(currentObj, writer);
        Console.WriteLine($"🔌 [Session] 綁定連線: {currentObj}");
        
        // 【關鍵】強制呼叫 logon() apply
        try { 
            Console.WriteLine($"🔍 [Diag] 準備呼叫 {currentObj}->logon()...");
            SessionManager.CurrentPlayer.Value = currentObj;
            ObjMgr.CallFunction(currentObj, "logon"); 
            Console.WriteLine("✅ logon() 呼叫成功！");
        } catch (Exception ex) { 
            Console.WriteLine($"❌ logon() 呼叫失敗:\n{ex}"); 
        }

        try {
            Console.WriteLine("🔍 [Diag] 進入 TCP 讀取迴圈...");
            while (true) {
                // 【絕對關鍵】必須有 await，否則會變成 Busy Loop 瞬間耗盡 CPU！
                ReadResult result = await reader.ReadAsync();
                Console.WriteLine("🔍 [Diag] PipeReader 收到資料！");
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (true) {
                    if (buffer.Length == 0) break;
                    byte firstByte = buffer.Slice(0, 1).ToArray()[0];
                    
                    if (firstByte == 255) {
                        if (buffer.Length < 5) break;
                        byte[] lenBytes = buffer.Slice(1, 4).ToArray();
                        int length = (lenBytes[0] << 24) | (lenBytes[1] << 16) | (lenBytes[2] << 8) | lenBytes[3];
                        if (buffer.Length < 5 + length) break;
                        
                        byte[] payload = buffer.Slice(5, length).ToArray();
                        string json = Encoding.UTF8.GetString(payload);
                        SessionManager.CurrentPlayer.Value = currentObj;
                        var activeObj = SessionManager.GetObjName(writer);
                        var activeObj = SessionManager.GetObjName(writer);
                        if (!string.IsNullOrEmpty(activeObj)) currentObj = activeObj;
                        Console.WriteLine($"🔥 [X-Ray] Calling receive_binary on {currentObj} with: {json}");
                        ObjMgr.CallFunction(currentObj, "receive_binary", LpcValue.Create(json));
                        buffer = buffer.Slice(5 + length);
                    } else {
                        SequencePosition? position = buffer.PositionOf((byte)10);
                        if (position == null) break;
                        byte[] lineBytes = buffer.Slice(0, position.Value).ToArray();
                        string line = Encoding.UTF8.GetString(lineBytes).Trim();
                        SessionManager.CurrentPlayer.Value = currentObj;
                        var activeObj = SessionManager.GetObjName(writer);
                        var activeObj = SessionManager.GetObjName(writer);
                        if (!string.IsNullOrEmpty(activeObj)) currentObj = activeObj;
                        Console.WriteLine($"🔥 [X-Ray] Calling receive_message on {currentObj} with: {line}");
                        ObjMgr.CallFunction(currentObj, "receive_message", LpcValue.Create(line));
                        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
                    }
                }
                reader.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted) {
                    Console.WriteLine("⚠️ [Diag] PipeReader 收到 EOF，客戶端已斷開！");
                    break;
                }
            }
        } catch (Exception ex) {
            Console.WriteLine($"❌ [Network] TCP 讀取異常:\n{ex}");
        } finally {
            Console.WriteLine($"❌ [Session] 斷開連線: {currentObj}");
            SessionManager.Unbind(currentObj);
            try { ObjMgr.CallFunction(currentObj, "logoff"); } catch {}
            client.Close();
        }
    }
    }
}
