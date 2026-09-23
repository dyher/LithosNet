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
        static readonly string MudlibPath = "/home/tiny/LithosNet/mudlib/obj/login.c";

        static async Task Main(string[] args) {
            Console.WriteLine("==================================================");
            Console.WriteLine("🔥 [Phase 7] 注入 MUD 靈魂：Efun 與 Apply 機制！");
            Console.WriteLine("==================================================\n");

            // 【關鍵】啟動時掃描並註冊所有底層 Efun
            EfunRegistry.RegisterFromType(typeof(BuiltInEfuns));
            Console.WriteLine();

            ObjMgr.LoadObject(MudlibPath);

            var listener = new TcpListener(IPAddress.Any, 6900);
            listener.Start();
            Console.WriteLine("\n🚀 Lithos.NET Driver 啟動！監聽端口: 6900\n");

            while (true) {
                var client = await listener.AcceptTcpClientAsync();
                Console.WriteLine($"[+] 新客戶端連線: {client.Client.RemoteEndPoint}");
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        static async Task HandleClientAsync(TcpClient client) {
            // 【Apply 機制】玩家一連線，立刻自動呼叫 LPC 的 logon()！
            Console.WriteLine("⚡ [NET] 觸發 Apply: 呼叫 login.c 的 logon()");
            ObjMgr.CallFunction(MudlibPath, "logon");

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
                Console.WriteLine($"\n🔥 [NET] 收到 0x0064 登入請求: [{user}] / [{pass}]");

                LpcValue result = ObjMgr.CallFunction(MudlibPath, "verify_login",
                    LpcValue.Create(user), LpcValue.Create(pass));

                byte[] response = new byte[79];
                if (result.AsInt() == 1) {
                    response[0] = 0x69; response[1] = 0x00;
                    response[2] = 0x4F; response[3] = 0x00;
                    response[4] = 0x01;
                    Console.WriteLine("✅ [NET] LPC 驗證通過！發送 0x0069 (Accept)");
                } else {
                    response[0] = 0x6a; response[1] = 0x00;
                    response[2] = 0x17; response[3] = 0x00;
                    response[4] = 0x00;
                    Console.WriteLine("❌ [NET] LPC 驗證失敗！發送 0x006a (Reject)");
                }

                writer.Write(response);
                needsFlush = true;
            }

            buffer = buffer.Slice(55);
            return true;
        }
    }
}
