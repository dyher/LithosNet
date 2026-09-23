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
        static readonly string MudlibPath = "/home/tiny/LithosNet/mudlib/obj/";

        static async Task Main(string[] args) {
            Console.WriteLine("==================================================");
            Console.WriteLine("🔥 [Phase 18] 注入 MUD 靈魂：clone_object() 實例化與物件切換！");
            Console.WriteLine("==================================================\n");

            EfunRegistry.RegisterFromType(typeof(BuiltInEfuns));
            
            ObjMgr.Preload(MudlibPath + "master.c");
            ObjMgr.CallFunction("master", "create");
            ObjMgr.Preload(MudlibPath + "monster.c");
            ObjMgr.Preload(MudlibPath + "dragon.c");
            ObjMgr.Preload(MudlibPath + "room.c");
            ObjMgr.Preload(MudlibPath + "player.c"); // 預載入玩家藍本
            ObjMgr.Preload(MudlibPath + "login.c");

            var listener = new TcpListener(IPAddress.Any, 6900);
            listener.Start();
            Console.WriteLine("\n🚀 Lithos.NET Driver 啟動！監聽端口: 6900\n");

            while (true) {
                var client = await listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        static async Task HandleClientAsync(TcpClient client) {
            LpcValue targetObjVal = ObjMgr.CallFunction("master", "connect");
            string currentObj = targetObjVal.AsString(); // 初始為 "login"
            
            // 呼叫 login->logon()，並接收它返回的新物件 ID
            LpcValue newObjVal = ObjMgr.CallFunction(currentObj, "logon");
            if (newObjVal.Type == LpcType.String) {
                currentObj = newObjVal.AsString(); // 【關鍵】切換互動物件！
                Console.WriteLine($"🔄 [NET] 互動物件已切換為: {currentObj}");
                
                // 呼叫新物件的 setup_user
                ObjMgr.CallFunction(currentObj, "setup_user", LpcValue.Create("Admin"));
            }

            var reader = PipeReader.Create(client.GetStream());
            var writer = PipeWriter.Create(client.GetStream());
            try {
                while (true) {
                    ReadResult result = await reader.ReadAsync();
                    ReadOnlySequence<byte> buffer = result.Buffer;
                    bool needsFlush = false;
                    while (TryParsePacket(ref buffer, writer, ref needsFlush, currentObj)) { }
                    if (needsFlush) await writer.FlushAsync();
                    reader.AdvanceTo(buffer.Start, buffer.End);
                    if (result.IsCompleted) break;
                }
            } catch { }
            finally { client.Close(); }
        }

        // 將 currentObj 傳入，讓後續封包由 player#1 處理
        static bool TryParsePacket(ref ReadOnlySequence<byte> buffer, PipeWriter writer, ref bool needsFlush, string currentObj) {
            if (buffer.Length < 55) return false;
            var pkt = buffer.Slice(0, 55).FirstSpan;
            if (pkt[0] == 0x64 && pkt[1] == 0x00) {
                // 模擬玩家受到 200 點傷害
                Console.WriteLine($"⚔️ [NET] 玩家 {currentObj} 受到 200 點傷害！");
                ObjMgr.CallFunction(currentObj, "take_damage", LpcValue.Create(200));
                
                byte[] response = new byte[79];
                response[0] = 0x69; response[1] = 0x00; response[2] = 0x4F; response[3] = 0x00; response[4] = 0x01;
                writer.Write(response); needsFlush = true;
            }
            buffer = buffer.Slice(55);
            return true;
        }
    }
}
