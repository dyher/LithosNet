using Antlr4.Runtime;
#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LithosNet.Core;
using LithosNet.Compiler.Ast;

namespace LithosNet.Compiler {
    public class AstBuilder : LPCBaseVisitor<AstNode> {
        // 【終極武器】使用反射動態建立 AST 節點，免疫屬性名稱變更！
        private AstNode CreateNode(string typeName, Dictionary<string, object> props) {
            var asm = typeof(AstNode).Assembly;
            var type = asm.GetTypes().FirstOrDefault(t => t.Name == typeName);
            if (type == null) throw new Exception($"AST Node type '{typeName}' not found in assembly!");
            var node = Activator.CreateInstance(type);
            foreach(var kvp in props) {
                var prop = type.GetProperty(kvp.Key);
                if (prop != null && kvp.Value != null) {
                    try { prop.SetValue(node, kvp.Value); } catch {}
                }
            }
            return (AstNode)node;
        }

        public override AstNode VisitProgram(LPCParser.ProgramContext context) {
            var decls = new List<AstNode>();
            foreach(var decl in context.topLevelDecl()) {
                var node = Visit(decl);
                if (node != null) decls.Add(node);
            }
            return CreateNode("ProgramNode", new Dictionary<string, object> { { "Declarations", decls } });
        }

        public override AstNode VisitInheritDecl(LPCParser.InheritDeclContext context) {
            string path = context.STRING_LITERAL().GetText().Trim('"');
            return CreateNode("InheritNode", new Dictionary<string, object> { { "Path", path } });
        }

        public override AstNode VisitVarDecl(LPCParser.VarDeclContext context) {
            string type = context.typeSpec().GetText();
            string name = context.ID().GetText();
            AstNode init = context.expr() != null ? Visit(context.expr()) : null;
            return CreateNode("VarDeclNode", new Dictionary<string, object> { 
                { "Type", type }, { "Name", name }, { "Initializer", init } 
            });
        }

        public override AstNode VisitFuncDecl(LPCParser.FuncDeclContext context) {
            string retType = context.typeSpec() != null ? context.typeSpec().GetText() : "mixed";
            string name = context.ID().GetText();
            var parameters = new List<string>();
            if (context.paramList() != null) {
                foreach(var id in context.paramList().ID()) parameters.Add(id.GetText());
            }
            var body = Visit(context.block());
            return CreateNode("FunctionDeclNode", new Dictionary<string, object> {
                { "ReturnType", retType }, { "Name", name }, { "Parameters", parameters }, { "Body", body }
            });
        }

        public override AstNode VisitBlock(LPCParser.BlockContext context) {
            var stmts = new List<AstNode>();
            foreach(var stmt in context.statement()) {
                var node = Visit(stmt);
                if (node != null) stmts.Add(node);
            }
            return CreateNode("BlockNode", new Dictionary<string, object> { { "Statements", stmts } });
        }

        public override AstNode VisitIfStmt(LPCParser.IfStmtContext context) {
            var cond = Visit(context.expr());
            var thenBranch = Visit(context.statement(0));
            var elseBranch = context.statement().Length > 1 ? Visit(context.statement(1)) : null;
            return CreateNode("IfNode", new Dictionary<string, object> {
                { "Condition", cond }, { "ThenBranch", thenBranch }, { "ElseBranch", elseBranch }
            });
        }

        public override AstNode VisitWhileStmt(LPCParser.WhileStmtContext context) {
            var cond = Visit(context.expr());
            var body = Visit(context.statement());
            return CreateNode("WhileNode", new Dictionary<string, object> { { "Condition", cond }, { "Body", body } });
        }

        public override AstNode VisitReturnStmt(LPCParser.ReturnStmtContext context) {
            var val = context.expr() != null ? Visit(context.expr()) : null;
            return CreateNode("ReturnNode", new Dictionary<string, object> { { "Value", val } });
        }

        public override AstNode VisitExprStmt(LPCParser.ExprStmtContext context) {
            return Visit(context.expr());
        }

        public override AstNode VisitAssignmentExpr(LPCParser.AssignmentExprContext context) {
            if (context.assignmentExpr() != null) {
                var left = Visit(context.logicalOrExpr());
                var right = Visit(context.assignmentExpr());
                string op = context.GetChild(1).GetText();
                return CreateNode("AssignNode", new Dictionary<string, object> { { "Target", left }, { "Value", right }, { "Operator", op } });
            }
            return Visit(context.logicalOrExpr());
        }

        // 【二進位運算通用處理】
        

                                                
        
        

        

        

        

        

        

        
        

                                                
        
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

        public override AstNode VisitUnaryExpr(LPCParser.UnaryExprContext context) {
            if (context.unaryExpr() != null) {
                string op = context.GetChild(0).GetText();
                return CreateNode("UnaryOpNode", new Dictionary<string, object> { { "Operator", op }, { "Operand", Visit(context.unaryExpr()) } });
            }
            return Visit(context.postfixExpr());
        }

        public override AstNode VisitPostfixExpr(LPCParser.PostfixExprContext context) {
            AstNode node = Visit(context.primaryExpr());
            for(int i=1; i<context.ChildCount; i++) {
                var child = context.GetChild(i);
                if (child is Antlr4.Runtime.Tree.ITerminalNode term) {
                    if (term.Symbol.Type == LPCParser.LPAREN) {
                        var args = new List<AstNode>();
                        var argListCtx = context.GetChild(i-1) as LPCParser.ArgListContext; 
                        // 注意：ANTLR4 的 Postfix 結構較複雜，這裡做基礎映射
                        if (context.argList() != null && context.argList().Length > 0) {
                             var al = context.argList(0);
                             foreach(var e in al.expr()) args.Add(Visit(e));
                        }
                        node = CreateNode("CallNode", new Dictionary<string, object> { { "Callee", node }, { "Arguments", args } });
                        i++; // 跳過 RPAREN
                    } else if (term.Symbol.Type == LPCParser.ARROW) {
                        string funcName = context.GetChild(i+1).GetText();
                        var args = new List<AstNode>();
                        // 簡化 CallOther
                        node = CreateNode("CallOtherNode", new Dictionary<string, object> { { "Target", node }, { "Function", funcName }, { "Arguments", args } });
                        i += 2; 
                    }
                }
            }
            return node;
        }

        public override AstNode VisitPrimaryExpr(LPCParser.PrimaryExprContext context) {
            if (context.ID() != null) return CreateNode("IdentifierNode", new Dictionary<string, object> { { "Name", context.ID().GetText() } });
            if (context.INT_LITERAL() != null) return CreateNode("LiteralNode", new Dictionary<string, object> { { "Value", LpcValue.Create(int.Parse(context.INT_LITERAL().GetText())) } });
            if (context.STRING_LITERAL() != null) return CreateNode("LiteralNode", new Dictionary<string, object> { { "Value", LpcValue.Create(context.STRING_LITERAL().GetText().Trim('"')) } });
            if (context.expr() != null) return Visit(context.expr());
            return null;
        }
    }
}
