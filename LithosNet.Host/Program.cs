using System;
using System.IO;
using System.Threading;
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

            Config = new ConfigManager();
            string configPath = args.Length > 0 ? args[0] : "mudlib/config.cfg";
            Config.Load(configPath);

            ObjMgr = new ObjectManager();
            ObjMgr.SetConfig(Config);
            
            EfunRegistry.RegisterFromType(typeof(BuiltInEfuns));
            SessionManager.Initialize(ObjMgr);

            try {
                string masterVirtualPath = Config.MasterFile;
                string masterObjName = Path.GetFileNameWithoutExtension(masterVirtualPath);
                
                Console.WriteLine($"📦 Loading Master Object: {masterVirtualPath}");
                ObjMgr.LoadObject(masterVirtualPath);
                await ObjMgr.EnqueueAndAwaitAsync(() => ObjMgr!.CallFunction(masterObjName, "preload", new LpcValue[0]));
                Console.WriteLine("✅ Master preload 完成。");
            } catch (Exception e) {
                Console.WriteLine($"⚠ Master preload 錯誤: {e}");
            }

            // 【Phase 86】啟動 SessionManager 接管所有連線
            _ = SessionManager.StartAsync(Config.ExternalPort);
            Console.WriteLine($"🚀 Driver 啟動！監聽端口: {Config.ExternalPort}");
            Console.WriteLine("💡 提示: 使用 'telnet localhost " + Config.ExternalPort + "' 連線");

            await Task.Delay(Timeout.Infinite);
        }
    }
}
