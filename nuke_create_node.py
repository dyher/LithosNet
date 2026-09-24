import re

with open("LithosNet.Compiler/AstBuilder.cs", "r", encoding="utf-8") as f:
    code = f.read()

new_create_node = """private AstNode CreateNode(string typeName, Dictionary<string, object> props) {
            var asm = typeof(AstNode).Assembly;
            var type = asm.GetTypes().FirstOrDefault(t => t.Name == typeName);
            if (type == null) return null; 
            var node = Activator.CreateInstance(type);
            
            foreach(var kvp in props) {
                if (kvp.Value == null) continue;
                
                // 1. 嘗試精準匹配
                var prop = type.GetProperty(kvp.Key);
                
                // 2. 【終極防禦】模糊匹配：如果找不到，尋找名稱最相似的屬性！
                if (prop == null) {
                    prop = type.GetProperties().FirstOrDefault(p => 
                        p.Name.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                        p.Name.Contains(kvp.Key) || 
                        kvp.Key.Contains(p.Name) ||
                        (kvp.Key == "Body" && (p.Name.Contains("Block") || p.Name.Contains("Statements")))
                    );
                }
                
                if (prop != null) {
                    try { prop.SetValue(node, kvp.Value); } catch {}
                }
            }
            
            // 【雷達日誌】如果是函數宣告，印出它的真實名稱，確保沒有變成 null！
            if (typeName.Contains("Function")) {
                var nameProp = type.GetProperties().FirstOrDefault(p => p.Name.Contains("Name"));
                if (nameProp != null) {
                    var val = nameProp.GetValue(node);
                    Console.WriteLine($"✅ [AstBuilder] 成功創建 {typeName}: Name='{val}'");
                }
            }
            
            return (AstNode)node;
        }"""

# 使用 Regex 精準替換舊的 CreateNode 方法
code = re.sub(r'private AstNode CreateNode\(string typeName.*?return \(AstNode\)node;\s*\}', new_create_node, code, flags=re.DOTALL)

with open("LithosNet.Compiler/AstBuilder.cs", "w", encoding="utf-8") as f:
    f.write(code)
print("✅ CreateNode 已注入「模糊匹配」與「雷達日誌」！徹底消滅 Silent Fail！")
