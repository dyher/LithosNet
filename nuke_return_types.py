import re, glob

count = 0
for f in glob.glob('mudlib/**/*.c', recursive=True):
    with open(f, 'r', encoding='utf-8') as file:
        c = file.read()
    
    # 精準匹配行首的型別宣告，並移除它 (保留函數名和括號)
    # 例如： int setup_user( -> setup_user(
    def remove_type(match):
        global count
        count += 1
        return match.group(1) + "("
        
    c = re.sub(r'(?m)^\s*(?:int|string|mixed|void)\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*\(', remove_type, c)
    
    with open(f, 'w', encoding='utf-8') as file:
        file.write(c)

print(f'✅ 成功移除了 {count} 個函數的返回型別！徹底避開 Parser 變數宣告陷阱！')
