import re

with open('LithosNet.VM/BuiltInEfuns.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 1. 暴力替換所有 writer.Write(bytes) 為無腦廣播
broadcast_bytes = """var writers = SessionManager.GetAllWriters();
                    Console.WriteLine($"🔍 [Efun] 準備廣播 bytes 給 {writers.Count} 個客戶端...");
                    foreach(var _w in writers) {
                        try { _w.Write(bytes); _w.FlushAsync().AsTask().Wait(); Console.WriteLine("✅ [Efun] bytes Flush 成功！"); } catch {}
                    }"""

code = re.sub(
    r'[a-zA-Z_]\w*\.Write\(\s*bytes\s*\);',
    broadcast_bytes,
    code
)

# 2. 暴力替換所有 writer.Write(字串/變數) 為無腦廣播 (自動轉 UTF8 bytes)
broadcast_str = """var writers = SessionManager.GetAllWriters();
                    var _bytes = System.Text.Encoding.UTF8.GetBytes(msg.ToString() + "\\n");
                    Console.WriteLine($"🔍 [Efun] 準備廣播字串給 {writers.Count} 個客戶端: {msg}");
                    foreach(var _w in writers) {
                        try { _w.Write(_bytes); _w.FlushAsync().AsTask().Wait(); Console.WriteLine("✅ [Efun] 字串 Flush 成功！"); } catch {}
                    }"""

# 匹配 Write(非 bytes 的參數)
code = re.sub(
    r'[a-zA-Z_]\w*\.Write\(\s*(?!bytes\b)[a-zA-Z_][a-zA-Z0-9_\.]*\s*\);',
    broadcast_str,
    code
)

with open('LithosNet.VM/BuiltInEfuns.cs', 'w', encoding='utf-8') as f:
    f.write(code)
print("✅ BuiltInEfuns.cs 已無腦廣播化！所有 Efun 輸出將強制穿透到客戶端！")
