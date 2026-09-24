import re

with open("LithosNet.Host/Program.cs", "r", encoding="utf-8") as f:
    code = f.read()

# 精準替換 catch 區塊中的 ex.Message 為 ex.ToString() (包含完整堆疊追蹤)
code = re.sub(
    r'Console\.WriteLine\(\$"❌ master->connect\(\) 失敗: \{ex\.Message\}"\);',
    r'Console.WriteLine($"❌ master->connect() 失敗:\n{ex.ToString()}");',
    code
)

# 備用寬泛匹配 (防止格式微調)
code = re.sub(
    r'Console\.WriteLine\([^)]*master->connect\(\)[^)]*ex\.Message[^)]*\);',
    r'Console.WriteLine($"❌ master->connect() 失敗:\n{ex.ToString()}");',
    code
)

with open("LithosNet.Host/Program.cs", "w", encoding="utf-8") as f:
    f.write(code)
print("✅ Program.cs 已修改為輸出完整的 StackTrace！真相即將大白！")
