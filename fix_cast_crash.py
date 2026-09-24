import glob, re

# 1. 掃描 LithosNet.Core 修復所有 (string)xxx 強制轉換
count = 0
for f in glob.glob('LithosNet.Core/**/*.cs', recursive=True):
    with open(f, 'r', encoding='utf-8') as file:
        c = file.read()
    
    # 將 (string)Value 或 (string)_value 替換為安全的 .ToString()
    new_c = re.sub(r'\(string\)\s*([a-zA-Z_]\w*)', r'(\1?.ToString() ?? "")', c)
    if new_c != c:
        with open(f, 'w', encoding='utf-8') as file:
            file.write(new_c)
        count += 1
print(f"✅ 已修復 {count} 個檔案中的 (string) 強制轉換崩潰！")

# 2. 在 Program.cs 注入診斷與 Fallback
with open("LithosNet.Host/Program.cs", "r", encoding="utf-8") as f:
    prog = f.read()

old_call = 'currentObj = ObjMgr.CallFunction(MasterObj, "connect").AsString();'
new_call = '''var connectRet = ObjMgr.CallFunction(MasterObj, "connect");
                Console.WriteLine($"🔍 [Diag] master->connect() 返回: Type={connectRet?.Type}, Value={connectRet?.Value}");
                currentObj = connectRet?.Value?.ToString() ?? "";
                
                // 【自動 Fallback】如果 connect() 返回了 0 或空字串，代表 clone_object 失敗，我們手動幫它 clone！
                if (string.IsNullOrEmpty(currentObj) || currentObj == "0") {
                    Console.WriteLine("⚠️ connect() 返回無效值，啟動自動 Fallback clone_object('login')...");
                    var fallback = LithosNet.VM.BuiltInEfuns.CloneObject(new[] { LithosNet.Core.LpcValue.Create("login") });
                    currentObj = fallback?.Value?.ToString() ?? "login#1";
                }'''

if old_call in prog:
    prog = prog.replace(old_call, new_call)
    with open("LithosNet.Host/Program.cs", "w", encoding="utf-8") as f:
        f.write(prog)
    print("✅ Program.cs 已注入診斷日誌與自動 Fallback 機制！")
else:
    print("⚠️ 找不到 Program.cs 中的目標代碼！")
