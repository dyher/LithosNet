with open('LithosNet.Host/Program.cs', 'r', encoding='utf-8') as f:
    prog = f.read()

# 1. 在 Text 處理前刷新 currentObj
update_text = """var activeObj = SessionManager.GetObjName(writer);
                        if (!string.IsNullOrEmpty(activeObj)) currentObj = activeObj;
                        Console.WriteLine($"🔥 [X-Ray] Calling receive_message on {currentObj} with: {line}");"""

prog = prog.replace(
    'Console.WriteLine($"🔥 [X-Ray] Calling receive_message on {currentObj} with: {line}");',
    update_text
)

# 2. 在 Binary 處理前刷新 currentObj
update_bin = """var activeObj = SessionManager.GetObjName(writer);
                        if (!string.IsNullOrEmpty(activeObj)) currentObj = activeObj;
                        Console.WriteLine($"🔥 [X-Ray] Calling receive_binary on {currentObj} with: {json}");"""

prog = prog.replace(
    'Console.WriteLine($"🔥 [X-Ray] Calling receive_binary on {currentObj} with: {json}");',
    update_bin
)

with open('LithosNet.Host/Program.cs', 'w', encoding='utf-8') as f:
    f.write(prog)
print('✅ Program.cs 已透過 Heredoc 完美注入動態刷新邏輯！currentObj 將完美跟隨 exec() 轉移！')
