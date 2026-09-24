import re

with open("LithosNet.Compiler/AstBuilder.cs", "r", encoding="utf-8") as f:
    code = f.read()

# 1. 在 VisitTopLevelDecl 注入日誌
if "Visiting TopLevelDecl" not in code:
    code = re.sub(
        r'(public override AstNode VisitTopLevelDecl\(LPCParser\.TopLevelDeclContext context\) \{)',
        r'\1\n            Console.WriteLine($"🔍 [AstBuilder] Visiting TopLevelDecl: {context.GetText().Substring(0, Math.Min(50, context.GetText().Length))}...");',
        code
    )

# 2. 在 VisitFuncDecl 注入日誌
if "Visiting FuncDecl" not in code:
    code = re.sub(
        r'(public override AstNode VisitFuncDecl\(LPCParser\.FuncDeclContext context\) \{)',
        r'\1\n            Console.WriteLine($"🔍 [AstBuilder] Visiting FuncDecl: {context.ID().GetText()}");',
        code
    )

# 3. 替換 CreateNode 為帶有完美 try/catch 與屬性賦值日誌的版本
new_create_node = """private AstNode CreateNode(string typeName, Dictionary<string, object> props) {
            try {
                var asm = typeof(AstNode).Assembly;
                var type = asm.GetTypes().FirstOrDefault(t => t.Name == typeName);
                if (type == null) {
                    Console.WriteLine($"❌ [AstBuilder] Type '{typeName}' NOT FOUND in LithosNet.Core!");
                    return null;
                }
                
                var node = Activator.CreateInstance(type);
                Console.WriteLine($"✅ [AstBuilder] Successfully instantiated '{typeName}'");
                
                foreach(var kvp in props) {
                    if (kvp.Value == null) continue;
                    var prop = type.GetProperty(kvp.Key);
                    if (prop == null) {
                        prop = type.GetProperties().FirstOrDefault(p => 
                            p.Name.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                            p.Name.Contains(kvp.Key) || 
                            kvp.Key.Contains(p.Name) ||
                            (kvp.Key == "Body" && (p.Name.Contains("Block") || p.Name.Contains("Statements")))
                        );
                    }
                    if (prop != null) {
                        try { prop.SetValue(node, kvp.Value); } 
                        catch (Exception ex) { 
                            Console.WriteLine($"⚠️ [AstBuilder] Failed to set property '{kvp.Key}' on '{typeName}': {ex.Message}"); 
                        }
                    } else {
                        Console.WriteLine($"⚠️ [AstBuilder] Property '{kvp.Key}' NOT FOUND on '{typeName}' even with fuzzy match!");
                    }
                }
                return (AstNode)node;
            } catch (Exception ex) {
                Console.WriteLine($"💥 [AstBuilder] CRITICAL Exception creating '{typeName}': {ex.Message}");
                return null;
            }
        }"""

code = re.sub(r'private AstNode CreateNode\(string typeName.*?return \(AstNode\)node;\s*\}', new_create_node, code, flags=re.DOTALL)

with open("LithosNet.Compiler/AstBuilder.cs", "w", encoding="utf-8") as f:
    f.write(code)
print("✅ AstBuilder.cs 已注入 X 光級別雷達日誌！所有底層行為將無所遁形！")
