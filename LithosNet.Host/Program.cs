using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LithosNet.Host {
    class Program {
        static async Task Main(string[] args) {
            var listener = new TcpListener(IPAddress.Any, 6900);
            listener.Start();
            Console.WriteLine("🚀 Lithos.NET Driver (MVP) 啟動！監聽端口: 6900");
            Console.WriteLine("徹底告別 C 語言的 Telnet 包袱，擁抱現代零拷貝 (Zero-Copy) 網路層！\n");

            while (true) {
                var client = await listener.AcceptTcpClientAsync();
                Console.WriteLine($"[+] 新客戶端連線: {client.Client.RemoteEndPoint}");
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        static async Task HandleClientAsync(TcpClient client) {
            var reader = PipeReader.Create(client.GetStream());
            var writer = PipeWriter.Create(client.GetStream());

            try {
                while (true) {
                    ReadResult result = await reader.ReadAsync();
                    ReadOnlySequence<byte> buffer = result.Buffer;

                    bool needsFlush = false;
                    // 【黃金範式】在迴圈中呼叫同步方法處理 Span
                    while (TryParseAndProcessPacket(ref buffer, writer, ref needsFlush)) { }

                    // 【關鍵修復】如果有寫入數據，立刻 Flush 到底層 TCP Socket！
                    if (needsFlush) {
                        await writer.FlushAsync();
                    }

                    reader.AdvanceTo(buffer.Start, buffer.End);

                    if (result.IsCompleted) break;
                }
            } catch (Exception ex) {
                Console.WriteLine($"[-] 客戶端斷開或發生錯誤: {ex.Message}");
            } finally {
                await reader.CompleteAsync();
                await writer.CompleteAsync();
                client.Close();
            }
        }

        // 【核心】同步解析方法，可以安全地使用 Span<T>
        static bool TryParseAndProcessPacket(ref ReadOnlySequence<byte> buffer, PipeWriter writer, ref bool needsFlush) {
            if (buffer.Length < 55) return false;

            var packet = buffer.Slice(0, 55).FirstSpan;
            
            if (packet[0] == 0x64 && packet[1] == 0x00) {
                string user = Encoding.ASCII.GetString(packet.Slice(6, 24)).TrimEnd('\0');
                string pass = Encoding.ASCII.GetString(packet.Slice(30, 24)).TrimEnd('\0');
                
                Console.WriteLine($"🔥 [Lithos.NET] 收到 0x0064 登入請求: [{user}] / [{pass}]");

                byte[] response = new byte[79];
                response[0] = 0x69; response[1] = 0x00; 
                response[2] = 0x4F; response[3] = 0x00; 
                response[4] = 0x01; 
                
                byte[] serverName = Encoding.ASCII.GetBytes("Lithos.NET");
                Array.Copy(serverName, 0, response, 54, serverName.Length);

                // 寫入 Pipelines 緩衝區
                writer.Write(response);
                needsFlush = true; // 標記需要 Flush
                Console.WriteLine("✅ [Lithos.NET] 準備發送 79 bytes 的 0x0069 封包！\n");
            }
            
            buffer = buffer.Slice(55);
            return true;
        }
    }
}
