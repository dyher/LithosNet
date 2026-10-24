using System; using System.Reflection; using LithosNet.Compiler;
class Dump{ static void Main(){ var ctx=new CodeGenContext(); var block=ctx.CodeBlock; var t=block.GetType();
Console.WriteLine($"Type:{t.FullName}");
foreach(var f in t.GetFields(BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance))
 Console.WriteLine($"Field:{f.Name} {f.FieldType}");
foreach(var p in t.GetProperties(BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance))
 Console.WriteLine($"Prop:{p.Name} {p.PropertyType}");
foreach(var m in t.GetMethods(BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance))
 if(m.Name.Contains("Patch")||m.Name.Contains("Ins")||m.Name.Contains("Position")||m.Name.Contains("ToArray"))
  Console.WriteLine($"Method:{m.Name}");
}}
