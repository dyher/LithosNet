using System;
using System.Collections.Generic;
using LithosNet.Core;

namespace LithosNet.VM {
    // 用於 return 語句的中斷控制
    public class ReturnSignal : Exception {
        public LpcValue Value { get; }
        public ReturnSignal(LpcValue value) { Value = value; }
    }

    public class Interpreter {
        private readonly Scope _scope;

        public Interpreter(Scope scope) { _scope = scope; }

        public void Execute(List<AstNode> ast) {
            foreach (var node in ast) Visit(node);
        }

        // 【核心】呼叫 LPC 函數
        public LpcValue CallFunction(string name, List<LpcValue> args) {
            var func = _scope.GetFunction(name);
            Console.WriteLine($"   ⚡ [VM] 呼叫 LPC 函數: {name}()");

            // 將參數綁定到 Scope
            for (int i = 0; i < func.Parameters.Count && i < args.Count; i++) {
                _scope.Set(func.Parameters[i].Name, args[i]);
            }

            // 執行函數體
            try {
                foreach (var stmt in func.Body) Visit(stmt);
            } catch (ReturnSignal ret) {
                Console.WriteLine($"   ⚡ [VM] 函數 {name}() 返回: {ret.Value}");
                return ret.Value;
            }
            return LpcValue.Create(0); // void 函數預設返回 0
        }

        private void Visit(AstNode node) {
            switch (node) {
                case VariableDeclarationNode v: VisitVarDecl(v); break;
                case FunctionDeclarationNode f: VisitFuncDecl(f); break;
                case ReturnNode r: VisitReturn(r); break;
                case FunctionCallNode c: EvalCall(c); break;
            }
        }

        private void VisitVarDecl(VariableDeclarationNode node) {
            LpcValue val = node.Initializer != null ? Eval(node.Initializer) : LpcValue.Create(0);
            _scope.Set(node.VariableName, val);
            Console.WriteLine($"   [VM] 已配置變數: {node.VariableName} = {val}");
        }

        private void VisitFuncDecl(FunctionDeclarationNode node) {
            _scope.RegisterFunction(node);
            Console.WriteLine($"   [VM] 已註冊函數: {node.ReturnType} {node.Name}({node.Parameters.Count} params)");
        }

        private void VisitReturn(ReturnNode node) {
            LpcValue val = node.Value != null ? Eval(node.Value) : LpcValue.Create(0);
            throw new ReturnSignal(val); // 用異常實現 return 中斷
        }

        private LpcValue Eval(AstNode node) {
            return node switch {
                LiteralNode lit => lit.Value,
                VariableRefNode vref => _scope.Get(vref.Name),
                FunctionCallNode call => EvalCall(call),
                _ => LpcValue.Create(0)
            };
        }

        private LpcValue EvalCall(FunctionCallNode node) {
            var args = new List<LpcValue>();
            foreach (var a in node.Arguments) args.Add(Eval(a));
            return CallFunction(node.Name, args);
        }
    }
}
