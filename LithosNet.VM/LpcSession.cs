using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using LithosNet.Core;

namespace LithosNet.VM
{
    public class LpcSession
    {
        public TcpClient Client { get; private set; }
        public NetworkStream Stream { get; private set; }
        public StreamReader Reader { get; private set; }
        public StreamWriter Writer { get; private set; }
        
        public string ObjectName { get; set; }  // 當前綁定的 LPC 物件名稱
        public string InputToFunc { get; set; }  // input_to 註冊的回調函數
        public string InputToObj { get; set; }   // input_to 的目標物件
        public ObjectManager ObjMgr { get; set; }
        
        public LpcSession(TcpClient client, ObjectManager objMgr)
        {
            Client = client;
            ObjMgr = objMgr;
            Stream = client.GetStream();
            Reader = new StreamReader(Stream, Encoding.UTF8);
            Writer = new StreamWriter(Stream, Encoding.UTF8) { AutoFlush = true };
        }
        
        public void Write(string message)
        {
            try {
                // 【Phase 86 修復】將 LPC 字串中未轉義的字面量 \n 轉換為真實換行符
                message = message.Replace("\\n", "\n").Replace("\\r", "\r");
                // 將真實的 \n 轉換為 \r\n (Telnet 協議標準)
                message = message.Replace("\n", "\r\n");
                Writer.Write(message);
            } catch (Exception ex) {
                Console.WriteLine($"[Session] Write error: {ex.Message}");
            }
        }
        
        public void Close()
        {
            try {
                Writer?.Close();
                Reader?.Close();
                Stream?.Close();
                Client?.Close();
            } catch { }
        }
        
        public async Task StartReceiveLoopAsync()
        {
            try {
                while (Client.Connected) {
                    string line = await Reader.ReadLineAsync();
                    if (line == null) break;
                    
                    Console.WriteLine($"[Session {ObjectName}] 收到輸入: {line}");
                    
                    // 如果有 input_to 註冊，呼叫回調函數
                    Console.WriteLine($"🔥 [Session {ObjectName}] 收到輸入: '{line}', InputToFunc='{InputToFunc}', InputToObj='{InputToObj}'");
                    if (!string.IsNullOrEmpty(InputToFunc)) {
                        string func = InputToFunc;
                        string targetObj = InputToObj ?? ObjectName;
                        Console.WriteLine($"🚀 [Session {ObjectName}] 準備呼叫 input_to 回調: {func} on {targetObj}");
                        InputToFunc = null;  // 一次性回調
                        InputToObj = null;
                        
                        try {
                            var result = await ObjMgr.EnqueueAndAwaitAsync(() => {
                                return ObjMgr.CallFunction(targetObj, func, new LpcValue[] { LpcValue.Create(line) });
                            });
                            Console.WriteLine($"✅ [Session {ObjectName}] input_to 回調執行成功，返回: {result.Type}");
                        } catch (Exception ex) {
                            Console.WriteLine($"❌ [Session {ObjectName}] input_to callback error: {ex.Message}\n{ex.StackTrace}");
                        }
                    } else {
                        // 否則嘗試呼叫 process_input apply
                        try {
                            await ObjMgr.EnqueueAndAwaitAsync(() => {
                                return ObjMgr.CallFunction(ObjectName, "process_input", new LpcValue[] { LpcValue.Create(line) });
                            });
                        } catch (Exception ex) {
                            Console.WriteLine($"[Session] process_input error: {ex.Message}");
                        }
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine($"[Session {ObjectName}] 連線中斷: {ex.Message}");
            } finally {
                Console.WriteLine($"[Session {ObjectName}] 連線關閉");
                Close();
                SessionManager.RemoveSession(this);
            }
        }
    }
}
