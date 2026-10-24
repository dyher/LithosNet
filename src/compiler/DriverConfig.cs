using System;
using System.Collections.Generic;
using System.IO;

namespace LithosNet.Driver
{
    // Parses src/neolith.conf from neolith-1.0.0-alpha.10
    public sealed class DriverConfig
    {
        public string MudName { get; set; } = "Eastern Stories II";
        public List<(int port, string proto)> Ports { get; } = new();
        public string MudlibDir { get; set; } = "/home/es2/mudlib";
        public string LogDir { get; set; } = "/home/es2/mudlib/log";
        public string DebugLogFile { get; set; } = "debug.log";
        public bool LogWithDate { get; set; } = true;
        public bool CrashDropCore { get; set; } = false;
        public string IncludeDir { get; set; } = "/include";
        public string GlobalInclude { get; set; } = "<globals.h>";
        public string SaveBinaryDir { get; set; } = "/bin";
        public string MasterFile { get; set; } = "/adm/obj/master.c";
        public string SimulEfunFile { get; set; } = "/adm/obj/simul_efun.c";
        public string DefaultErrorMsg { get; set; } = "You found something with the world!";
        public string DefaultFailMsg { get; set; } = "What?";
        public int CleanupDuration { get; set; } = 600;
        public int ResetDuration { get; set; } = 1800;
        public int MaxInheritDepth { get; set; } = 30;
        public int MaxEvaluationCost { get; set; } = 1000000;
        public int MaxArraySize { get; set; } = 15000;
        public int MaxMappingSize { get; set; } = 15000;
        public int MaxBufferSize { get; set; } = 400000;
        public int MaxStringLength { get; set; } = 200000;
        public int MaxBitFieldBits { get; set; } = 1200;
        public int MaxByteTransfer { get; set; } = 10000;
        public int MaxReadFileSize { get; set; } = 200000;
        public int SharedStringHashSize { get; set; } = 20011;
        public int ObjectHashSize { get; set; } = 10007;
        public int StackSize { get; set; } = 1000;
        public int MaxLocalVariables { get; set; } = 25;
        public int MaxCallDepth { get; set; } = 50;
        public bool ArgumentsInTrace { get; set; } = true;
        public bool LocalVariablesInTrace { get; set; } = true;

        // Resolver TTLs from neolith.conf
        public int ResolverForwardCacheTtl { get; set; } = 300;
        public int ResolverReverseCacheTtl { get; set; } = 900;
        public int ResolverNegativeCacheTtl { get; set; } = 30;
        public int ResolverStaleRefreshWindow { get; set; } = 30;

        public static DriverConfig ParseFile(string path)
        {
            var cfg = new DriverConfig();
            if (!File.Exists(path)) return cfg;
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                var parts = line.Split(new[]{' ', '\t'}, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                var key = parts[0];
                var val = parts[1].Trim();
                switch (key)
                {
                    case "MudName": cfg.MudName = val; break;
                    case "Port":
                        var pp = val.Split(':');
                        if (pp.Length==2 && int.TryParse(pp[0], out var port)) cfg.Ports.Add((port, pp[1]));
                        break;
                    case "MudlibDir": cfg.MudlibDir = val; break;
                    case "LogDir": cfg.LogDir = val; break;
                    case "DebugLogFile": cfg.DebugLogFile = val; break;
                    case "LogWithDate": cfg.LogWithDate = val.ToLower()=="yes"; break;
                    case "CrashDropCore": cfg.CrashDropCore = val.ToLower()=="yes"; break;
                    case "IncludeDir": cfg.IncludeDir = val; break;
                    case "GlobalInclude": cfg.GlobalInclude = val; break;
                    case "SaveBinaryDir": cfg.SaveBinaryDir = val; break;
                    case "MasterFile": cfg.MasterFile = val; break;
                    case "SimulEfunFile": cfg.SimulEfunFile = val; break;
                    case "DefaultErrorMsg": cfg.DefaultErrorMsg = val; break;
                    case "DefaultFailMsg": cfg.DefaultFailMsg = val; break;
                    case "CleanupDuration": if(int.TryParse(val, out var cd)) cfg.CleanupDuration=cd; break;
                    case "ResetDuration": if(int.TryParse(val, out var rd)) cfg.ResetDuration=rd; break;
                    case "MaxInheritDepth": if(int.TryParse(val, out var mid)) cfg.MaxInheritDepth=mid; break;
                    case "MaxEvaluationCost": if(int.TryParse(val, out var mec)) cfg.MaxEvaluationCost=mec; break;
                    case "MaxArraySize": if(int.TryParse(val, out var mas)) cfg.MaxArraySize=mas; break;
                    case "MaxMappingSize": if(int.TryParse(val, out var mms)) cfg.MaxMappingSize=mms; break;
                    case "MaxLocalVariables": if(int.TryParse(val, out var mlv)) cfg.MaxLocalVariables=mlv; break;
                    case "MaxCallDepth": if(int.TryParse(val, out var mcd)) cfg.MaxCallDepth=mcd; break;
                    case "StackSize": if(int.TryParse(val, out var ss)) cfg.StackSize=ss; break;
                }
            }
            return cfg;
        }

        public void Dump()
        {
            Console.WriteLine($"MudName: {MudName}");
            Console.WriteLine($"Ports: {string.Join(", ", Ports)}");
            Console.WriteLine($"MudlibDir: {MudlibDir}");
            Console.WriteLine($"MasterFile: {MasterFile}");
            Console.WriteLine($"SimulEfunFile: {SimulEfunFile}");
            Console.WriteLine($"StackSize: {StackSize} MaxCallDepth: {MaxCallDepth}");
        }
    }
}

