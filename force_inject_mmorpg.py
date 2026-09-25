import re

with open('mudlib/master.c', 'r', encoding='utf-8') as f:
    code = f.read()

print("=== 🔍 master.c 前 25 行真實內容 ===")
print('\n'.join(code.split('\n')[:25]))
print("=====================================\n")

if "r = random(50);" not in code:
    # 1. 確保變數宣告存在 (防重複宣告)
    if "int r;" not in code:
        code = re.sub(
            r'(void\s+preload\s*\(\s*\)\s*\{)', 
            r'\1\n    int r;\n    int dmg;\n    string filtered;\n    int now;\n', 
            code, count=1
        )
        print("✅ 已注入 C89 變數宣告！")
    
    # 2. 注入測試邏輯 (在第一個 debug_message 之後)
    test_logic = '''    r = random(50);
    dmg = 100 + r;
    debug_message("Combat Dmg: " + dmg + "\\n");
    filtered = replace_string("bad word", "bad", "***");
    debug_message("Filter: " + filtered + "\\n");
    now = time();
    debug_message("Time: " + now + "\\n");
'''
    # 精準替換第一個 debug_message("...");
    code = re.sub(
        r'(debug_message\([^;]+;)', 
        r'\1\n' + test_logic, 
        code, count=1
    )
    
    with open('mudlib/master.c', 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ 已強制注入 MMORPG 測試邏輯！")
else:
    print("ℹ️ 測試邏輯已存在。")
