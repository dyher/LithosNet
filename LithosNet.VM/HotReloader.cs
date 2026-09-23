using System;
using System.IO;
using System.Threading;

namespace LithosNet.VM {
    public class HotReloader {
        private readonly FileSystemWatcher _watcher;
        private readonly ObjectManager _objMgr;
        private DateTime _lastRead = DateTime.MinValue;

        public HotReloader(string watchPath, ObjectManager objMgr) {
            _objMgr = objMgr;
            _watcher = new FileSystemWatcher(watchPath, "*.c") {
                // 【核心修復】同時監聽內容修改、檔案建立與重新命名 (sed -i 會觸發這些)
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };
            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.Renamed += OnRenamed;
            Console.WriteLine($"👁️ [HotReloader] 正在監聽目錄 (包含 Rename/Create): {watchPath}");
        }

        private void OnRenamed(object sender, RenamedEventArgs e) => OnChanged(sender, e);

        private void OnChanged(object sender, FileSystemEventArgs e) {
            if (DateTime.Now.Subtract(_lastRead).TotalMilliseconds < 500) return;
            _lastRead = DateTime.Now;
            
            Thread.Sleep(100); 
            try {
                if (File.Exists(e.FullPath)) {
                    _objMgr.ReloadObject(e.FullPath);
                }
            } catch (Exception ex) {
                Console.WriteLine($"❌ [HotReloader] 編譯失敗: {ex.Message}");
            }
        }
    }
}
