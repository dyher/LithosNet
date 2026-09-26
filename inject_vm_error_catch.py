with open('LithosNet.VM/Interpreter.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 尋找 VisitBlock 中遍歷 Statements 的地方，注入 try/catch
if '❌ [VM Block Error]' not in code:
    code = code.replace(
        'Visit(stmt);',
        'try { Visit(stmt); } catch (Exception ex) { Console.WriteLine($"❌ [VM Block Error] Stmt {stmt.GetType().Name} failed: {ex.Message}"); }',
        1 # 只替換第一次出現的 (通常是 VisitBlock 裡面的)
    )
    with open('LithosNet.VM/Interpreter.cs', 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ 已注入 VM Block Error Catch！")
else:
    print("ℹ Error Catch 已存在。")
