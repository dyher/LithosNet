import glob, re

count = 0
for f in glob.glob('LithosNet.Core/**/*.cs', recursive=True):
    with open(f, 'r', encoding='utf-8') as file:
        c = file.read()
    
    # 精準鎖定 LpcValue 相關的檔案
    if "LpcValue" in c and ("AsString" in c or "AsInt" in c or "Value" in c):
        print(f"🔍 找到目標檔案: {f}")
        
        # 1. 將 (string)Value 替換為絕對安全的 Convert.ToString(Value) ?? ""
        c = re.sub(r'\(string\)\s*Value', 'Convert.ToString(Value) ?? ""', c)
        
        # 2. 將 (int)Value 替換為絕對安全的 Convert.ToInt32(Value)
        c = re.sub(r'\(int\)\s*Value', 'Convert.ToInt32(Value)', c)
        
        # 3. 將 (bool)Value 替換為絕對安全的 Convert.ToBoolean(Value)
        c = re.sub(r'\(bool\)\s*Value', 'Convert.ToBoolean(Value)', c)
        
        # 確保有 using System;
        if "using System;" not in c:
            c = "using System;\n" + c
            
        with open(f, 'w', encoding='utf-8') as file:
            file.write(c)
        count += 1
        print(f"✅ 已將 {f} 中的所有危險強制轉換替換為安全的 Convert.ToXXX()！")

if count == 0:
    print("⚠️ 找不到需要修改的 LpcValue 檔案！")
