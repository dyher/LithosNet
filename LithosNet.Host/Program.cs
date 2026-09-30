using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using LithosNet.VM;
using LithosNet.Core;

namespace LithosNet.Host {
    class Program {
        static ObjectManager? ObjMgr;
        static ConfigManager? Config;

        static async Task Main(string[] args) {
            Console.WriteLine("==================================================");
            Console.WriteLine("🔥 LithosNet Driver (FluffOS Aligned)");
            Console.WriteLine("==================================================");

            // 1. 讀取 FluffOS 標準 Config
            Config = new ConfigManager();
            string configPath = args.Length > 0 ? args[0] : "mudlib/config.cfg";
            Config.Load(configPath);

            // 2. 初始化 ObjectManager 並注入 Config
            ObjMgr = new ObjectManager();
            ObjMgr.SetConfig(Config);
            
            // 【Phase 75: 接通電源】啟動 Efun 插件化 Registry
            EfunRegistry.RegisterFromType(typeof(BuiltInEfuns));

            // 3. 預載入 master 並執行 preload
            try {
                string masterVirtualPath = Config.MasterFile;
                // 【關鍵修復】LoadObject 使用虛擬路徑，但 CallFunction 需要物件名稱 (如 "master")
                string masterObjName = Path.GetFileNameWithoutExtension(masterVirtualPath);
                
                Console.WriteLine($"📦 Loading Master Object: {masterVirtualPath} (ObjName: {masterObjName})");
                
                ObjMgr.LoadObject(masterVirtualPath);
                await ObjMgr.EnqueueAndAwaitAsync(() => ObjMgr!.CallFunction(masterObjName, "preload", new LpcValue[0]));
                Console.WriteLine("✅ Master preload 完成。");
            } catch (Exception e) {
                Console.WriteLine($"⚠ Master preload 錯誤: {e}");
            }

            // 4. 啟動 TCP 監聽 (使用 Config 中的端口)
            var listener = new TcpListener(IPAddress.Any, Config.ExternalPort);
            listener.Start();
            Console.WriteLine($"🚀 Driver 啟動！監聽端口: {Config.ExternalPort}");

            // 5. 保持主執行緒存活，並簡單處理連線
            _ = Task.Run(async () => {
                while (true) {
                    try {
                        var client = await listener.AcceptTcpClientAsync();
                        Console.WriteLine($"🔌 新連線: {client.Client.RemoteEndPoint}");
                        // 【Phase 84.2】呼叫 master->connect(port)
                        try {
                            ObjMgr!.EnqueueAndAwaitAsync(() => {
                                var result = ObjMgr.CallFunction("master", "connect", new LpcValue[] {
                                    LpcValue.Create(Config!.ExternalPort)
                                });
                                Console.WriteLine($"✅ master->connect() 返回: {result.Type}");
                                return result; // 【關鍵修復】返回 lambda 結果
                            }).Wait();
                        } catch (Exception ex) {
                            Console.WriteLine($"⚠ master->connect() 錯誤: {ex.Message}");
                        }
                        // 【待辦】後續將此 client 交給 SessionManager 處理
                        client.Close(); 
                    } catch {
                        break;
                    }
                }
            });

            await Task.Delay(Timeout.Infinite);
        }
    }
}
