with open("LithosNet.Compiler/AstBuilder.cs", "r", encoding="utf-8") as f:
    code = f.read()

top_level_decl = """
        // 【終極路由】將 TopLevelDecl 精準分發給 Inherit / Var / Func
        public override AstNode VisitTopLevelDecl(LPCParser.TopLevelDeclContext context) {
            if (context.inheritDecl() != null) return Visit(context.inheritDecl());
            if (context.varDecl() != null) return Visit(context.varDecl());
            if (context.funcDecl() != null) return Visit(context.funcDecl());
            return null;
        }
"""

if "VisitTopLevelDecl" not in code:
    code = code.replace("public override AstNode VisitInheritDecl", top_level_decl + "\n        public override AstNode VisitInheritDecl")
    with open("LithosNet.Compiler/AstBuilder.cs", "w", encoding="utf-8") as f:
        f.write(code)
    print("✅ AstBuilder.cs 已注入 VisitTopLevelDecl！所有函數宣告瞬間復活！")
else:
    print("ℹ️ VisitTopLevelDecl 已存在！")
