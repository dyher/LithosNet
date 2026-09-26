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
                var rawText = context.block().GetText();
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
            var node = new ReturnNode();
            if (context.expr() != null) node.Value = Visit(context.expr());
            return node;
        }

        public override AstNode VisitExprStmt(LPCParser.ExprStmtContext context) {
            return Visit(context.expr());
        }

        public override AstNode VisitExpr(LPCParser.ExprContext context) {
            return Visit(context.assignmentExpr());
        }

        public override AstNode VisitAssignmentExpr(LPCParser.AssignmentExprContext context) {
            AstNode left = Visit(context.logicalOrExpr());
            if (left == null) return null;

            // 如果沒有賦值運算子，它就是一個純表達式，直接返回
            if (context.assignmentExpr() == null) return left;

            AstNode right = Visit(context.assignmentExpr());

            // 【終極路由】如果左邊是 IndexAccessNode，生成 IndexAssignmentNode！
            if (left is IndexAccessNode ian) {
                var idxAssignNode = new IndexAssignmentNode();
                idxAssignNode.Array = ian.Array;
                idxAssignNode.Index = ian.Index;
                idxAssignNode.Value = right;
                return idxAssignNode;
            }

            // 一般變數賦值
            string varName = "unknown";
            var nameProp = left.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var nameField = left.GetType().GetField("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (nameProp != null) varName = nameProp.GetValue(left)?.ToString() ?? "unknown";
            else if (nameField != null) varName = nameField.GetValue(left)?.ToString() ?? "unknown";

            return CreateNode("AssignmentNode", new Dictionary<string, object> { { "VariableName", varName }, { "Value", right } });
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
            for (int _i = 0; _i < context.ChildCount; _i++) {
                var _c = context.GetChild(_i);
            }
            
            AstNode node = Visit(context.primaryExpr());
            int i = 1;
            // 【終極循環】完美處理所有後綴操作 (函數呼叫、Call Other、索引訪問)
            while (i < context.ChildCount) {
                var child = context.GetChild(i);
                if (child is ITerminalNode term) {
                    if (term.Symbol.Type == LPCParser.LPAREN) {
                        var args = new System.Collections.Generic.List<AstNode>();
                        if (i + 1 < context.ChildCount && context.GetChild(i + 1) is LPCParser.ArgListContext al) {
                            if (al.expr() != null) foreach(var e in al.expr()) args.Add(Visit(e));
                            i++; // skip argList
                        }
                        string funcName = "unknown";
                        if (node != null) {
                            var nameProp = node.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            var nameField = node.GetType().GetField("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (nameProp != null) funcName = nameProp.GetValue(node)?.ToString() ?? "unknown";
                            else if (nameField != null) funcName = nameField.GetValue(node)?.ToString() ?? "unknown";
                        }
                        node = CreateNode("FunctionCallNode", new Dictionary<string, object> { { "Name", funcName }, { "Arguments", args }, { "Args", args } });
                        i++; // skip RPAREN
                    } else if (term.Symbol.Type == LPCParser.ARROW) {
                        string funcName = context.GetChild(i + 1).GetText();
                        i++; // skip ID
                        var args = new System.Collections.Generic.List<AstNode>();
                        if (i + 1 < context.ChildCount && context.GetChild(i + 1) is ITerminalNode n2 && n2.Symbol.Type == LPCParser.LPAREN) {
                            if (i + 2 < context.ChildCount && context.GetChild(i + 2) is LPCParser.ArgListContext al) {
                                if (al.expr() != null) foreach(var e in al.expr()) args.Add(Visit(e));
                                i++; // skip argList
                            }
                            i++; // skip LPAREN
                            i++; // skip RPAREN
                        }
                        node = CreateNode("CallOtherNode", new Dictionary<string, object> { { "Target", node }, { "FuncName", funcName }, { "Arguments", args }, { "Args", args } });
                    } else if (term.Symbol.Type == LPCParser.LBRACKET) {
                        try {
                            // 【創世補齊】完美處理索引訪問 a[b]！
                            var indexNode = Visit(context.GetChild(i + 1));
                            node = CreateNode("IndexAccessNode", new Dictionary<string, object> { { "Array", node }, { "Target", node }, { "Index", indexNode } });
                            Console.WriteLine($"🔍 [Postfix LBRACKET X-Ray] CreateNode returned: {(node != null ? node.GetType().Name : "NULL")}");
                            i++; // skip expr
                            i++; // skip RBRACKET
                        } catch (Exception ex) {
                        }
                    }
                }
                i++;
            }
            return node;
        }

        public override AstNode VisitPrimaryExpr(LPCParser.PrimaryExprContext context) {
            if (context.CLOSURE_OPEN() != null && context.ID() != null) {
                return new FunctionPointerNode { FuncName = context.ID().GetText() };
            }

            
            // 【最高優先級】Literal 路由
            if (context.mappingLiteral() != null) return Visit(context.mappingLiteral());
            if (context.arrayLiteral() != null) return Visit(context.arrayLiteral());
            
            // 【原有核心邏輯】變數與常數處理
            if (context.ID() != null) return CreateNode("VariableRefNode", new Dictionary<string, object> { { "Name", context.ID().GetText() } });
            if (context.INT_LITERAL() != null) return CreateNode("LiteralNode", new Dictionary<string, object> { { "Value", LpcValue.Create(int.Parse(context.INT_LITERAL().GetText())) } });
            if (context.STRING_LITERAL() != null) return CreateNode("LiteralNode", new Dictionary<string, object> { { "Value", LpcValue.Create(context.STRING_LITERAL().GetText().Trim('"')) } });
            
            // 【遞迴處理】括號表達式
            if (context.expr() != null) return Visit(context.expr());
            
            return null;
        }
    
        public override AstNode VisitArrayLiteral(LPCParser.ArrayLiteralContext context) {
            string rawText = context.GetText();
            
            // 【型別劫持】如果以 ([ 開頭，強制當作 Mapping 處理！
            if (rawText.StartsWith("([") || rawText.StartsWith("([")) {
                var mapNode = new MappingLiteralNode();
                if (context.expr() != null) {
                    var exprs = context.expr();
                    for (int i = 0; i < exprs.Length; i += 2) {
                        var keyNode = Visit(exprs[i]);
                        var valNode = (i + 1 < exprs.Length) ? Visit(exprs[i + 1]) : null;
                        if (keyNode != null) mapNode.Keys.Add(keyNode);
                        if (valNode != null) mapNode.Values.Add(valNode);
                    }
                }
                return mapNode;
            }

            // 原本的 Array 邏輯
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

        public override AstNode VisitSwitchStmt(LPCParser.SwitchStmtContext context) {
            var node = new SwitchNode();
            if (context.expr() != null) node.Condition = Visit(context.expr());
            if (context.switchBlock() != null) {
                if (context.switchBlock().caseBlock() != null) {
                    foreach(var c in context.switchBlock().caseBlock()) {
                        var caseNode = new SwitchCaseNode();
                        if (c.expr() != null) caseNode.Value = Visit(c.expr());
                        if (c.statement() != null) {
                            foreach(var s in c.statement()) {
                                var astNode = Visit(s);
                                if (astNode != null) caseNode.Body.Add(astNode);
                            }
                        }
                        node.Cases.Add(caseNode);
                    }
                }
                if (context.switchBlock().defaultBlock() != null) {
                    var defNode = new SwitchCaseNode();
                    defNode.IsDefault = true;
                    var db = context.switchBlock().defaultBlock();
                    if (db.statement() != null) {
                        foreach(var s in db.statement()) {
                            var astNode = Visit(s);
                            if (astNode != null) defNode.Body.Add(astNode);
                        }
                    }
                    node.Cases.Add(defNode);
                }
            }
            return node;
        }

        public override AstNode VisitBreakStmt(LPCParser.BreakStmtContext context) {
            return new BreakNode();
        }

}
}
        public override AstNode VisitForeachStmt(LPCParser.ForeachStmtContext context) {
            var node = new ForeachNode {
                VarName = context.ID().GetText(),
                Collection = Visit(context.expr())
            };
            node.Body = Visit(context.statement());
            return node;
        }
