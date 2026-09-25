#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LithosNet.Core;
using LithosNet.Compiler.Ast;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace LithosNet.Compiler {
    public class AstBuilder : LPCBaseVisitor<AstNode> {
        // 【終極武器】支援 Property、Field、NonPublic、甚至 List 的 Add 方法！
        private AstNode CreateNode(string typeName, Dictionary<string, object> props) {
            var asm = typeof(AstNode).Assembly;
            var type = asm.GetTypes().FirstOrDefault(t => t.Name == typeName);
            if (type == null) return null; 
            var node = Activator.CreateInstance(type);
            
            foreach(var kvp in props) {
                if (kvp.Value == null) continue;
                
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                
                // 1. 嘗試 Property
                var prop = type.GetProperty(kvp.Key, flags);
                // 2. 嘗試 Field
                var field = type.GetField(kvp.Key, flags);
                
                if (prop != null) {
                    try { prop.SetValue(node, kvp.Value); } catch {}
                } else if (field != null) {
                    try { field.SetValue(node, kvp.Value); } catch {}
                } else {
                    // 3. 如果都找不到，嘗試尋找 Add 方法 (針對 List/Collection)
                    var addMethod = type.GetMethods(flags).FirstOrDefault(m => m.Name == "Add" || m.Name == "Add" + kvp.Key);
                    if (addMethod != null && kvp.Value is System.Collections.IEnumerable list) {
                        foreach(var item in list) {
                            try { addMethod.Invoke(node, new[] { item }); } catch {}
                        }
                    }
                }
            }
            return (AstNode)node;
        }

        public override AstNode VisitTopLevelDecl(LPCParser.TopLevelDeclContext context) {
            if (context.inheritDecl() != null) return Visit(context.inheritDecl());
            if (context.varDecl() != null) return Visit(context.varDecl());
            if (context.funcDecl() != null) return Visit(context.funcDecl());
            return null;
        }

        public override AstNode VisitInheritDecl(LPCParser.InheritDeclContext context) {
            string path = context.STRING_LITERAL().GetText().Trim('"');
            return CreateNode("InheritNode", new Dictionary<string, object> { { "ParentObjName", path } });
        }

        public override AstNode VisitVarDecl(LPCParser.VarDeclContext context) {
            string type = context.typeSpec().GetText();
            string name = context.ID().GetText();
            AstNode init = context.expr() != null ? Visit(context.expr()) : null;
            return CreateNode("VariableDeclarationNode", new Dictionary<string, object> { 
                { "TypeName", type }, { "VariableName", name }, { "Initializer", init } 
            });
        }

        public override AstNode VisitFuncDecl(LPCParser.FuncDeclContext context) {
            string retType = context.typeSpec() != null ? context.typeSpec().GetText() : "mixed";
            string name = context.ID().GetText();
            var parameters = new System.Collections.Generic.List<ParameterNode>();
            if (context.paramList() != null && context.paramList().ID() != null) {
                foreach(var id in context.paramList().ID()) {
                    var p = new ParameterNode();
                    p.Name = id.GetText();
                    p.TypeName = "mixed";
                    parameters.Add(p);
                }
            }
            var blockNode = Visit(context.block());
                var body = new System.Collections.Generic.List<AstNode>();
                if (blockNode != null) {
                    // 【終極拆解】如果拿到的是 BlockNode，自動提取它的 Statements 列表！
                    var stmtsProp = blockNode.GetType().GetProperty("Statements");
                    var stmtsField = blockNode.GetType().GetField("Statements");
                    if (stmtsProp != null) body = (System.Collections.Generic.List<AstNode>)stmtsProp.GetValue(blockNode);
                    else if (stmtsField != null) body = (System.Collections.Generic.List<AstNode>)stmtsField.GetValue(blockNode);
                    else body.Add(blockNode); // Fallback
                }
                Console.WriteLine($"🔍 [AstBuilder X-Ray] 函數 '{name}' 的 Body 語句數量: {body.Count}");
                Console.WriteLine($"🔍 [AST-Raw] Block ChildCount: {context.block().ChildCount}");
                var rawText = context.block().GetText();
                Console.WriteLine($"🔍 [AST-Raw] Block Text: {rawText.Substring(0, Math.Min(150, rawText.Length))}...");
            return CreateNode("FunctionDeclarationNode", new Dictionary<string, object> {
                { "ReturnType", retType }, { "Name", name }, { "Parameters", parameters }, { "Body", body }
            });
        }

        public override AstNode VisitBlock(LPCParser.BlockContext context) {
            var block = new BlockNode();
            // 【終極降維】放棄 context.statement()，直接遍歷最底層的 context.children！
            if (context.children != null) {
                foreach (var child in context.children) {
                    if (child is LPCParser.StatementContext stmtCtx) {
                        var astNode = Visit(stmtCtx);
                        Console.WriteLine($"🔍 [Block X-Ray] Child Type: {child.GetType().Name}, AST Node: {(astNode != null ? astNode.GetType().Name : "NULL")}");
                        if (astNode != null) block.Statements.Add(astNode);
                    }
                }
            }
            return block;
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

        public override AstNode VisitExpr(LPCParser.ExprContext context) {
            return Visit(context.assignmentExpr());
        }

        public override AstNode VisitAssignmentExpr(LPCParser.AssignmentExprContext context) {
            if (context.assignmentExpr() != null) {
                var left = Visit(context.logicalOrExpr());
                var right = Visit(context.assignmentExpr());
                
                // 【關鍵修復】AssignmentNode 需要 VariableName (字串)，而不是 Target (節點)
                string varName = "unknown";
                if (left != null) {
                    var nameProp = left.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var nameField = left.GetType().GetField("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (nameProp != null) varName = nameProp.GetValue(left)?.ToString() ?? "unknown";
                    else if (nameField != null) varName = nameField.GetValue(left)?.ToString() ?? "unknown";
                }
                
                return CreateNode("AssignmentNode", new Dictionary<string, object> { { "VariableName", varName }, { "Value", right } });
            }
            return Visit(context.logicalOrExpr());
        }

        private AstNode ProcessBinaryOps(ParserRuleContext context, ParserRuleContext[] children) {
            if (children == null || children.Length == 0) return null;
            AstNode node = Visit(children[0]);
            if (node == null) return null;
            
            int childIdx = 1;
            for (int i = 1; i < context.ChildCount && childIdx < children.Length; i++) {
                if (context.GetChild(i) is ITerminalNode term) {
                    string op = term.GetText();
                    AstNode right = Visit(children[childIdx]);
                    if (right == null) continue;
                    node = CreateNode("BinaryOpNode", new Dictionary<string, object> {
                        { "Left", node }, { "Right", right }, { "Op", op }
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
                return CreateNode("BinaryOpNode", new Dictionary<string, object> { { "Left", null }, { "Right", Visit(context.unaryExpr()) }, { "Op", op } });
            }
            return Visit(context.postfixExpr());
        }

        public override AstNode VisitPostfixExpr(LPCParser.PostfixExprContext context) {
            AstNode node = Visit(context.primaryExpr());
            if (context.ChildCount > 1) {
                var second = context.GetChild(1);
                if (second is ITerminalNode term) {
                    if (term.Symbol.Type == LPCParser.LPAREN) {
                        var args = new List<AstNode>();
                        if (context.argList() != null && context.argList().Length > 0) {
                            var al = context.argList(0);
                            if (al.expr() != null) foreach(var e in al.expr()) args.Add(Visit(e));
                        }
                        // FunctionCallNode 只有 Name，我們將引數嘗試塞入 Arguments 或 Args
                        string funcName = "unknown";
                        if (node != null) {
                            var nameProp = node.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            var nameField = node.GetType().GetField("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (nameProp != null) funcName = nameProp.GetValue(node)?.ToString() ?? "unknown";
                            else if (nameField != null) funcName = nameField.GetValue(node)?.ToString() ?? "unknown";
                        }
                        node = CreateNode("FunctionCallNode", new Dictionary<string, object> { { "Name", funcName }, { "Arguments", args }, { "Args", args } });
                    } else if (term.Symbol.Type == LPCParser.ARROW) {
                        string funcName = context.GetChild(2).GetText();
                        var args = new List<AstNode>();
                        node = CreateNode("CallOtherNode", new Dictionary<string, object> { { "Target", node }, { "FuncName", funcName }, { "Arguments", args } });
                    }
                }
            }
            return node;
        }

        public override AstNode VisitPrimaryExpr(LPCParser.PrimaryExprContext context) {
            // 【終極路由】強制精確轉發給 Literal Visitor！
            if (context.mappingLiteral() != null) return Visit(context.mappingLiteral());
            if (context.arrayLiteral() != null) return Visit(context.arrayLiteral());
            return base.VisitPrimaryExpr(context);
        }
    
        public override AstNode VisitArrayLiteral(LPCParser.ArrayLiteralContext context) {
            Console.WriteLine($"🔍 [AST X-Ray] VisitArrayLiteral called! Expr count: {(context.expr() != null ? context.expr().Length : 0)}");
            var node = new ArrayLiteralNode();
            if (context.expr() != null) {
                foreach (var e in context.expr()) {
                    var astNode = Visit(e);
                    if (astNode != null) node.Elements.Add(astNode);
                }
            }
            return node;
        }

        public override AstNode VisitMappingLiteral(LPCParser.MappingLiteralContext context) {
            Console.WriteLine($"🔍 [AST X-Ray] VisitMappingLiteral called! Expr count: {(context.expr() != null ? context.expr().Length : 0)}");
            var node = new MappingLiteralNode();
            if (context.expr() != null) {
                var exprs = context.expr();
                for (int i = 0; i < exprs.Length; i += 2) {
                    var keyNode = Visit(exprs[i]);
                    var valNode = (i + 1 < exprs.Length) ? Visit(exprs[i + 1]) : null;
                    if (keyNode != null) node.Keys.Add(keyNode);
                    if (valNode != null) node.Values.Add(valNode);
                }
            }
            return node;
        }
}
}
