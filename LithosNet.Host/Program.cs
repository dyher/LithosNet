using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using LithosNet.Core;
using LithosNet.VM;

namespace LithosNet.Host {
    class Program {
        static readonly ObjectManager ObjMgr = new();
        static readonly string MudlibPath = "/home/tiny/LithosNet/mudlib/";

        static async Task Main(string[] args) {
            Console.WriteLine("==================================================");
            Console.WriteLine("🔥 [Phase 19] 啟動文字指令與房間漫遊系統！");
            Console.WriteLine("==================================================\n");

            EfunRegistry.RegisterFromType(typeof(BuiltInEfuns));
            
            // 載入世界
            ObjMgr.Preload(MudlibPath + "obj/master.c");
            ObjMgr.CallFunction("master", "create");
            ObjMgr.Preload(MudlibPath + "obj/player.c");
            ObjMgr.Preload(MudlibPath + "obj/login.c");
            
            // 載入房間藍本
            ObjMgr.Preload(MudlibPath + "room/town.c");
            ObjMgr.Preload(MudlibPath + "room/forest.c");

            var listener = new TcpListener(IPAddress.Any, 6900);
            listener.Start();
            Console.WriteLine("\n🚀 Lithos.NET Driver 啟動！請使用 Telnet 或 Python 腳本連線 6900 端口。\n");

            while (true) {
                var client = await listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        static async Task HandleClientAsync(TcpClient client) {
            var reader = PipeReader.Create(client.GetStream());
            var writer = PipeWriter.Create(client.GetStream());
            
            string currentObj = "login"; // 初始狀態為 login
            bool isLoggedIn = false;

            try {
                while (true) {
                    ReadResult result = await reader.ReadAsync();
                    ReadOnlySequence<byte> buffer = result.Buffer;

                    // 【核心】解析文字流，尋找 \n
                    SequencePosition? position;
                    while ((position = buffer.PositionOf((byte)'\n')) != null) {
                        var lineBytes = buffer.Slice(0, position.Value);
                        string line = Encoding.UTF8.GetString(lineBytes).Trim();
                        
                        if (!string.IsNullOrEmpty(line)) {
                            if (!isLoggedIn) {
                                // 登入流程
                                if (currentObj == "login") {
                                    // 簡單模擬：第一行帳號，第二行密碼
                                    // 這裡我們直接呼叫 verify_login，如果成功就 clone
                                    LpcValue res = ObjMgr.CallFunction("login", "verify_login", LpcValue.Create(line), LpcValue.Create("123456"));
                                    if (res.AsInt() == 1) {
                                        LpcValue playerObj = ObjMgr.CallFunction("login", "logon");
                                        currentObj = playerObj.AsString();
                                        SessionManager.Bind(currentObj, writer); // 綁定 TCP 連線
                                        ObjMgr.CallFunction(currentObj, "setup_user", LpcValue.Create(line));
                                        isLoggedIn = true;
                                        
                                        // 預設執行一次 look
                                        ObjMgr.CallFunction(currentObj, "command", LpcValue.Create("look"));
                                    } else {
                                        await SessionManager.SendAsync("system", "密碼錯誤，請重新輸入帳號："); // 簡化處理
                                    }
                                }
                            } else {
                                // 遊戲內指令路由
                                ObjMgr.CallFunction(currentObj, "command", LpcValue.Create(line));
                            }
                        }
                        
                        // 吃掉已處理的行 (包含 \n)
                        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
                    }

                    reader.AdvanceTo(buffer.Start, buffer.End);
                    if (result.IsCompleted) break;
                }
            } catch { }
            finally { 
                if (isLoggedIn) SessionManager.Unbind(currentObj);
                client.Close(); 
            }
        }
    }
}
