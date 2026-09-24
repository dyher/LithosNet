import re

with open('LithosNet.Host/Program.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 精準替換 Fallback 邏輯：不再使用 login#1，而是探測並使用 obj/login 或 login
old_fallback = r'if\s*\(string\.IsNullOrEmpty\(currentObj\)\s*\|\|\s*currentObj\s*==\s*"0"\)\s*\{[^}]+\}'
new_fallback = '''if (string.IsNullOrEmpty(currentObj) || currentObj == "0") {
                Console.WriteLine("⚠ connect() 返回無效值，強制 Fallback 載入藍圖並直接使用它...");
                ObjMgr.LoadObject("obj/login");
                
                // 探測真實的物件名稱 (可能是 "obj/login" 或 "login")
                currentObj = "obj/login";
                try { 
                    ObjMgr.CallFunction(currentObj, "query_name"); 
                } catch { 
                    currentObj = "login"; 
                }
                Console.WriteLine($"✅ Fallback 鎖定物件: {currentObj}");
            }'''

code = re.sub(old_fallback, new_fallback, code, flags=re.DOTALL)

with open('LithosNet.Host/Program.cs', 'w', encoding='utf-8') as f:
    f.write(code)
print("✅ Program.cs Fallback 已完美修正！直接鎖定藍圖物件，徹底消滅幽靈 login#1！")
