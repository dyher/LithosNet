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

                    // 嘗試解析 RO 0x0064 封包 (長度 55 bytes)
                    if (buffer.Length >= 55) {
                        var packet = buffer.Slice(0, 55).FirstSpan;
                        
                        if (packet[0] == 0x64 && packet[1] == 0x00) {
                            string user = Encoding.ASCII.GetString(packet.Slice(6, 24)).TrimEnd('\0');
                            string pass = Encoding.ASCII.GetString(packet.Slice(30, 24)).TrimEnd('\0');
                            
                            Console.WriteLine($"🔥 [Lithos.NET] 收到 0x0064 登入請求: [{user}] / [{pass}]");

                            // 構造 0x0069 封包 (79 bytes)
                            byte[] response = new byte[79];
                            response[0] = 0x69; response[1] = 0x00; 
                            response[2] = 0x4F; response[3] = 0x00; 
                            response[4] = 0x01; 
                            
                            byte[] serverName = Encoding.ASCII.GetBytes("Lithos.NET");
                            Array.Copy(serverName, 0, response, 54, serverName.Length);

                            await writer.WriteAsync(response);
                            await writer.FlushAsync();
                            Console.WriteLine("✅ [Lithos.NET] 成功發送 79 bytes 的 0x0069 封包！無損二進位傳輸！\n");
                        }
                        reader.AdvanceTo(buffer.GetPosition(55));
                    } else {
                        reader.AdvanceTo(buffer.Start, buffer.End);
                    }

                    if (result.IsCompleted) break;
                }
            } catch (Exception ex) {
                Console.WriteLine($"[-] 客戶端斷開或發生錯誤: {ex.Message}");
            } finally {
                reader.Complete();
                writer.Complete();
                client.Close();
            }
        }
    }
}
