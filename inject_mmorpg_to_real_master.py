import re

with open('mudlib/master.c', 'r', encoding='utf-8') as f:
    code = f.read()

# 檢查是否已經注入
if 'Combat Dmg' not in code:
    # 1. 在 void preload() { 之後插入變數宣告 (嚴格遵守 C89 置頂鐵律)
    code = re.sub(
        r'(void\s+preload\s*\(\s*\)\s*\{)',
        r'\1\n    int r;\n    int dmg;\n    string filtered;\n    int now;\n',
        code, count=1
    )
    
    # 2. 在第一個 debug_message("Master preloading... 之後插入測試邏輯
    test_logic = '''
        r = random(50);
        dmg = 100 + r;
        debug_message("Combat Dmg: " + dmg + "\\n");

        filtered = replace_string("bad word", "bad", "***");
        debug_message("Filter: " + filtered + "\\n");

        now = time();
        debug_message("Time: " + now + "\\n");
    '''
    
    # 尋找 Master preloading 並注入
    if 'debug_message("Master preloading' in code:
        code = re.sub(
            r'(debug_message\("Master preloading[^;]+;)',
            r'\1\n' + test_logic,
            code, count=1
        )
    else:
        # 備用：直接在變數宣告後注入
        code = code.replace('int now;\n', 'int now;\n' + test_logic)
        
    with open('mudlib/master.c', 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ 已成功將 MMORPG 測試邏輯以 C89 標準注入真實 master.c！")
else:
    print("ℹ️ 測試邏輯已存在。")
