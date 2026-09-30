using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LithosNet.Core
{
    public class ConfigManager
    {
        private readonly Dictionary<string, string> _config = new(StringComparer.OrdinalIgnoreCase);
        
        public string MudlibDirectory { get; private set; } = "./mudlib";
        public string LogDirectory { get; private set; } = "/log";
        public string MasterFile { get; private set; } = "/obj/master";
        public string SimulatedEfunFile { get; private set; } = "";
        public string IncludeDirectories { get; private set; } = "/include";
        public int ExternalPort { get; private set; } = 4000;

        public void Load(string configPath)
        {
            if (!File.Exists(configPath))
            {
                Console.WriteLine($"⚠️ Config file not found: {configPath}. Using defaults.");
                return;
            }

            Console.WriteLine($"📂 Loading FluffOS config: {configPath}");
            var lines = File.ReadAllLines(configPath);
            
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                // 忽略空行和註解
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                // 尋找第一個冒號作為分隔符
                int colonIdx = line.IndexOf(':');
                if (colonIdx == -1) continue;

                string key = line.Substring(0, colonIdx).Trim().ToLower();
                string value = line.Substring(colonIdx + 1).Trim();

                // 移除首尾的雙引號 (FluffOS config 常見格式)
                if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2)
                {
                    value = value.Substring(1, value.Length - 2);
                }

                _config[key] = value;
            }

            // 解析核心路徑與設定
            MudlibDirectory = GetPath("mudlib directory", "./mudlib");
            LogDirectory = GetPath("log directory", "/log");
            MasterFile = GetString("master file", "/obj/master");
            SimulatedEfunFile = GetString("simulated efun file", "");
            IncludeDirectories = GetString("include directories", "/include");

            // 解析 external_port_1 (格式通常為: telnet 4000)
            string portStr = GetString("external_port_1", "telnet 4000");
            var parts = portStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && int.TryParse(parts.Last(), out int port))
            {
                ExternalPort = port;
            }
            
            Console.WriteLine($"✅ Config loaded successfully.");
            Console.WriteLine($"   Mudlib Dir: {MudlibDirectory}");
            Console.WriteLine($"   Master: {MasterFile}");
            Console.WriteLine($"   Port: {ExternalPort}");
        }

        public string GetString(string key, string defaultValue = "")
        {
            return _config.TryGetValue(key, out var val) ? val : defaultValue;
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            if (_config.TryGetValue(key, out var val) && int.TryParse(val, out int res)) return res;
            return defaultValue;
        }

        public string GetPath(string key, string defaultValue = "")
        {
            string val = GetString(key, defaultValue);
            // 將相對路徑轉換為絕對路徑 (相對於當前執行目錄)
            if (!string.IsNullOrEmpty(val) && !Path.IsPathRooted(val))
            {
                return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), val));
            }
            return val;
        }

        /// <summary>
        /// 解析 Mudlib 內部的虛擬路徑 (例如將 "/obj/master" 轉換為實際硬碟路徑)
        /// </summary>
        public string ResolveMudlibPath(string virtualPath)
        {
            if (string.IsNullOrEmpty(virtualPath)) return virtualPath;
            
            // 移除開頭的 / 以結合物理路徑
            string cleanPath = virtualPath.TrimStart('/', '\\');
            return Path.Combine(MudlibDirectory, cleanPath);
        }
    }
}
