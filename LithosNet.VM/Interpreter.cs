using System;
using System.Collections.Generic;
using LithosNet.Core;

namespace LithosNet.VM {
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

        public LpcValue CallFunction(string name, List<LpcValue> args) {
            // 1. 優先尋找 LPC 物件內部的函數
            if (_scope.HasFunction(name)) {
                var func = _scope.GetFunction(name);
                Console.WriteLine($"   ⚡ [VM] 呼叫 LPC 函數: {name}()");
                for (int i = 0; i < func.Parameters.Count && i < args.Count; i++) {
                    _scope.Set(func.Parameters[i].Name, args[i]);
                }
                try {
                    foreach (var stmt in func.Body) Visit(stmt);
                } catch (ReturnSignal ret) {
                    return ret.Value;
                }
                return LpcValue.Create(0);
            }
            
            // 2. 如果物件內沒有，則尋找底層 C# Efun
            if (EfunRegistry.TryGet(name, out var efun)) {
                Console.WriteLine($"   🔌 [VM] 呼叫底層 Efun: {name}()");
                return efun(args.ToArray());
            }

            throw new Exception($"[VM] Function or Efun '{name}' not found.");
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
        }

        private void VisitFuncDecl(FunctionDeclarationNode node) {
            _scope.RegisterFunction(node);
        }

        private void VisitReturn(ReturnNode node) {
            LpcValue val = node.Value != null ? Eval(node.Value) : LpcValue.Create(0);
            throw new ReturnSignal(val);
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
