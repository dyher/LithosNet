using System.IO.Pipelines;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using LithosNet.VM;
using LithosNet.Core;

namespace LithosNet.Host {
    class Program {
        static ObjectManager ObjMgr;
        static string MudlibPath;
        static string MasterObj;

        static async Task Main(string[] args) {
            Console.WriteLine("==================================================");
            Console.WriteLine("🔥 LithosNet Driver (FluffOS Aligned)");
            Console.WriteLine("==================================================");

            // 1. 讀取配置
            if (!File.Exists("config.json")) {
                Console.WriteLine("❌ 找不到 config.json");
                return;
            }
            var cfg = JsonDocument.Parse(File.ReadAllText("config.json")).RootElement;
            MudlibPath = cfg.GetProperty("mudlib_dir").GetString();
            MasterObj = cfg.GetProperty("master_object").GetString();
            int port = cfg.GetProperty("port").GetInt32();

            // 2. 初始化 ObjectManager
            ObjMgr = new ObjectManager();
            BuiltInEfuns.ObjMgr = ObjMgr;

            // 3. 預載入 master 並執行 preload
            try {
                ObjMgr.LoadObject(MudlibPath + "obj/" + MasterObj + ".c");
                await ObjMgr.EnqueueAndAwaitAsync(() => ObjMgr.CallFunction(MasterObj, "preload"));
                Console.WriteLine("✅ Master preload 完成。");
            } catch (Exception e) {
                Console.WriteLine($"⚠ Master preload 錯誤: {e}");
            }

            // 4. 啟動 TCP 監聽
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine($"🚀 Driver 啟動！監聽端口: {port}");

            while (true) {
                var client = await listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client);
            }
        }

        static async Task HandleClientAsync(TcpClient client) {
            string currentObj = "";
            try {
                // 1. master->connect()
                var connectRet = await ObjectManager.Instance.EnqueueAndAwaitAsync(() => ObjMgr.CallFunction(MasterObj, "connect"));
                currentObj = connectRet.AsString();

                if (string.IsNullOrEmpty(currentObj) || currentObj == "0") {
                    Console.WriteLine("⚠ connect() 返回無效值");
                    client.Close();
                    return;
                }

                // 2. 綁定 Session
                var writer = System.IO.Pipelines.PipeWriter.Create(client.GetStream());
                SessionManager.Bind(currentObj, writer); // TODO: 需要正確的 PipeWriter
                Console.WriteLine($"🔌 [Session] 綁定連線: {currentObj}");

                // 3. logon()
                await ObjectManager.Instance.EnqueueAndAwaitAsync(() => ObjMgr.CallFunction(currentObj, "logon"));

                // 4. 穩健的逐行讀取循環
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream)) {
                    while (client.Connected) {
                        // 【Phase 63: FluffOS 對齊】動態查詢當前綁定的物件名（exec() 後會改變）
                        currentObj = SessionManager.GetObjNameByWriter(writer) ?? currentObj;
                        
                        var line = await reader.ReadLineAsync();
                        
                        // 【Phase 63: FluffOS 對齊】客戶端斷開或物件銷毀時立即跳出
                        if (line == null) break;
                        
                        // 動態查詢當前物件名（exec 後會改變）
                        currentObj = SessionManager.GetObjNameByWriter(writer) ?? currentObj;
                        
                        // 檢查物件是否存在（可能被 destruct）
                        if (!ObjectManager.Instance.ObjectExists(currentObj)) {
                            Console.WriteLine($"⚠ [Session] Object '{currentObj}' destructed. Closing.");
                            break;
                        }
                        Console.WriteLine($"🔍 [Diag] 收到輸入: '{line}' (IsNullOrEmpty: {string.IsNullOrEmpty(line)})");

                        string trapFunc = SessionManager.GetAndClearInputTrap(currentObj);
                        if (!string.IsNullOrEmpty(trapFunc)) {
                            await ObjectManager.Instance.EnqueueAndAwaitAsync(() => ObjMgr.CallFunction(currentObj, trapFunc, new LpcValue[] { LpcValue.Create(line) }));
                        } else {
                            await ObjectManager.Instance.EnqueueAndAwaitAsync(() => ObjMgr.CallFunction(currentObj, "command", new LpcValue[] { LpcValue.Create(line) }));
                        }
                    }
                }

            } catch (Exception ex) {
                Console.WriteLine($"❌ [Session] 錯誤: {ex.Message}");
            } finally {
                if (!string.IsNullOrEmpty(currentObj)) {
                    SessionManager.Unbind(currentObj);
                    if (!string.IsNullOrEmpty(currentObj) && ObjectManager.Instance.ObjectExists(currentObj)) {
                    try { await ObjectManager.Instance.EnqueueAndAwaitAsync(() => ObjMgr.CallFunction(currentObj, "logoff")); } catch {}
                }
                }
                client.Close();
                Console.WriteLine($"❌ [Session] 斷開連線: {currentObj}");
            }
        }
    }
}