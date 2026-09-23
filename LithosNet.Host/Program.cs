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
            Console.WriteLine("🔥 [Phase 20] 啟動多人互動與聊天系統！");
            Console.WriteLine("==================================================\n");

            EfunRegistry.RegisterFromType(typeof(BuiltInEfuns));
            
            ObjMgr.Preload(MudlibPath + "obj/master.c");
            ObjMgr.CallFunction("master", "create");
            ObjMgr.Preload(MudlibPath + "obj/player.c");
            ObjMgr.Preload(MudlibPath + "obj/login.c");
            ObjMgr.Preload(MudlibPath + "room/town.c");
            ObjMgr.Preload(MudlibPath + "room/forest.c");

            var listener = new TcpListener(IPAddress.Any, 6900);
            listener.Start();
            Console.WriteLine("\n🚀 Lithos.NET Driver 啟動！(多人模式)\n");

            while (true) {
                var client = await listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        static async Task HandleClientAsync(TcpClient client) {
            var reader = PipeReader.Create(client.GetStream());
            var writer = PipeWriter.Create(client.GetStream());
            string currentObj = "login";
            bool isLoggedIn = false;

            try {
                while (true) {
                    ReadResult result = await reader.ReadAsync();
                    ReadOnlySequence<byte> buffer = result.Buffer;
                    SequencePosition? position;
                    while ((position = buffer.PositionOf((byte)'\n')) != null) {
                        var lineBytes = buffer.Slice(0, position.Value);
                        string line = Encoding.UTF8.GetString(lineBytes).Trim();
                        
                        if (!string.IsNullOrEmpty(line)) {
                            if (!isLoggedIn) {
                                // 【簡化】第一行直接作為玩家名稱登入
                                string user_name = line;
                                LpcValue playerObj = ObjMgr.CallFunction("login", "logon");
                                currentObj = playerObj.AsString();
                                SessionManager.Bind(currentObj, writer);
                                ObjMgr.CallFunction(currentObj, "setup_user", LpcValue.Create(user_name));
                                isLoggedIn = true;
                                ObjMgr.CallFunction(currentObj, "command", LpcValue.Create("look"), LpcValue.Create(""));
                            } else {
                                // 【核心】將指令拆分為 verb 和 args
                                string[] parts = line.Split(new[] { ' ' }, 2);
                                string verb = parts[0];
                                string args_str = parts.Length > 1 ? parts[1] : "";
                                ObjMgr.CallFunction(currentObj, "command", LpcValue.Create(verb), LpcValue.Create(args_str));
                            }
                        }
                        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
                    }
                    reader.AdvanceTo(buffer.Start, buffer.End);
                    if (result.IsCompleted) break;
                }
            } catch { }
            finally { 
                if (isLoggedIn) { try { ObjMgr.CallFunction(currentObj, "logoff"); } catch {} SessionManager.Unbind(currentObj); }
                client.Close(); 
            }
        }
    }
}
