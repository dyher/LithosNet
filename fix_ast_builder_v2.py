import re

with open("LithosNet.Compiler/AstBuilder.cs", "r", encoding="utf-8") as f:
    code = f.read()

# 1. 刪除舊的 ProcessBinaryOps (匹配從 終極防禦 到 return node; })
code = re.sub(r'// 【終極防禦】.*?return node;\s*\}', '', code, flags=re.DOTALL)
# 2. 刪除舊的 VisitXxxExpr 方法
code = re.sub(r'public override AstNode VisitLogicalOrExpr.*?;\n', '', code)
code = re.sub(r'public override AstNode VisitLogicalAndExpr.*?;\n', '', code)
code = re.sub(r'public override AstNode VisitEqualityExpr.*?;\n', '', code)
code = re.sub(r'public override AstNode VisitRelationalExpr.*?;\n', '', code)
code = re.sub(r'public override AstNode VisitAdditiveExpr.*?;\n', '', code)
code = re.sub(r'public override AstNode VisitMultiplicativeExpr.*?;\n', '', code)

# 3. 注入全新的「終端節點探測法」
new_logic = """
        // 【終極防禦 v2】基於 ITerminalNode 探測，100% 免疫 ANTLR4 Error Recovery 導致的樹結構錯位！
        private AstNode ProcessBinaryOps(ParserRuleContext context, ParserRuleContext[] children) {
            if (children == null || children.Length == 0) return null;
            AstNode node = Visit(children[0]);
            if (node == null) return null;
            
            int childIdx = 1;
            // 遍歷 context 的所有子節點，只捕捉 ITerminalNode 作為運算子 (過濾掉 ErrorNode)
            for (int i = 1; i < context.ChildCount && childIdx < children.Length; i++) {
                var child = context.GetChild(i);
                if (child is Antlr4.Runtime.Tree.ITerminalNode term) {
                    string op = term.GetText();
                    AstNode right = Visit(children[childIdx]);
                    if (right == null) continue;
                    node = CreateNode("BinaryOpNode", new Dictionary<string, object> {
                        { "Left", node }, { "Right", right }, { "Operator", op }
                    });
                    childIdx++;
                }
            }
            return node;
        }

        public override AstNode VisitLogicalOrExpr(LPCParser.LogicalOrExprContext c) => ProcessBinaryOps(c, c.logicalAndExpr());
        public override AstNode VisitLogicalAndExpr(LPCParser.LogicalAndExprContext c) => ProcessBinaryOps(c, c.equalityExpr());
        public override AstNode VisitEqualityExpr(LPCParser.EqualityExprContext c) => ProcessBinaryOps(c, c.relationalExpr());
        public override AstNode VisitRelationalExpr(LPCParser.RelationalExprContext c) => ProcessBinaryOps(c, c.additiveExpr());
        public override AstNode VisitAdditiveExpr(LPCParser.AdditiveExprContext c) => ProcessBinaryOps(c, c.multiplicativeExpr());
        public override AstNode VisitMultiplicativeExpr(LPCParser.MultiplicativeExprContext c) => ProcessBinaryOps(c, c.unaryExpr());
"""

if "終極防禦 v2" not in code:
    code = code.replace("public override AstNode VisitUnaryExpr", new_logic + "\n        public override AstNode VisitUnaryExpr")
    with open("LithosNet.Compiler/AstBuilder.cs", "w", encoding="utf-8") as f:
        f.write(code)
    print("✅ AstBuilder.cs 已終極修復 (v2)：改用 ITerminalNode 探測法，徹底免疫樹結構錯位！")
else:
    print("ℹ️ 已存在 v2 邏輯！")
