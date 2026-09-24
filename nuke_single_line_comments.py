import glob, re

count = 0
# 遍歷所有 .c 和 .h 檔案
for f in glob.glob('mudlib/**/*.c', recursive=True) + glob.glob('mudlib/**/*.h', recursive=True):
    with open(f, 'r', encoding='utf-8') as file:
        c = file.read()
    
    # 精準匹配並刪除 // 開頭的單行註解 (保留換行符以免破壞行號)
    new_c = re.sub(r'//.*', '', c)
    
    if new_c != c:
        count += 1
        with open(f, 'w', encoding='utf-8') as file:
            file.write(new_c)

print(f'✅ 成功清理了 {count} 個檔案的 // 單行註解！徹底免疫 Lexer 陷阱！')
