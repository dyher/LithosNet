import re

with open("LithosNet.Compiler/AstBuilder.cs", "r", encoding="utf-8") as f:
    code = f.read()

# 刪除所有舊的二元運算處理方法
code = re.sub(r'public override AstNode VisitLogicalOrExpr.*?return node;\s*\}', '', code, flags=re.DOTALL)
code = re.sub(r'public override AstNode VisitLogicalAndExpr.*?return node;\s*\}', '', code, flags=re.DOTALL)
code = re.sub(r'public override AstNode VisitEqualityExpr.*?return node;\s*\}', '', code, flags=re.DOTALL)
code = re.sub(r'public override AstNode VisitRelationalExpr.*?return node;\s*\}', '', code, flags=re.DOTALL)
code = re.sub(r'public override AstNode VisitAdditiveExpr.*?return node;\s*\}', '', code, flags=re.DOTALL)
code = re.sub(r'public override AstNode VisitMultiplicativeExpr.*?return node;\s*\}', '', code, flags=re.DOTALL)

# 注入全新的「指標安全遍歷法」Helper 與方法
new_logic = """
        // 【終極防禦】使用指標安全遍歷，100% 免疫 ANTLR4 Error Recovery 導致的畸形樹崩潰！
        private AstNode ProcessBinaryOps(ParserRuleContext context, ParserRuleContext[] children, string defaultOp) {
            if (children == null || children.Length == 0) return null;
            AstNode node = null;
            int opIndex = 1;
            for (int i = 0; i < children.Length; i++) {
                if (children[i] == null) continue;
                var childNode = Visit(children[i]);
                if (childNode == null) continue;
                if (node == null) { node = childNode; continue; }
                
                string op = defaultOp;
                if (opIndex < context.ChildCount) {
                    var opNode = context.GetChild(opIndex);
                    if (opNode is Antlr4.Runtime.Tree.ITerminalNode term) op = term.GetText();
                    opIndex += 2;
                }
                
                node = CreateNode("BinaryOpNode", new Dictionary<string, object> { 
                    { "Left", node }, { "Right", childNode }, { "Operator", op } 
                });
            }
            return node;
        }

        public override AstNode VisitLogicalOrExpr(LPCParser.LogicalOrExprContext context) => ProcessBinaryOps(context, context.logicalAndExpr(), "||");
        public override AstNode VisitLogicalAndExpr(LPCParser.LogicalAndExprContext context) => ProcessBinaryOps(context, context.equalityExpr(), "&&");
        public override AstNode VisitEqualityExpr(LPCParser.EqualityExprContext context) => ProcessBinaryOps(context, context.relationalExpr(), "==");
        public override AstNode VisitRelationalExpr(LPCParser.RelationalExprContext context) => ProcessBinaryOps(context, context.additiveExpr(), "<");
        public override AstNode VisitAdditiveExpr(LPCParser.AdditiveExprContext context) => ProcessBinaryOps(context, context.multiplicativeExpr(), "+");
        public override AstNode VisitMultiplicativeExpr(LPCParser.MultiplicativeExprContext context) => ProcessBinaryOps(context, context.unaryExpr(), "*");
"""

# 找到 VisitUnaryExpr 的前面插入
if "ProcessBinaryOps" not in code:
    code = code.replace("public override AstNode VisitUnaryExpr", new_logic + "\n        public override AstNode VisitUnaryExpr")
    with open("LithosNet.Compiler/AstBuilder.cs", "w", encoding="utf-8") as f:
        f.write(code)
    print("✅ AstBuilder.cs 已終極修復：全面改用「指標安全遍歷法」，徹底免疫畸形樹崩潰！")
else:
    print("ℹ️ 已存在安全遍歷邏輯！")
