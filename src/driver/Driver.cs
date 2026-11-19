using System;
using System.Collections.Generic;
using LithosNet.VM;

namespace LithosNet.Driver;

public sealed class Driver
{
    public DriverConfig Config { get; }
    public ObjectManager ObjectManager { get; }
    public LpcObject MasterObject { get; private set; }
    public LpcObject SimulEfunObject { get; private set; }
    public CallOutManager CallOuts { get; } = new();
    public HeartBeatManager HeartBeats { get; } = new();

    public Driver(DriverConfig cfg)
    {
        Config = cfg;
        ObjectManager = new ObjectManager(cfg.MudlibDir ?? Environment.CurrentDirectory);
    }

    public static Driver Start(string configPath)
    {
        var cfg = DriverConfig.ParseFile(configPath);
        var driver = new Driver(cfg);
        driver.Boot();
        return driver;
    }

    public void Boot()
    {
        Console.WriteLine($"[Driver] Mudlib={Config.MudlibDir} Master={Config.MasterFile}");

        string masterPath = Config.MasterFile ?? "/obj/master";
        MasterObject = ObjectManager.LoadObject(masterPath);
        Console.WriteLine($"[Driver] master loaded: {masterPath}");

        try
        {
            var interp = new InterpreterV4();
            if (MasterObject.CurrentProgram != null) interp.SetProgram(MasterObject.CurrentProgram);
            var simPath = Config.SimulEfunFile ?? "/obj/simul_efun";
            try { SimulEfunObject = ObjectManager.LoadObject(simPath); Console.WriteLine($"[Driver] simul_efun loaded: {simPath}"); }
            catch (Exception ex) { Console.WriteLine($"[Driver] simul_efun not found: {ex.Message}"); }
        }
        catch {}

        List<string> preloadList = new List<string>();
        try
        {
            var interp = new InterpreterV4();
            if (MasterObject.CurrentProgram != null) interp.SetProgram(MasterObject.CurrentProgram);
            if (MasterObject.TryFindFunction(Apply.Epilog, out _, out _))
            {
                try
                {
                    var arg0 = SValueS.FromNumber(0);
                    var ret = interp.CallApply(Apply.Epilog, new SValueS[]{ arg0 }, MasterObject);
                    var arr = ret.Value.Array ?? ret.U.Array;
                    if (ret.Value.Array != null)
                    {
                        foreach (var sv in ret.Value.Array.Items)
                            if (sv.Type == SValueType.T_STRING && !string.IsNullOrEmpty(sv.String)) preloadList.Add(sv.String);
                    }
                    else if (ret.U.Array != null)
                    {
                        foreach (var it in ret.U.Array.Items)
                        {
                            string str = it.String;
                            if (!string.IsNullOrEmpty(str)) preloadList.Add(str);
                        }
                    }
                    else if (ret.Value.Type == SValueType.T_ARRAY && ret.Value.Array != null)
                    {
                        foreach (var sv in ret.Value.Array.Items)
                            if (!string.IsNullOrEmpty(sv.String)) preloadList.Add(sv.String);
                    }
                    Console.WriteLine($"[Driver] epilog returned {preloadList.Count} preload files");
                }
                catch (Exception ex) { Console.WriteLine($"[Driver] epilog failed: {ex.Message} {ex.StackTrace}"); }
            }
            if (preloadList.Count == 0 && MasterObject.TryFindFunction(Apply.Preload, out _, out _))
            {
                try
                {
                    var interp2 = new InterpreterV4();
                    if (MasterObject.CurrentProgram != null) interp2.SetProgram(MasterObject.CurrentProgram);
                    var ret = interp2.CallApply(Apply.Preload, Array.Empty<SValueS>(), MasterObject);
                    var arr = ret.Value.Array ?? ret.U.Array;
                    if (arr != null)
                    {
                        foreach (var sv in arr.Items)
                        {
                            string str = sv.String;
                            if (!string.IsNullOrEmpty(str)) preloadList.Add(str);
                        }
                    }
                    Console.WriteLine($"[Driver] preload() returned {preloadList.Count} files");
                }
                catch {}
            }
            if (preloadList.Count > 0)
            {
                ObjectManager.Preload(preloadList);
            }
        }
        catch (Exception ex) { Console.WriteLine($"[Driver] preload/epilog failed: {ex.Message}"); }

        try
        {
            if (MasterObject.TryFindFunction(Apply.Preload, out _, out _))
            {
                foreach (var f in preloadList)
                {
                    try
                    {
                        var interp = new InterpreterV4();
                        if (MasterObject.CurrentProgram != null) interp.SetProgram(MasterObject.CurrentProgram);
                        var arg = new SValueS(f);
                        interp.CallApply(Apply.Preload, new SValueS[]{ arg }, MasterObject);
                    }
                    catch {}
                }
                Console.WriteLine("[Driver] preload(file) hooks called");
            }
        }
        catch {}

        Console.WriteLine("[Driver] epilog called, MUD ready");
        Console.WriteLine($"[Boot] SUCCESS - MUD ready, objects: {string.Join(",", ObjectManager.AllObjects())}");
    }
}
