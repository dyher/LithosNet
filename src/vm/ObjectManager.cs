using System;
using System.IO;
using System.Collections.Generic;
using LithosNet.Compiler;

namespace LithosNet.VM;

public sealed class ObjectManager
{
    public string MudlibRoot { get; }
    private readonly Dictionary<string, LpcObject> _objects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Program> _programs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _loading = new();

    public LpcObject MasterObject { get; private set; }
    public LpcObject SimulEfunObject { get; private set; }

    public ObjectManager(string mudlibRoot)
    {
        MudlibRoot = mudlibRoot ?? Directory.GetCurrentDirectory();
    }

    public string ResolvePath(string lpcPath)
    {
        if (lpcPath.StartsWith("/")) lpcPath = lpcPath.Substring(1);
        var full = Path.Combine(MudlibRoot, lpcPath);
        if (File.Exists(full)) return full;
        if (File.Exists(full + ".c")) return full + ".c";
        return full;
    }

    public LpcObject LoadObject(string lpcPath)
    {
        string key = lpcPath.Trim();
        if (!key.StartsWith("/")) key = "/" + key;
        key = key.Replace(".c","");

        if (_objects.TryGetValue(key, out var existing)) return existing;
        if (_loading.Contains(key)) throw new Exception($"*Recursive load {key}");

        string file = ResolvePath(key);
        if (!File.Exists(file)) throw new FileNotFoundException($"Object not found: {key} -> {file}");

        _loading.Add(key);
        try
        {
            var pre = new Preprocessor(MudlibRoot, null);
            string processed;
            try { processed = pre.PreprocessFile(file, out var _); if (processed==null) processed = File.ReadAllText(file); }
            catch { processed = File.ReadAllText(file); }

            var lexer = new Lexer(processed, file);
            var ctx = new CompilerContext();
            var parser = new Parser(lexer, ctx, file);
            var funcs = parser.ParseFunctions();

            if (parser.HasErrors)
            {
                foreach (var e in parser.Errors) Console.Error.WriteLine(e);
                throw new Exception($"Compile error in {key}");
            }

            var codegen = new CodeGenerator(ctx.InnerContext);
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

            var obj = new LpcObject(key, prog);
            _objects[key] = obj;

            var interp = new InterpreterV4();
            interp.SetProgram(prog);
            if (obj.TryFindFunction("create", out _, out _))
            {
                try { interp.CallApply("create", System.Array.Empty<SValueS>(), obj); Console.WriteLine($"[{key}::create] OK"); }
                catch (Exception ex) { Console.WriteLine($"[{key}::create] {ex.Message}"); }
            }

            if (key == "/obj/master" || key == "/secure/master" || key.EndsWith("/master")) MasterObject = obj;
            if (key == "/obj/simul_efun" || key.EndsWith("/simul_efun")) SimulEfunObject = obj;

            return obj;
        }
        finally { _loading.Remove(key); }
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

    public IEnumerable<string> AllObjects() => _objects.Keys;
}
