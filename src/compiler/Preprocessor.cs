using System;
using System.Collections.Generic;
using System.IO;
namespace LithosNet.Compiler;
public sealed class Preprocessor
{
    public Preprocessor(string root, List<string> inc) {}
    public string PreprocessFile(string path, out List<string> included) { included = new List<string>(); try { return File.ReadAllText(path); } catch { return null; } }
}
