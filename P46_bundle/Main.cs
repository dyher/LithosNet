using System;
using System.IO;
using System.Collections.Generic;
using LithosNet.VM;
using LithosNet.Compiler;

namespace LithosNet.Driver;

public static class ProgramDriver
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: --compile <file> | --run <file>");
            return;
        }

        if (args[0] == "--compile" && args.Length >= 2)
        {
            var path = args[1];
            string mudlibRoot = Directory.GetCurrentDirectory();
            var pre = new Preprocessor(mudlibRoot, null);
            string processed;
            try
            {
                processed = pre.PreprocessFile(path, out var included);
                if (processed == null)
                    processed = File.ReadAllText(path);
            }
            catch
            {
                processed = File.ReadAllText(path);
            }

            var lexer = new Lexer(processed, path);
            var ctx = new CompilerContext();
            var parser = new Parser(lexer, ctx, path);
            var funcs = parser.ParseFunctions();

            if (parser.HasErrors)
            {
                foreach (var err in parser.Errors)
                    Console.Error.WriteLine(err);
            }

            var codegen = new CodeGenerator(ctx.InnerContext);
            try { var fld = parser.GetType().GetField("Inherits"); if (fld != null) { var inh = fld.GetValue(parser) as List<string>; if (inh != null) codegen.Inherits = new List<string>(inh); } else { var prop = parser.GetType().GetProperty("Inherits"); if (prop != null) { var inh = prop.GetValue(parser) as List<string>; if (inh != null) codegen.Inherits = new List<string>(inh); } } } catch {}
            var prog = codegen.GenerateProgram(Path.GetFileNameWithoutExtension(path), funcs);

            Console.WriteLine($"Program: {prog.Name}");
            Console.WriteLine($"Code size: {prog.Code?.Length ?? 0}");
            Console.WriteLine($"Code: {BitConverter.ToString(prog.Code ?? Array.Empty<byte>())}");
            Console.WriteLine($"Functions: {prog.FunctionTable?.Length ?? 0}");
            foreach (var f in prog.FunctionTable ?? Array.Empty<CompilerFunction>())
            {
                Console.WriteLine($"  {f.Name} @ {f.Address}");
            }
            Console.WriteLine($"Strings: {prog.Strings?.Length ?? 0}");
            try { File.WriteAllBytes(path + ".bin", prog.Code ?? Array.Empty<byte>()); Console.WriteLine($"Saved: {path}.bin"); } catch { }
            return;
        }

        if (args[0] == "--run" && args.Length >= 2)
        {
            var path = args[1];
            string mudlibRoot = Directory.GetCurrentDirectory();
            var pre = new Preprocessor(mudlibRoot, null);
            string processed;
            try
            {
                processed = pre.PreprocessFile(path, out var included);
                if (processed == null)
                    processed = File.ReadAllText(path);
            }
            catch
            {
                processed = File.ReadAllText(path);
            }

            var lexer = new Lexer(processed, path);
            var ctx = new CompilerContext();
            var parser = new Parser(lexer, ctx, path);
            var funcs = parser.ParseFunctions();

            if (parser.HasErrors)
            {
                foreach (var err in parser.Errors)
                    Console.Error.WriteLine(err);
            }

            var codegen = new CodeGenerator(ctx.InnerContext);
            try { var fld = parser.GetType().GetField("Inherits"); if (fld != null) { var inh = fld.GetValue(parser) as List<string>; if (inh != null) codegen.Inherits = new List<string>(inh); } else { var prop = parser.GetType().GetProperty("Inherits"); if (prop != null) { var inh = prop.GetValue(parser) as List<string>; if (inh != null) codegen.Inherits = new List<string>(inh); } } } catch {}
            var prog = codegen.GenerateProgram(Path.GetFileNameWithoutExtension(path), funcs);

            Console.WriteLine($"Program: {prog.Name} Code size: {prog.Code?.Length ?? 0}");
            var interp = new InterpreterV4();
            interp.SetProgram(prog);
            try
            {
                long result = interp.RunFromMain();
                Console.WriteLine($"Result: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Run error: {ex}");
            }
            return;
        }

        Console.WriteLine("Unknown args: use --compile <file> or --run <file>");
    }
}
