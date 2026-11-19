using System;
using System.IO;
using System.Collections.Generic;
using LithosNet.Compiler;

namespace LithosNet.VM;

public sealed class ObjectManager
{
    public static ObjectManager Current { get; private set; }
    public string MudlibRoot { get; }
    private readonly Dictionary<string, LpcObject> _objects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Program> _programs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _loading = new();

    public LpcObject MasterObject { get; private set; }
    public LpcObject SimulEfunObject { get; private set; }

    public ObjectManager(string mudlibRoot)
    {
        MudlibRoot = mudlibRoot ?? Directory.GetCurrentDirectory();
        Current = this;
    }

    public string ResolvePath(string lpcPath)
    {
        if (lpcPath.StartsWith("/")) lpcPath = lpcPath.Substring(1);
        var full = Path.Combine(MudlibRoot, lpcPath);
        if (File.Exists(full)) return full;
        if (File.Exists(full + ".c")) return full + ".c";
        if (File.Exists(full + ".lpc")) return full + ".lpc";
        if (File.Exists(full)) return full;
        return full;
    }

    static string NormalizeKey(string lpcPath)
    {
        string key = lpcPath.Trim();
        if (!key.StartsWith("/")) key = "/" + key;
        key = key.Replace(".c","").Replace(".lpc","");
        return key;
    }

    // 編譯並快取 Program，不建 live 物件、不調 create()。供 CloneObject 使用：blueprint 只是模板。
    public Program LoadProgram(string lpcPath)
    {
        string key = NormalizeKey(lpcPath);
        if (_programs.TryGetValue(key, out var cached)) return cached;
        if (_loading.Contains(key)) throw new Exception($"*Recursive load {key}");

        string file = ResolvePath(key);
        if (!File.Exists(file)) throw new FileNotFoundException($"Object not found: {key} -> {file}");

        _loading.Add(key);
        try
        {
            var incDirs = new System.Collections.Generic.List<string>();
            incDirs.Add(System.IO.Path.Combine(MudlibRoot, "include"));
            var pre = new Preprocessor(MudlibRoot, incDirs);
            pre.GlobalIncludePath = "<globals.h>";
            string processed;
            try { processed = pre.PreprocessFile(file, out var _); if (processed==null) processed = File.ReadAllText(file); }
            catch (Exception ex) { System.Console.WriteLine($"[preprocess fail {key}] {ex.Message}"); processed = File.ReadAllText(file); }

            var lexer = new Lexer(processed, file);
            var ctx = new CompilerContext();
            var parser = new Parser(lexer, ctx, file);
            var funcs = parser.ParseFunctions();

            if (parser.HasErrors)
            {
                foreach (var e in parser.Errors) Console.Error.WriteLine(e);
                throw new Exception($"Compile error in {key}");
            }

            LithosNet.Compiler.CodeGenerator.MudlibRoot = MudlibRoot;
            var codegen = new CodeGenerator(ctx.InnerContext);
            codegen.Globals = new System.Collections.Generic.List<LithosNet.Compiler.GlobalDecl>(parser.Globals);
            try {
                var fld = parser.GetType().GetField("Inherits");
                var inh = fld?.GetValue(parser) as List<string>;
                if (inh != null) codegen.Inherits = new List<string>(inh);
                else {
                    var prop = parser.GetType().GetProperty("Inherits");
                    var inh2 = prop?.GetValue(parser) as List<string>;
                    if (inh2 != null) codegen.Inherits = new List<string>(inh2);
                }
            } catch {}

            var prog = codegen.GenerateProgram(Path.GetFileNameWithoutExtension(file), funcs);
            _programs[key] = prog;
            return prog;
        }
        finally { _loading.Remove(key); }
    }

    public LpcObject LoadObject(string lpcPath)
    {
        string key = NormalizeKey(lpcPath);
        if (_objects.TryGetValue(key, out var existing)) return existing;

        var prog = LoadProgram(key);
        var obj = new LpcObject(key, prog);
        _objects[key] = obj;

        var interp = new InterpreterV4();
        interp.SetProgram(prog);
        if (obj.TryFindFunction("__global_init", out _, out _))
        {
            try { interp.CallApply("__global_init", System.Array.Empty<SValueS>(), obj); }
            catch (Exception ex) { Console.WriteLine($"[{key}::__global_init] {ex.Message}"); }
        }
        if (obj.TryFindFunction("create", out _, out _))
        {
            try { interp.CallApply("create", System.Array.Empty<SValueS>(), obj); Console.WriteLine($"[{key}::create] OK"); }
            catch (Exception ex) { Console.WriteLine($"[{key}::create] {ex.Message}"); }
        }

        if (key == "/obj/master" || key == "/secure/master" || key.EndsWith("/master")) MasterObject = obj;
        if (key == "/obj/simul_efun" || key.EndsWith("/simul_efun")) SimulEfunObject = obj;

        return obj;
    }

    // Neolith clone_object: 取 Program，並建一個 cold blueprint 存入 _objects（供 find_object/load_object 找），但不調 blueprint 的 create()
    // —— blueprint 只是模板，create() 只在 clone 上調，避免 blueprint 的 create() 副作用（如 move_object）污染世界
    // clone 不進 _objects（它是獨立 instance），ProgramName 跟 blueprint 相同
    public LpcObject CloneObject(string lpcPath)
    {
        string key = NormalizeKey(lpcPath);
        var prog = LoadProgram(key);
        if (!_objects.ContainsKey(key))
        {
            var bp = new LpcObject(key, prog);
            _objects[key] = bp;
        }
        var clone = new LpcObject(key, prog);
        var interp = new InterpreterV4();
        interp.SetProgram(prog);
        if (clone.TryFindFunction("__global_init", out _, out _))
        {
            try { interp.CallApply("__global_init", System.Array.Empty<SValueS>(), clone); }
            catch (Exception ex) { Console.WriteLine($"[{clone.ProgramName}::__global_init] {ex.Message}"); }
        }
        if (clone.TryFindFunction("create", out _, out _))
        {
            try { interp.CallApply("create", System.Array.Empty<SValueS>(), clone); Console.WriteLine($"[{clone.ProgramName}::create] OK"); }
            catch (Exception ex) { Console.WriteLine($"[{clone.ProgramName}::create] {ex.Message}"); }
        }
        return clone;
    }

    public LpcObject FindObject(string lpcPath)
    {
        string key = lpcPath.Trim().Replace(".c","");
        if (!key.StartsWith("/")) key = "/" + key;
        _objects.TryGetValue(key, out var ob);
        return ob;
    }

    public void Preload(IEnumerable<string> files)
    {
        foreach (var f in files)
        {
            try { LoadObject(f); Console.WriteLine($"[preload] {f}"); }
            catch (Exception ex) { Console.WriteLine($"[preload failed] {f}: {ex.Message}"); }
        }
    }

    public bool Destruct(LpcObject ob)
    {
        if(ob == null) return false;
        if(ob.Destructed) return false;
        ob.Destructed = true;
        string key = ob.ProgramName ?? "";
        if(string.IsNullOrEmpty(key)) return true;
        if(_objects.TryGetValue(key, out var cached) && ReferenceEquals(cached, ob)) _objects.Remove(key);
        return true;
    }

    public IEnumerable<string> AllObjects() => _objects.Keys;
}
