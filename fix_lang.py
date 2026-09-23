import re

# 1. Parser.cs: 注入 Unary Minus (負數) 支援
with open("LithosNet.Compiler/Parser.cs", "r", encoding="utf-8") as f:
    p = f.read()
if "Check(TokenType.Minus))" not in p:
    um = """            if (Check(TokenType.Minus)) {
                Consume();
                var expr = ParsePrimary();
                return new BinaryOpNode { Left = new LiteralNode { Value = LpcValue.Create(0) }, Op = "-", Right = expr };
            }
"""
    p = p.replace("if (Check(TokenType.LeftParen)) { Consume(); var expr = ParseExpression();", um + "            if (Check(TokenType.LeftParen)) { Consume(); var expr = ParseExpression();")
    with open("LithosNet.Compiler/Parser.cs", "w", encoding="utf-8") as f: f.write(p)
    print("✅ Parser.cs: Unary Minus (-1) supported!")

# 2. BuiltInEfuns.cs: 注入 to_int() Efun
with open("LithosNet.VM/BuiltInEfuns.cs", "r", encoding="utf-8") as f:
    e = f.read()
if "[Efun(\"to_int\")]" not in e:
    ti = """
        // 【Mudlib 基礎】to_int 字串轉整數
        [Efun("to_int")]
        public static LpcValue ToInt(LpcValue[] args) {
            if (args.Length > 0 && args[0].Type == LpcType.String) {
                if (int.TryParse(args[0].AsString(), out int res)) return LpcValue.Create(res);
            } else if (args.Length > 0 && args[0].Type == LpcType.Int) return args[0];
            return LpcValue.Create(0);
        }
"""
    e = e.replace("public static class BuiltInEfuns {", "public static class BuiltInEfuns {\n" + ti)
    with open("LithosNet.VM/BuiltInEfuns.cs", "w", encoding="utf-8") as f: f.write(e)
    print("✅ BuiltInEfuns.cs: to_int() added!")
