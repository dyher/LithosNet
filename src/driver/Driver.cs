using System;
using System.Collections.Generic;
using LithosNet.VM;

namespace LithosNet.Driver;

/// <summary>
/// Port of main.cpp / backend.c boot sequence
/// </summary>
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

        // 1. load master
        string masterPath = Config.MasterFile ?? "/obj/master";
        MasterObject = ObjectManager.LoadObject(masterPath);
        Console.WriteLine($"[Driver] master loaded: {masterPath}");

        // 2. get simul_efun
        try
        {
            var interp = new InterpreterV4();
            if (MasterObject.CurrentProgram != null) interp.SetProgram(MasterObject.CurrentProgram);
            var simPath = Config.SimulEfunFile ?? "/obj/simul_efun";
            try { SimulEfunObject = ObjectManager.LoadObject(simPath); Console.WriteLine($"[Driver] simul_efun loaded: {simPath}"); }
            catch (Exception ex) { Console.WriteLine($"[Driver] simul_efun not found: {ex.Message}"); }
        }
        catch {}

        // 3. master->preload() -> list of files to preload
        try
        {
            var interp = new InterpreterV4();
            if (MasterObject.CurrentProgram != null) interp.SetProgram(MasterObject.CurrentProgram);
            if (MasterObject.TryFindFunction(Apply.Preload, out _, out _))
            {
                var ret = interp.CallApply(Apply.Preload, Array.Empty<SValueS>(), MasterObject);
                if (ret.Type == SValueType.T_ARRAY && ret.Value.Array != null)
                {
                    var list = new List<string>();
                    foreach (var sv in ret.Value.Array.Items)
                        if (sv.Type == SValueType.T_STRING) list.Add(sv.String);
                    ObjectManager.Preload(list);
                }
            }
        }
        catch (Exception ex) { Console.WriteLine($"[Driver] preload failed: {ex.Message}"); }

        // 4. master->epilog()
        try
        {
            var interp = new InterpreterV4();
            if (MasterObject.CurrentProgram != null) interp.SetProgram(MasterObject.CurrentProgram);
            if (MasterObject.TryFindFunction(Apply.Epilog, out _, out _))
            {
                interp.CallApply(Apply.Epilog, Array.Empty<SValueS>(), MasterObject);
                Console.WriteLine("[Driver] epilog called, MUD ready");
            }
        }
        catch (Exception ex) { Console.WriteLine($"[Driver] epilog failed: {ex.Message}"); }
    }
}
