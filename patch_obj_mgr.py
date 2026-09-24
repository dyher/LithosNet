import re

with open("LithosNet.VM/ObjectManager.cs", "r", encoding="utf-8") as f:
    code = f.read()

# 替換舊的 Lexer + Parser 呼叫
old_compile = r'var tokens = new Lexer\(src\)\.Tokenize\(\);\s*var ast = new Parser\(tokens\)\.Parse\(\);'
new_compile = '''var inputStream = new Antlr4.Runtime.AntlrInputStream(src);
            var lexer = new LithosNet.Compiler.Ast.LPCLexer(inputStream);
            var tokenStream = new Antlr4.Runtime.CommonTokenStream(lexer);
            var parser = new LithosNet.Compiler.Ast.LPCParser(tokenStream);
            var tree = parser.program();
            var ast = new LithosNet.Compiler.AstBuilder().Visit(tree);'''

code = re.sub(old_compile, new_compile, code)

with open("LithosNet.VM/ObjectManager.cs", "w", encoding="utf-8") as f:
    f.write(code)
print("✅ ObjectManager.cs 已成功接入 ANTLR4 工業級引擎！")
