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
            Console.WriteLine("🔥 [Phase 17] 注入 FluffOS 靈魂：Master Object 與 connect() Apply！");
            Console.WriteLine("==================================================\n");

            EfunRegistry.RegisterFromType(typeof(BuiltInEfuns));
            
            // 【關鍵】Driver 啟動時，第一個載入的必須是 master.c
            ObjMgr.Preload(MudlibPath + "master.c");
            ObjMgr.CallFunction("master", "create"); // 觸發 master 的初始化
            
            // 預載入其他基礎物件
            ObjMgr.Preload(MudlibPath + "monster.c");
            ObjMgr.Preload(MudlibPath + "dragon.c");
            ObjMgr.Preload(MudlibPath + "room.c");
            ObjMgr.Preload(MudlibPath + "login.c");

            var listener = new TcpListener(IPAddress.Any, 6900);
            listener.Start();
            Console.WriteLine("\n🚀 Lithos.NET Driver 啟動！監聽端口: 6900\n");

            while (true) {
                var client = await listener.AcceptTcpClientAsync();
                Console.WriteLine($"[+] 新 TCP 連線接入: {client.Client.RemoteEndPoint}");
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        static async Task HandleClientAsync(TcpClient client) {
            // 【FluffOS 標準流程】
            // 1. 呼叫 master->connect()，詢問 Master 應該把連線交給誰
            LpcValue targetObjVal = ObjMgr.CallFunction("master", "connect");
            string targetObj = targetObjVal.AsString();
            Console.WriteLine($"🔌 [NET] Master 指派互動物件: {targetObj}.c");
            
            // 2. 呼叫該物件的 logon() Apply
            ObjMgr.CallFunction(targetObj, "logon");

            var reader = PipeReader.Create(client.GetStream());
            var writer = PipeWriter.Create(client.GetStream());
            try {
                while (true) {
                    ReadResult result = await reader.ReadAsync();
                    ReadOnlySequence<byte> buffer = result.Buffer;
                    bool needsFlush = false;
                    while (TryParsePacket(ref buffer, writer, ref needsFlush)) { }
                    if (needsFlush) await writer.FlushAsync();
                    reader.AdvanceTo(buffer.Start, buffer.End);
                    if (result.IsCompleted) break;
                }
            } catch { }
            finally { client.Close(); }
        }

        static bool TryParsePacket(ref ReadOnlySequence<byte> buffer, PipeWriter writer, ref bool needsFlush) {
            if (buffer.Length < 55) return false;
            var pkt = buffer.Slice(0, 55).FirstSpan;
            if (pkt[0] == 0x64 && pkt[1] == 0x00) {
                string user = Encoding.ASCII.GetString(pkt.Slice(6, 24)).TrimEnd('\0');
                string pass = Encoding.ASCII.GetString(pkt.Slice(30, 24)).TrimEnd('\0');
                
                // 登入驗證依然交由 login.c 處理
                LpcValue result = ObjMgr.CallFunction("login", "verify_login", LpcValue.Create(user), LpcValue.Create(pass));
                
                byte[] response = new byte[79];
                if (result.AsInt() == 1) {
                    response[0] = 0x69; response[1] = 0x00; response[2] = 0x4F; response[3] = 0x00; response[4] = 0x01;
                } else {
                    response[0] = 0x6a; response[1] = 0x00; response[2] = 0x17; response[3] = 0x00; response[4] = 0x00;
                }
                writer.Write(response); needsFlush = true;
            }
            buffer = buffer.Slice(55);
            return true;
        }
    }
}
