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
            Console.WriteLine("🔥 [Phase 14] 注入 LPC 靈魂：跨物件通訊 (Call Other ->)！");
            Console.WriteLine("==================================================\n");

            EfunRegistry.RegisterFromType(typeof(BuiltInEfuns));
            
            // 【關鍵】預載入所有需要的 LPC 物件
            ObjMgr.Preload(MudlibPath + "room.c");
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
            // 觸發 Apply
            ObjMgr.CallFunction("login", "logon");

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
