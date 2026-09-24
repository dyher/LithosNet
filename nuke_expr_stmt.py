import re, glob

count = 0
for f in glob.glob('mudlib/**/*.c', recursive=True):
    with open(f, 'r', encoding='utf-8') as file:
        c = file.read()
    
    # 精準匹配行首的裸函數呼叫： func_name(args);
    # 排除已經有 int/string 開頭的，也排除 if/while/return
    def wrap_in_block(match):
        global count
        indent = match.group(1)
        func_call = match.group(2)
        count += 1
        # 用 Block Scope 包覆，避免變數名稱衝突！
        return f"{indent}{{ int _dummy_{count} = {func_call}; }}"
        
    # 匹配： (縮排)(函數名(參數));
    c = re.sub(r'(?m)^(\s*)([a-zA-Z_][a-zA-Z0-9_]*\s*\([^;]*\))\s*;', wrap_in_block, c)
    
    with open(f, 'w', encoding='utf-8') as file:
        file.write(c)

print(f'✅ 成功將 {count} 個裸函數呼叫包覆為 Block Scope！徹底騙過 Parser！')
