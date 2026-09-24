import glob, re, os

count = 0
# 掃描 VM 和 Core 中的所有 .cs 檔案
targets = glob.glob('LithosNet.VM/**/*.cs', recursive=True) + glob.glob('LithosNet.Core/**/*.cs', recursive=True)

for f in targets:
    if 'obj/' in f or 'bin/' in f: continue
    
    with open(f, 'r', encoding='utf-8') as file:
        c = file.read()
        
    original_c = c
    
    # 1. 將 (string)xxx 替換為 Convert.ToString(xxx)
    # 匹配變數名、屬性存取、陣列索引 (例如 args[0].Value)
    c = re.sub(r'\(string\)\s*([a-zA-Z_][a-zA-Z0-9_\.]*(?:\[[^\]]+\])?)', r'Convert.ToString(\1)', c)
    
    # 2. 將 (int)xxx 替換為 Convert.ToInt32(xxx)
    c = re.sub(r'\(int\)\s*([a-zA-Z_][a-zA-Z0-9_\.]*(?:\[[^\]]+\])?)', r'Convert.ToInt32(\1)', c)
    
    # 3. 確保檔案頂部有 using System; (Convert 需要它)
    if c != original_c and "using System;" not in c:
        c = "using System;\n" + c
        
    if c != original_c:
        with open(f, 'w', encoding='utf-8') as file:
            file.write(c)
        count += 1
        print(f"✅ 已淨化: {f}")

print(f"\n🎉 總共淨化了 {count} 個檔案！VM 內部的 Unboxing 炸彈已徹底拆除！")
