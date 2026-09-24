import re

with open('LithosNet.Host/Program.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 1. 確保呼叫 logon() apply (尋找 SessionManager.Bind 或 currentObj 賦值後)
if 'CallFunction(currentObj, "logon")' not in code and 'CallFunction("login#1", "logon")' not in code:
    code = re.sub(
        r'(SessionManager\.Bind[^;]+;)',
        r'\1\n                Console.WriteLine($"🔍 [Diag] 準備呼叫 {currentObj}->logon()...");\n                try { ObjMgr.CallFunction(currentObj, "logon"); Console.WriteLine("✅ logon() 呼叫成功！"); } catch (Exception ex) { Console.WriteLine($"❌ logon() 呼叫失敗: {ex}"); }',
        code
    )
    # 備用匹配 (如果沒有 SessionManager.Bind)
    code = re.sub(
        r'(currentObj\s*=\s*"login#1";)',
        r'\1\n                Console.WriteLine($"🔍 [Diag] Fallback 準備呼叫 {currentObj}->logon()...");\n                try { ObjMgr.CallFunction(currentObj, "logon"); Console.WriteLine("✅ logon() 呼叫成功！"); } catch (Exception ex) { Console.WriteLine($"❌ logon() 呼叫失敗: {ex}"); }',
        code
    )

# 2. 在 while(true) 讀取迴圈注入 X 光日誌
code = re.sub(
    r'while\s*\(\s*true\s*\)\s*\{',
    r'Console.WriteLine("🔍 [Diag] 進入 TCP 讀取迴圈...");\n                while (true) {',
    code
)

code = re.sub(
    r'(var readResult = await reader\.ReadAsync\(\);)',
    r'Console.WriteLine("🔍 [Diag] 等待 PipeReader 資料...");\n                    \1',
    code
)

code = re.sub(
    r'if\s*\(\s*readResult\.IsCompleted\s*\)\s*\{',
    r'if (readResult.IsCompleted) {\n                        Console.WriteLine("⚠️ [Diag] PipeReader 收到 EOF，客戶端可能已斷開！");',
    code
)

with open('LithosNet.Host/Program.cs', 'w', encoding='utf-8') as f:
    f.write(code)
print("✅ Program.cs 已注入 logon() 呼叫與 X 光讀取迴圈日誌！")
