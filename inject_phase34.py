import os

# 1. 建立 Preprocessor.cs
preprocessor_code = """#nullable disable
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace LithosNet.Compiler {
    public static class Preprocessor {
        public static string Process(string source, string basePath) {
            // 處理 #include "file.h" 或 #include <file.h>
            string includePattern = @"#include\s+[<""']([^>""']+)[>""']";
            source = Regex.Replace(source, includePattern, match => {
                string fileName = match.Groups[1].Value;
                string fullPath = Path.Combine(basePath, fileName);
                if (!File.Exists(fullPath)) {
                    fullPath = Path.Combine("/home/tiny/LithosNet/mudlib/include", fileName);
                }
                if (File.Exists(fullPath)) {
                    return File.ReadAllText(fullPath);
                }
                return $"/* INCLUDE NOT FOUND: {fileName} */";
            });
            
            // 處理 #define MACRO value (簡單文本替換)
            string definePattern = @"#define\s+(\w+)\s+(.*)";
            var defines = Regex.Matches(source, definePattern);
            foreach (Match m in defines) {
                string macro = m.Groups[1].Value;
                string value = m.Groups[2].Value.Trim();
                source = source.Replace(m.Value, ""); 
                source = Regex.Replace(source, $@"\b{macro}\b", value);
            }
            return source;
        }
    }
}
"""
with open("LithosNet.Compiler/Preprocessor.cs", "w", encoding="utf-8") as f:
    f.write(preprocessor_code)
print("✅ Preprocessor.cs 已建立！")

# 2. 修改 ObjectManager.cs (在編譯前呼叫 Preprocessor)
with open("LithosNet.VM/ObjectManager.cs", "r", encoding="utf-8") as f:
    om = f.read()
if "Preprocessor.Process" not in om:
    om = om.replace("string src = File.ReadAllText(path);", "string src = File.ReadAllText(path);\n            src = LithosNet.Compiler.Preprocessor.Process(src, Path.GetDirectoryName(path));")
    with open("LithosNet.VM/ObjectManager.cs", "w", encoding="utf-8") as f:
        f.write(om)
    print("✅ ObjectManager.cs 已掛載 Preprocessor！")

# 3. 修改 Program.cs (雙軌制網路層：Text + Binary)
with open("LithosNet.Host/Program.cs", "r", encoding="utf-8") as f:
    prog = f.read()

new_network_loop = """            while (true) {
                    ReadResult result = await reader.ReadAsync();
                    ReadOnlySequence<byte> buffer = result.Buffer;
                    
                    while (true) {
                        if (buffer.Length == 0) break;
                        byte firstByte = buffer.FirstSpan[0];
                        
                        if (firstByte == 0xFF) {
                            // 【Binary Protocol】0xFF + 4 bytes Length + Payload
                            if (buffer.Length < 5) break;
                            var lenSpan = buffer.Slice(1, 4).FirstSpan;
                            int length = (lenSpan[0] << 24) | (lenSpan[1] << 16) | (lenSpan[2] << 8) | lenSpan[3];
                            if (buffer.Length < 5 + length) break;
                            
                            var payloadBytes = buffer.Slice(5, length);
                            string json = Encoding.UTF8.GetString(payloadBytes);
                            currentObj = SessionManager.GetObjName(writer);
                            if (!string.IsNullOrEmpty(currentObj)) {
                                try { ObjMgr.CallFunction(currentObj, "receive_binary", LpcValue.Create(json)); } catch {}
                            }
                            buffer = buffer.Slice(5 + length);
                        } else {
                            // 【Text Protocol】按 \\n 分割
                            SequencePosition? position = buffer.PositionOf((byte)'\\n');
                            if (position == null) break;
                            var lineBytes = buffer.Slice(0, position.Value);
                            string line = Encoding.UTF8.GetString(lineBytes).Trim();
                            currentObj = SessionManager.GetObjName(writer);
                            if (!string.IsNullOrEmpty(currentObj)) {
                                try { ObjMgr.CallFunction(currentObj, "receive_message", LpcValue.Create(line)); } catch {}
                            }
                            buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
                        }
                    }
                    reader.AdvanceTo(buffer.Start, buffer.End);
                    if (result.IsCompleted) break;
                }"""

# 替換舊的 while(true) 網路循環
import re
prog = re.sub(r'while \(true\) \{.*?if \(result\.IsCompleted\) break;\s*\}', new_network_loop, prog, flags=re.DOTALL)
with open("LithosNet.Host/Program.cs", "w", encoding="utf-8") as f:
    f.write(prog)
print("✅ Program.cs 已升級為雙軌制網路層 (Text + Binary)！")

# 4. 注入 json_decode Efun
with open("LithosNet.VM/BuiltInEfuns.cs", "r", encoding="utf-8") as f:
    efuns = f.read()

json_efun = """
        // 【MMORPG 通訊】json_decode (將 JSON 字串轉為 Mapping)
        [Efun("json_decode")]
        public static LpcValue JsonDecode(LpcValue[] args) {
            if (args.Length < 1) return LpcValue.Create(new Dictionary<string, LpcValue>());
            try {
                var doc = System.Text.Json.JsonDocument.Parse(args[0].AsString());
                var dict = new Dictionary<string, LpcValue>();
                foreach (var prop in doc.RootElement.EnumerateObject()) {
                    if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Number)
                        dict[prop.Name] = LpcValue.Create(prop.Value.GetInt32());
                    else if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                        dict[prop.Name] = LpcValue.Create(prop.Value.GetString());
                    else
                        dict[prop.Name] = LpcValue.Create(prop.Value.ToString());
                }
                return LpcValue.Create(dict);
            } catch { return LpcValue.Create(new Dictionary<string, LpcValue>()); }
        }
"""
if "[Efun(\"json_decode\")]" not in efuns:
    efuns = efuns.replace("public static class BuiltInEfuns {", "public static class BuiltInEfuns {\n" + json_efun)
    with open("LithosNet.VM/BuiltInEfuns.cs", "w", encoding="utf-8") as f:
        f.write(efuns)
    print("✅ BuiltInEfuns.cs 已注入 json_decode！")

# 5. 恢復 living.c 的 #include (現在 Preprocessor 支援了！)
with open("mudlib/std/living.c", "r", encoding="utf-8") as f:
    living = f.read()
if "#include <aoi.h>" not in living:
    living = living.replace("// 標準生物藍本", "// 標準生物藍本\n#include <aoi.h>")
    with open("mudlib/std/living.c", "w", encoding="utf-8") as f:
        f.write(living)
    print("✅ living.c 已恢復 #include <aoi.h>！")

