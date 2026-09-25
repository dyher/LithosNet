import re

TARGET = 'mudlib/obj/master.c'

with open(TARGET, 'r', encoding='utf-8') as f:
    code = f.read()

print("=== 修改前 ===")
print(code[:500])
print("...")

if "r = random(50);" not in code:
    # 找到 preload 函數的開頭大括號
    match = re.search(r'(void\s+preload\s*\(\s*\)\s*\{)', code)
    if match:
        insert_pos = match.end()
        
        # C89 變數宣告 (置頂)
        decls = "\n    int r;\n    int dmg;\n    string filtered;\n    int now;\n"
        
        # 測試邏輯 (放在第一個 debug_message 之後)
        test_logic = """
    r = random(50);
    dmg = 100 + r;
    debug_message("Combat Dmg: " + dmg + "\\n");
    filtered = replace_string("bad word", "bad", "***");
    debug_message("Filter: " + filtered + "\\n");
    now = time();
    debug_message("Time: " + now + "\\n");
"""
        # 先插入變數宣告
        code = code[:insert_pos] + decls + code[insert_pos:]
        
        # 在第一個 debug_message 之後插入測試邏輯
        code = re.sub(
            r'(debug_message\("Master preloading[^"]*"\s*\)\s*;)',
            r'\1\n' + test_logic,
            code, count=1
        )
        
        with open(TARGET, 'w', encoding='utf-8') as f:
            f.write(code)
        print("\n=== 修改後 ===")
        print(code[:800])
        print("\n✅ 已成功注入到真正的 master.c！")
    else:
        print("⚠ 找不到 preload 函數！")
else:
    print("ℹ 測試邏輯已存在。")
