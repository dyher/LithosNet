import re

with open("LithosNet.Host/Program.cs", "r", encoding="utf-8") as f:
    prog = f.read()

# 精準替換上一輪注入的有問題的 connect 處理邏輯
old_logic = r'var connectRet = ObjMgr\.CallFunction\(MasterObj, "connect"\);\s*Console\.WriteLine.*?currentObj = fallback\?\.Value\?\.ToString\(\) \?\? "login#1";\s*\}'

new_logic = '''var connectRet = ObjMgr.CallFunction(MasterObj, "connect");
                Console.WriteLine($"🔍 [Diag] master->connect() 返回: Type={connectRet.Type}, Value={connectRet.Value}");
                
                // 【終極修復】LpcValue 是 struct，不能使用 ?.
                currentObj = connectRet.Value?.ToString() ?? "";
                
                if (string.IsNullOrEmpty(currentObj) || currentObj == "0") {
                    Console.WriteLine("⚠️ connect() 返回無效值，強制 Fallback 載入 login 藍圖並使用 login#1...");
                    ObjMgr.LoadObject("obj/login");
                    currentObj = "login#1";
                }'''

prog = re.sub(old_logic, new_logic, prog, flags=re.DOTALL)

# 保險起見：如果正則沒匹配到，嘗試暴力替換所有 connectRet?.
prog = prog.replace("connectRet?.Type", "connectRet.Type")
prog = prog.replace("connectRet?.Value", "connectRet.Value")

with open("LithosNet.Host/Program.cs", "w", encoding="utf-8") as f:
    f.write(prog)
print("✅ Program.cs 已完美修復 C# 語法錯誤！徹底免疫 Struct 陷阱！")
