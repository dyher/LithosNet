import os, re, glob

for f in glob.glob('mudlib/**/*.c', recursive=True):
    with open(f, 'r', encoding='utf-8') as file:
        c = file.read()
    
    # 移除 int _dX = ...; 的妥協
    c = re.sub(r'int\s+_d\d+\s*=\s*', '', c)
    
    # 恢復一些標準的 // 註解 (可選)
    if "master.c" in f and "// Master preloading..." not in c:
        c = c.replace('debug_message("Master preloading...");', '// Master preloading...\n    debug_message("Master preloading...");')
        
    with open(f, 'w', encoding='utf-8') as file:
        file.write(c)
print("✅ Mudlib 已恢復純淨語法！徹底告別妥協！")
