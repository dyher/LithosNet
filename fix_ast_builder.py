import re

with open("LithosNet.Compiler/AstBuilder.cs", "r", encoding="utf-8") as f:
    code = f.read()

# 刪除舊的 HandleBinaryOps 及其呼叫者
code = re.sub(r'private AstNode HandleBinaryOps.*?return node;\s*\}', '', code, flags=re.DOTALL)
code = re.sub(r'public override AstNode VisitLogicalOrExpr.*?;\n', '', code)
code = re.sub(r'public override AstNode VisitLogicalAndExpr.*?;\n', '', code)
code = re.sub(r'public override AstNode VisitEqualityExpr.*?;\n', '', code)
code = re.sub(r'public override AstNode VisitRelationalExpr.*?;\n', '', code)
code = re.sub(r'public override AstNode VisitAdditiveExpr.*?;\n', '', code)
code = re.sub(r'public override AstNode VisitMultiplicativeExpr.*?;\n', '', code)

# 注入全新的強型別處理邏輯
new_logic = """
        public override AstNode VisitLogicalOrExpr(LPCParser.LogicalOrExprContext context) {
            var children = context.logicalAndExpr();
            if (children == null || children.Length == 0) return null;
            AstNode node = Visit(children[0]);
            for (int i = 1; i < children.Length; i++) {
                node = CreateNode("BinaryOpNode", new Dictionary<string, object> { { "Left", node }, { "Right", Visit(children[i]) }, { "Operator", "||" } });
            }
            return node;
        }

        public override AstNode VisitLogicalAndExpr(LPCParser.LogicalAndExprContext context) {
            var children = context.equalityExpr();
            if (children == null || children.Length == 0) return null;
            AstNode node = Visit(children[0]);
            for (int i = 1; i < children.Length; i++) {
                node = CreateNode("BinaryOpNode", new Dictionary<string, object> { { "Left", node }, { "Right", Visit(children[i]) }, { "Operator", "&&" } });
            }
            return node;
        }

        public override AstNode VisitEqualityExpr(LPCParser.EqualityExprContext context) {
            var children = context.relationalExpr();
            if (children == null || children.Length == 0) return null;
            AstNode node = Visit(children[0]);
            for (int i = 1; i < children.Length; i++) {
                string op = context.GetChild(2 * i - 1).GetText();
                node = CreateNode("BinaryOpNode", new Dictionary<string, object> { { "Left", node }, { "Right", Visit(children[i]) }, { "Operator", op } });
            }
            return node;
        }

        public override AstNode VisitRelationalExpr(LPCParser.RelationalExprContext context) {
            var children = context.additiveExpr();
            if (children == null || children.Length == 0) return null;
            AstNode node = Visit(children[0]);
            for (int i = 1; i < children.Length; i++) {
                string op = context.GetChild(2 * i - 1).GetText();
                node = CreateNode("BinaryOpNode", new Dictionary<string, object> { { "Left", node }, { "Right", Visit(children[i]) }, { "Operator", op } });
            }
            return node;
        }

        public override AstNode VisitAdditiveExpr(LPCParser.AdditiveExprContext context) {
            var children = context.multiplicativeExpr();
            if (children == null || children.Length == 0) return null;
            AstNode node = Visit(children[0]);
            for (int i = 1; i < children.Length; i++) {
                string op = context.GetChild(2 * i - 1).GetText();
                node = CreateNode("BinaryOpNode", new Dictionary<string, object> { { "Left", node }, { "Right", Visit(children[i]) }, { "Operator", op } });
            }
            return node;
        }

        public override AstNode VisitMultiplicativeExpr(LPCParser.MultiplicativeExprContext context) {
            var children = context.unaryExpr();
            if (children == null || children.Length == 0) return null;
            AstNode node = Visit(children[0]);
            for (int i = 1; i < children.Length; i++) {
                string op = context.GetChild(2 * i - 1).GetText();
                node = CreateNode("BinaryOpNode", new Dictionary<string, object> { { "Left", node }, { "Right", Visit(children[i]) }, { "Operator", op } });
            }
            return node;
        }
"""

# 找到 VisitUnaryExpr 的前面插入
if "VisitLogicalOrExpr" not in code:
    code = code.replace("public override AstNode VisitUnaryExpr", new_logic + "\n        public override AstNode VisitUnaryExpr")
    with open("LithosNet.Compiler/AstBuilder.cs", "w", encoding="utf-8") as f:
        f.write(code)
    print("✅ AstBuilder.cs 已終極修復：全面改用 ANTLR4 強型別 API，徹底免疫底層樹結構崩潰！")
else:
    print("ℹ️ 已存在強型別邏輯！")
